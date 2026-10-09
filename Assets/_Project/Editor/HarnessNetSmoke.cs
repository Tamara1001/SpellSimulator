using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using SpellSimulator.Core;
using SpellSimulator.Net;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Pruebas de red de F1 (MULTIPLAYER_PLAN), en Play y sin gente:
    /// - "Run Net Smoke (local)": un anfitrion y un cliente virtuales en el mismo proceso, por loopback (sin nube). Chequea que conecten,
    ///   que cada uno tenga su avatar, que el movimiento del cliente llegue al anfitrion y que al salir se limpie el registro.
    /// - "Run Net Relay Probe": pide una sala a Relay por WebSocket (necesita Internet y el proyecto de Unity Cloud enlazado) y la cierra.
    /// Resultado en Logs/harness-netsmoke.txt.
    /// </summary>
    public static class HarnessNetSmoke
    {
        private const string FlagKey = "HarnessNetSmoke.Mode"; // "", "local", "relay"
        private const string StartFrameKey = "HarnessNetSmoke.StartFrame";
        private const int WarmupFrames = 30;
        private const ushort Port = 7788;
        private static readonly string ResultPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "harness-netsmoke.txt"));

        private static readonly List<string> failures = new List<string>();
        private static readonly List<string> notes = new List<string>();
        private static int passed;
        private static IEnumerator routine;
        private static float waitUntil;
        private static NetworkManager host, client;

        [MenuItem("SpellSimulator/Harness/Run Net Smoke (local)")]
        public static void RunLocal() => Start("local");

        [MenuItem("SpellSimulator/Harness/Run Net Relay Probe")]
        public static void RunRelay() => Start("relay");

        private static void Start(string mode)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.WriteAllText(ResultPath, "RUNNING");
            SessionState.SetString(FlagKey, mode);
            SessionState.SetInt(StartFrameKey, -1);
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Hook() => EditorApplication.update += Tick;

        private static void Tick()
        {
            string mode = SessionState.GetString(FlagKey, "");
            if (mode == "" || !EditorApplication.isPlaying) return;
            int start = SessionState.GetInt(StartFrameKey, -1);
            if (start < 0)
            {
                SessionState.SetInt(StartFrameKey, Time.frameCount);
                failures.Clear(); notes.Clear(); passed = 0; routine = null; host = client = null;
                return;
            }
            if (Time.frameCount - start < WarmupFrames) return;
            if (routine == null) routine = mode == "relay" ? ProbeRelay() : ProbeLocal();
            if (Time.realtimeSinceStartup < waitUntil) return;
            bool more;
            try
            {
                more = routine.MoveNext();
                if (more && routine.Current is float seconds) waitUntil = Time.realtimeSinceStartup + seconds;
            }
            catch (System.Exception e) { failures.Add("excepcion: " + e); more = false; }
            if (more) return;

            SessionState.SetString(FlagKey, "");
            Cleanup();
            string result = (failures.Count == 0 ? $"PASS ({passed} chequeos)" : $"FAIL {failures.Count} de {passed + failures.Count}: " + string.Join(" | ", failures))
                            + (notes.Count > 0 ? "\nNOTAS: " + string.Join(" | ", notes) : "");
            File.WriteAllText(ResultPath, result);
            Debug.Log("[NetSmoke] " + result);
            EditorApplication.isPlaying = false;
        }

        private static void Check(bool ok, string name) { if (ok) passed++; else failures.Add(name); }

        private static void Cleanup()
        {
            if (client != null) { if (client.IsListening) client.Shutdown(); Object.Destroy(client.gameObject); client = null; }
            if (host != null) { if (host.IsListening) host.Shutdown(); Object.Destroy(host.gameObject); host = null; }
        }

        private static NetPlayer PlayerOf(NetworkManager nm, ulong ownerId)
        {
            foreach (var p in Object.FindObjectsByType<NetPlayer>(FindObjectsSortMode.None))
                if (p.NetworkManager == nm && p.OwnerClientId == ownerId) return p;
            return null;
        }

        private static IEnumerator ProbeLocal()
        {
            if (Resources.Load<GameObject>(NetSession.AvatarResource) == null) { failures.Add("falta el avatar de red (menu SpellSimulator/Net/Crear avatar de red)"); yield break; }

            host = NetSession.CreateNetworkManager("NM_Host", true);
            client = NetSession.CreateNetworkManager("NM_Client", false);
            if (host == null || client == null) { failures.Add("no se pudieron crear los NetworkManager"); yield break; }
            host.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", Port, "0.0.0.0");
            client.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", Port);
            Check(host.StartHost(), "el anfitrion inicia");
            yield return 0.3f;
            Check(client.StartClient(), "el cliente inicia");

            float limit = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < limit && !(client.IsConnectedClient && PlayerOf(host, client.LocalClientId) != null && PlayerOf(client, client.LocalClientId) != null))
                yield return 0.1f;
            Check(client.IsConnectedClient, "el cliente se conecta");
            var hostOwn = PlayerOf(host, host.LocalClientId);
            var clientOnHost = PlayerOf(host, client.LocalClientId);
            var clientOwn = PlayerOf(client, client.LocalClientId);
            var hostOnClient = PlayerOf(client, host.LocalClientId);
            Check(hostOwn != null && hostOwn.IsOwner, "el anfitrion tiene su avatar (dueno)");
            Check(clientOnHost != null && !clientOnHost.IsOwner, "el anfitrion ve el avatar del cliente");
            Check(clientOwn != null && clientOwn.IsOwner, "el cliente tiene su avatar (dueno)");
            Check(hostOnClient != null && !hostOnClient.IsOwner, "el cliente ve el avatar del anfitrion");
            if (hostOwn == null || clientOnHost == null || clientOwn == null || hostOnClient == null) yield break;
            Check(host.LocalClientId != client.LocalClientId, "los ids de cliente son distintos");

            // el cliente camina 2 m: el anfitrion tiene que ver el avatar remoto moverse
            var hostDriver = new GameObject("HostDriver").transform;
            var clientDriver = new GameObject("ClientDriver").transform;
            hostDriver.position = new Vector3(0f, 0f, 0f);
            clientDriver.position = new Vector3(5f, 0f, 0f);
            hostOwn.SetDriver(hostDriver);
            clientOwn.SetDriver(clientDriver);
            yield return 1f;
            Vector3 before = clientOnHost.transform.position;
            for (int i = 1; i <= 20; i++) { clientDriver.position = new Vector3(5f + i * 0.1f, 0f, 0f); clientDriver.rotation = Quaternion.Euler(0f, 90f, 0f); yield return 0.05f; }
            yield return 1.2f;
            Vector3 after = clientOnHost.transform.position;
            Check(Vector3.Distance(before, new Vector3(5f, 0f, 0f)) < 0.5f, $"el avatar remoto parte del punto inicial (esta en {before})");
            Check(Mathf.Abs(after.x - 7f) < 0.35f, $"el anfitrion ve al cliente avanzar 2 m (x={after.x:F2}, esperado 7)");
            Check(Mathf.Abs(Mathf.DeltaAngle(clientOnHost.transform.eulerAngles.y, 90f)) < 15f, $"el giro llega (y={clientOnHost.transform.eulerAngles.y:F0})");
            Check(clientOnHost.DisplayName == NetSession.PlayerName, $"el nombre llega (\"{clientOnHost.DisplayName}\")");

            // registro de jugadores: los remotos se registran como no locales
            int remotes = PlayerRegistry.Players.Count(p => p != null && !p.IsLocal);
            Check(remotes >= 1, $"los jugadores remotos estan en PlayerRegistry (remotos={remotes})");
            Check(PlayerRegistry.ById((int)client.LocalClientId) != null, "PlayerRegistry.ById encuentra al cliente");

            // el cliente se va: su avatar desaparece del anfitrion
            client.Shutdown();
            yield return 1.5f;
            Check(PlayerOf(host, 1UL) == null && Object.FindObjectsByType<NetPlayer>(FindObjectsSortMode.None).Count(p => p.NetworkManager == host) == 1, "al irse el cliente su avatar desaparece del anfitrion");
            Object.Destroy(hostDriver.gameObject); Object.Destroy(clientDriver.gameObject);
        }

        private static IEnumerator ProbeRelay()
        {
            if (Resources.Load<GameObject>(NetSession.AvatarResource) == null) { failures.Add("falta el avatar de red"); yield break; }
            var session = NetSession.Instance;
            var task = session.HostAsync(2);
            float limit = Time.realtimeSinceStartup + 40f;
            while (!task.IsCompleted && Time.realtimeSinceStartup < limit) yield return 0.2f;
            Check(task.IsCompleted, "Relay responde a tiempo (40 s)");
            if (!task.IsCompleted) yield break;
            if (!task.Result) { failures.Add("HostAsync fallo: " + session.LastError); yield break; }
            Check(session.State == NetState.Hosting, "estado Hosting");
            Check(!string.IsNullOrEmpty(session.JoinCode), "hay codigo de sala (" + session.JoinCode + ")");
            var nm = NetworkManager.Singleton;
            Check(nm != null && nm.IsHost, "el NetworkManager esta en modo anfitrion");
            var utp = nm != null ? nm.GetComponent<UnityTransport>() : null;
            Check(utp != null && utp.Protocol == UnityTransport.ProtocolType.RelayUnityTransport, "el transporte usa Relay");
            notes.Add("codigo=" + session.JoinCode + (utp != null ? " websockets=" + utp.UseWebSockets + " wss=" + utp.UseEncryption : ""));
            Check(utp != null && utp.UseWebSockets, "el transporte usa WebSocket");
            session.Leave();
            yield return 2f;
            Check(session.State == NetState.Offline, "al salir vuelve a Offline");
        }
    }
}
