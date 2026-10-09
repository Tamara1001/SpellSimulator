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
        public NetState State { get; private set; } = NetState.Offline;
        /// <summary>Codigo para compartir (solo el anfitrion por Relay).</summary>
        public string JoinCode { get; private set; } = "";
        public string LastError { get; private set; } = "";
        public bool IsHost => State == NetState.Hosting;
        public bool IsConnected => State == NetState.Hosting || State == NetState.Joined;
        /// <summary>Se emite al cambiar el estado, el codigo o el error.</summary>
        public event Action Changed;

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
            try
            {
                if (session != null)
                {
                    var s = session; session = null;
                    if (s.IsHost) await s.AsHost().DeleteAsync(); else await s.LeaveAsync();
                }
            }
            catch (Exception e) { Debug.LogWarning("[Net] al salir: " + e.Message); }
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening) nm.Shutdown();
            JoinCode = "";
            SetState(NetState.Offline);
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
            if ((s == NetState.Hosting || s == NetState.Joined) && GetComponent<NetHud>() == null) gameObject.AddComponent<NetHud>();
            Changed?.Invoke();
        }

        private void Update()
        {
            // engancha el callback de desconexion cuando el NetworkManager aparece
            var nm = NetworkManager.Singleton;
            if (nm != null && !hooked) { nm.OnClientDisconnectCallback += OnDisconnected; hooked = true; }
        }

        private void OnDisconnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || clientId != nm.LocalClientId || State != NetState.Joined) return;
            LastError = "Se perdio la conexion con el anfitrion.";
            JoinCode = "";
            SetState(NetState.Failed);
        }

        private void OnDestroy() { if (instance == this) instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;
    }
}
