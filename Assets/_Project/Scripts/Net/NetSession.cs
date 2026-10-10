using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace SpellSimulator.Net
{
    public enum NetState { Offline, Connecting, Hosting, Joined, Failed }

    /// <summary>
    /// Sesion de red (F1 de MULTIPLAYER_PLAN): crea o se une a una partida por codigo de Relay (transporte WebSocket/wss, para que
    /// PC, Quest y WebGL jueguen juntos) o por IP directa (pruebas locales sin nube). Crea en tiempo de ejecucion el NetworkManager
    /// con el avatar de red de <c>Resources/Net/NetPlayer</c>; no hace falta nada en la escena.
    /// </summary>
    public sealed class NetSession : MonoBehaviour
    {
        public const int MaxPlayersCoop = 4;
        public const string AvatarResource = "Net/NetPlayer";
        private const string NameKey = "net.name";

        private static NetSession instance;
        private ISession session;
        private bool hooked;
        private int mainThreadId;
        private volatile bool pendingChanged;
        private volatile string pendingLost;
        private bool leaving;

        public static NetSession Instance
        {
            get
            {
                if (instance != null) return instance;
                var go = new GameObject("NetSession");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<NetSession>();
                return instance;
            }
        }

        public static bool Exists => instance != null;

        private void Awake() => mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

        private void WarnIfOffMainThread(string where)
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != mainThreadId)
                Debug.LogWarning("[Net] " + where + " completo fuera del hilo principal (los eventos se pasan al hilo principal en Update).");
        }

        public NetState State { get; private set; } = NetState.Offline;
        /// <summary>Codigo para compartir (solo el anfitrion por Relay).</summary>
        public string JoinCode { get; private set; } = "";
        public string LastError { get; private set; } = "";
        public bool IsHost => State == NetState.Hosting;
        public bool IsConnected => State == NetState.Hosting || State == NetState.Joined;
        /// <summary>Se emite al cambiar el estado, el codigo o el error.</summary>
        public event Action Changed;
        /// <summary>Se emite (en el hilo principal) cuando la conexion se pierde por causas ajenas: el anfitrion se fue o cerro la sala. No se emite al salir uno mismo.</summary>
        public static event Action<string> Disconnected;

        public static string PlayerName
        {
            get => PlayerPrefs.GetString(NameKey, "Mago");
            set { PlayerPrefs.SetString(NameKey, string.IsNullOrWhiteSpace(value) ? "Mago" : value.Trim()); }
        }

        /// <summary>Crea (si falta) el NetworkManager con UnityTransport y el avatar registrado. Publico para pruebas con varias instancias.</summary>
        public static NetworkManager CreateNetworkManager(string objectName = "NetworkManager", bool makeSingleton = true)
        {
            if (makeSingleton && NetworkManager.Singleton != null) return NetworkManager.Singleton;
            var prefab = Resources.Load<GameObject>(AvatarResource);
            if (prefab == null) { Debug.LogError("[Net] Falta Resources/" + AvatarResource + " (menu SpellSimulator/Net/Crear avatar de red)."); return null; }

            var go = new GameObject(objectName);
            go.SetActive(false); // asi la configuracion queda lista antes del Awake del NetworkManager
            DontDestroyOnLoad(go);
            var transport = go.AddComponent<UnityTransport>();
            var nm = go.AddComponent<NetworkManager>();
            nm.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                PlayerPrefab = prefab,
                EnableSceneManagement = false,
                TickRate = 30,
            };
            nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
            go.SetActive(true);
            return nm;
        }

        // ---- Relay (internet) ----

        public async Task<bool> HostAsync(int maxPlayers = MaxPlayersCoop)
        {
            if (!BeginConnect()) return false;
            try
            {
                UseTransport(CreateNetworkManager(), true);
                await EnsureServicesAsync();
                var options = new SessionOptions { MaxPlayers = maxPlayers }.WithRelayNetwork().WithNetworkOptions(WssOptions());
                session = await MultiplayerService.Instance.CreateSessionAsync(options);
                WarnIfOffMainThread("HostAsync");
                HookSession(session);
                JoinCode = session.Code ?? "";
                SetState(NetState.Hosting);
                return true;
            }
            catch (Exception e) { Fail(e); return false; }
        }

        public async Task<bool> JoinAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) { Fail(new ArgumentException("codigo vacio")); return false; }
            if (!BeginConnect()) return false;
            try
            {
                UseTransport(CreateNetworkManager(), true);
                await EnsureServicesAsync();
                session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant(), new JoinSessionOptions().WithNetworkOptions(WssOptions()));
                WarnIfOffMainThread("JoinAsync");
                HookSession(session);
                JoinCode = session.Code ?? code;
                SetState(NetState.Joined);
                return true;
            }
            catch (Exception e) { Fail(e); return false; }
        }

        // ---- IP directa (pruebas locales sin nube) ----

        public bool StartDirectHost(ushort port = 7777)
        {
            if (!BeginConnect()) return false;
            var nm = CreateNetworkManager();
            if (nm == null) { Fail(new InvalidOperationException("sin avatar de red")); return false; }
            UseTransport(nm, false);
            nm.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", port, "0.0.0.0");
            if (!nm.StartHost()) { Fail(new InvalidOperationException("no se pudo iniciar el anfitrion")); return false; }
            JoinCode = "127.0.0.1:" + port;
            SetState(NetState.Hosting);
            return true;
        }

        public bool StartDirectClient(string ip = "127.0.0.1", ushort port = 7777)
        {
            if (!BeginConnect()) return false;
            var nm = CreateNetworkManager();
            if (nm == null) { Fail(new InvalidOperationException("sin avatar de red")); return false; }
            UseTransport(nm, false);
            nm.GetComponent<UnityTransport>().SetConnectionData(ip, port);
            if (!nm.StartClient()) { Fail(new InvalidOperationException("no se pudo conectar")); return false; }
            JoinCode = ip + ":" + port;
            SetState(NetState.Joined);
            return true;
        }

        /// <summary>Sale de la partida y vuelve a modo solitario.</summary>
        public async void Leave()
        {
            leaving = true;
            try
            {
                if (session != null)
                {
                    var s = session; session = null;
                    UnhookSession(s);
                    if (s.IsHost) await s.AsHost().DeleteAsync(); else await s.LeaveAsync();
                }
            }
            catch (Exception e) { Debug.LogWarning("[Net] al salir: " + e.Message); }
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening) nm.Shutdown();
            JoinCode = "";
            pendingLost = null;
            leaving = false;
            SetState(NetState.Offline);
        }

        private void HookSession(ISession s)
        {
            s.RemovedFromSession += OnSessionGone;
            s.Deleted += OnSessionGone;
        }

        private void UnhookSession(ISession s)
        {
            s.RemovedFromSession -= OnSessionGone;
            s.Deleted -= OnSessionGone;
        }

        private void OnSessionGone() => Lose("La sala se cerró.");

        /// <summary>Marca la conexion como perdida; la limpieza y los eventos corren en Update (hilo principal).</summary>
        private void Lose(string reason)
        {
            if (leaving || !IsConnected) return;
            pendingLost = reason;
        }

        private bool BeginConnect()
        {
            if (State == NetState.Connecting || IsConnected) return false;
            LastError = "";
            SetState(NetState.Connecting);
            return true;
        }

        /// <summary>Relay va por WebSocket (wss): el transporte tiene que tenerlo activo antes de arrancar. La IP directa de pruebas va por UDP.</summary>
        private static void UseTransport(NetworkManager nm, bool webSockets)
        {
            if (nm == null) throw new InvalidOperationException("sin avatar de red");
            var t = nm.GetComponent<UnityTransport>();
            t.UseWebSockets = webSockets;
        }

        /// <summary>Todos van por WebSocket seguro (wss): es lo unico que entiende WebGL, y los demas deben usar el mismo transporte.</summary>
        private static NetworkOptions WssOptions() => new NetworkOptions { RelayProtocol = RelayProtocol.WSS };

        private static async Task EnsureServicesAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized) await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        private void Fail(Exception e)
        {
            LastError = e.Message;
            Debug.LogWarning("[Net] " + e);
            session = null;
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening) nm.Shutdown();
            SetState(NetState.Failed);
        }

        private void SetState(NetState s)
        {
            State = s;
            pendingChanged = true; // el HUD y los eventos salen en Update, siempre en el hilo principal
        }

        private void Update()
        {
            // engancha el callback de desconexion cuando el NetworkManager aparece
            var nm = NetworkManager.Singleton;
            if (nm != null && !hooked) { nm.OnClientDisconnectCallback += OnDisconnected; hooked = true; }

            string lost = pendingLost;
            if (lost != null)
            {
                pendingLost = null;
                if (IsConnected && !leaving) DropConnection(lost);
            }
            if (pendingChanged)
            {
                pendingChanged = false;
                if (IsConnected && GetComponent<NetHud>() == null) gameObject.AddComponent<NetHud>();
                Changed?.Invoke();
            }
        }

        private void DropConnection(string reason)
        {
            if (session != null) { UnhookSession(session); session = null; }
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening) nm.Shutdown();
            LastError = reason;
            JoinCode = "";
            State = NetState.Failed;
            pendingChanged = false;
            Changed?.Invoke();
            Disconnected?.Invoke(reason);
        }

        private void OnDisconnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || clientId != nm.LocalClientId || State != NetState.Joined) return;
            Lose("Se perdió la conexión con el anfitrión.");
        }

        private void OnDestroy() { if (instance == this) instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; Disconnected = null; }
    }
}
