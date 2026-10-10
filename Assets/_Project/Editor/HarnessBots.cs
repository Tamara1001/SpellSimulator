using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using SpellSimulator.Core;
using SpellSimulator.Net;
using SpellSimulator.World;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Prueba de red con bots (F1 de MULTIPLAYER_PLAN): el editor hospeda una sala y lanza N ejecutables de Windows (Builds/Bot, ver
    /// <see cref="BotBuild"/>) que se unen, caminan y escriben un informe. El anfitrion comprueba que los ve, que se mueven y que sus
    /// avatares desaparecen al irse. Menus: SpellSimulator/Harness/Run Bots (local | Relay | churn local).
    /// Resultado en Logs/harness-bots.txt; informes de cada bot en Logs/bots/.
    /// </summary>
    public static class HarnessBots
    {
        private const string FlagKey = "HarnessBots.Mode"; // "", "local", "relay", "churn"
        private const string StartFrameKey = "HarnessBots.StartFrame";
        private const int WarmupFrames = 30;
        private const ushort Port = 7788;
        private const int BotCount = 3;
        private static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static readonly string ResultPath = Path.Combine(Root, "Logs", "harness-bots.txt");
        private static readonly string BotLogDir = Path.Combine(Root, "Logs", "bots");

        private static readonly List<string> failures = new List<string>();
        private static readonly List<string> notes = new List<string>();
        private static readonly List<Process> processes = new List<Process>();
        private static int passed;
        private static IEnumerator routine;
        private static float waitUntil;

        [MenuItem("SpellSimulator/Harness/Run Bots (local)")]
        public static void RunLocal() => Start("local");

        [MenuItem("SpellSimulator/Harness/Run Bots (Relay)")]
        public static void RunRelay() => Start("relay");

        [MenuItem("SpellSimulator/Harness/Run Bots (churn local)")]
        public static void RunChurn() => Start("churn");

        [MenuItem("SpellSimulator/Harness/Run Bots Grab (local)")]
        public static void RunGrab() => Start("grab");

        [MenuItem("SpellSimulator/Harness/Run Bots Grab (150 ms)")]
        public static void RunGrabLatency() => Start("grab150");

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
                failures.Clear(); notes.Clear(); processes.Clear(); passed = 0; routine = null;
                return;
            }
            if (Time.frameCount - start < WarmupFrames) return;
            if (routine == null) routine = Probe(mode);
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
                            + (notes.Count > 0 ? "\nNOTAS:\n- " + string.Join("\n- ", notes) : "");
            File.WriteAllText(ResultPath, result);
            UnityEngine.Debug.Log("[Bots] " + result);
            EditorApplication.isPlaying = false;
        }

        private static void Check(bool ok, string name) { if (ok) passed++; else failures.Add(name); }

        private static void Cleanup()
        {
            foreach (var p in processes) { try { if (!p.HasExited) p.Kill(); } catch { } }
            processes.Clear();
            if (NetSession.Exists && NetSession.Instance.IsConnected) NetSession.Instance.Leave();
        }

        private static int RemoteCount()
        {
            int n = 0;
            var players = PlayerRegistry.Players;
            for (int i = 0; i < players.Count; i++) if (players[i] != null && players[i].IsAlive && !players[i].IsLocal) n++;
            return n;
        }

        private static long NowMs() => (long)(System.DateTime.UtcNow - new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)).TotalMilliseconds;

        private static IEnumerator WaitUntilMs(long ms) { while (NowMs() < ms) yield return null; }

        // Escenario de agarre de F2: tres bots pelean por una caja, se la pasan, una queda flotando; el anfitrion empuja 30 objetos; al final todos comparan posiciones.
        private static IEnumerator ProbeGrab(string mode)
        {
            int oneWayMs = mode == "grab150" ? 75 : 0;
            NetSession.SimulatedLatencyMs = oneWayMs;
            var menu = UI.MainMenu.Instance;
            yield return 1f;
            if (UI.MainMenu.IsActive) menu.EnterSandbox();
            yield return 0.5f;
            var session = NetSession.Instance;
            Check(session.StartDirectHost(Port), "el anfitrion abre el puerto local");
            if (!session.IsConnected) yield break;
            yield return 1f;
            NetWorld world = NetWorld.Active;
            Check(world != null && world.Began && world.BodyCount > 50, "el mundo compartido esta activo con muchos cuerpos (" + (world != null ? world.BodyCount : 0) + ")");
            if (world == null) yield break;

            Directory.CreateDirectory(BotLogDir);
            foreach (string old in Directory.GetFiles(BotLogDir)) { try { File.Delete(old); } catch { } }
            long t0 = NowMs() + 32000; // los bots tardan en cargar la escena y conectarse
            for (int i = 1; i <= BotCount; i++)
            {
                string name = "Bot" + i;
                string args = "-batchmode -nographics -logFile \"" + Path.Combine(BotLogDir, name + "-unity.log") + "\" -bot -botdirect 127.0.0.1:" + Port + " -botname " + name + " "
                              + "-botscript grab -botseconds 45 -botat " + t0 + " -botlatency " + oneWayMs + " -botlog \"" + Path.Combine(BotLogDir, name + ".txt") + "\"";
                var psi = new ProcessStartInfo(BotBuild.ExePath, args) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = BotBuild.OutputDir };
                processes.Add(Process.Start(psi));
                yield return 1.5f;
            }

            float limit = Time.realtimeSinceStartup + 40f;
            while (Time.realtimeSinceStartup < limit && RemoteCount() < BotCount) yield return 0.5f;
            Check(RemoteCount() >= BotCount, "los 3 bots se conectaron antes del arranque (" + RemoteCount() + ")");

            // T0+2.5: alguien (un bot) tiene Crate_0
            while (NowMs() < t0 + 2500) yield return null;
            world.TryGetBody("Crate_0", out NetBody crate0);
            int holder = crate0 != null ? crate0.HeldBy : -2;
            Check(holder > 0, "tras la pelea por Crate_0 la tiene un bot (dueno " + holder + ")");

            // T0+9: el anfitrion empuja 30 objetos
            while (NowMs() < t0 + 9000) yield return null;
            int pushed = 0;
            var rnd = new System.Random(11);
            foreach (var b in world.Bodies)
            {
                if (pushed >= 30) break;
                if (b == null || b.name == "Crate_0" || b.name == "Crate_1" || b.Rb == null || b.Rb.isKinematic || b.HeldBy != Holdable.NoOwner || !b.TryGetComponent(out Movable _)) continue;
                Vector3 dir = new Vector3((float)rnd.NextDouble() - 0.5f, 0.6f, (float)rnd.NextDouble() - 0.5f).normalized;
                b.Rb.AddForce(dir * 4f, ForceMode.VelocityChange);
                pushed++;
            }
            Check(pushed >= 25, "el anfitrion empujo " + pushed + " objetos");

            // T0+24: volcado del anfitrion
            while (NowMs() < t0 + 24000) yield return null;
            var hostPos = new Dictionary<int, Vector3>();
            foreach (var b in world.Bodies) if (b != null) hostPos[b.Id] = b.Rb.position;
            yield return 1f;

            float deadline = Time.realtimeSinceStartup + 40f;
            while (Time.realtimeSinceStartup < deadline && processes.Any(p => { try { return !p.HasExited; } catch { return false; } })) yield return 0.5f;
            Check(processes.All(p => { try { return p.HasExited; } catch { return true; } }), "los bots terminaron a tiempo");

            int wins = 0;
            var lines = new Dictionary<int, string[]>();
            for (int i = 1; i <= BotCount; i++)
            {
                string report = Path.Combine(BotLogDir, "Bot" + i + ".txt");
                string[] l = File.Exists(report) ? File.ReadAllLines(report) : new string[0];
                lines[i] = l;
                if (l.Any(x => x.Contains("GRAB Crate_0 WIN"))) wins++;
                int code = -1; try { code = processes[i - 1].HasExited ? processes[i - 1].ExitCode : -2; } catch { }
                Check(code == 0, "Bot" + i + " termina bien (codigo " + code + ")");
            }
            Check(wins == 1, "exactamente un bot se queda con Crate_0 (" + wins + ")");
            Check(lines[1].Any(x => x.Contains("GRAB Crate_1 OK")), "Bot1 agarra Crate_1");
            Check(lines[2].Any(x => x.Contains("GRAB Crate_1 OK")), "Bot2 agarra Crate_1 (ya soltado por Bot1)");
            Check(lines[3].Any(x => x.Contains("GRAB Crate_1 (ocupado) DENIED")), "Bot3 no puede agarrar Crate_1 mientras lo tiene Bot2");
            Check(lines[3].Any(x => x.Contains("GRAB Crate_1 (flotando) OK")), "Bot3 agarra Crate_1 cuando Bot2 lo dejo flotando");

            // posiciones: lo que ve cada bot contra el anfitrion
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            for (int i = 1; i <= BotCount; i++)
            {
                int total = 0, close = 0; float worst = 0f; string worstName = "";
                foreach (string line in lines[i])
                {
                    if (!line.StartsWith("BODY ")) continue;
                    string[] f = line.Split(' ');
                    if (f.Length < 5 || !int.TryParse(f[1], out int id) || !hostPos.TryGetValue(id, out Vector3 hp)) continue;
                    var bp = new Vector3(float.Parse(f[2], inv), float.Parse(f[3], inv), float.Parse(f[4], inv));
                    float d = Vector3.Distance(hp, bp);
                    total++;
                    if (d <= 0.05f) close++;
                    if (d > worst) { worst = d; worstName = id < world.Bodies.Count && world.Bodies[id] != null ? world.Bodies[id].name : "?"; }
                }
                Check(total > 50, "Bot" + i + " volco las posiciones (" + total + " cuerpos)");
                Check(total > 0 && close >= total * 0.98f && worst < 0.5f, "Bot" + i + " ve el mundo igual que el anfitrion: " + close + "/" + total + " a menos de 5 cm; peor " + worst.ToString("F2") + " m (" + worstName + ")");
            }
            NetSession.SimulatedLatencyMs = 0;
        }

        private static IEnumerator Probe(string mode)
        {
            if (!File.Exists(BotBuild.ExePath)) { failures.Add("falta el ejecutable de los bots (menu SpellSimulator/Build/Windows (bots))"); yield break; }
            if (mode.StartsWith("grab")) { var grab = ProbeGrab(mode); while (grab.MoveNext()) yield return grab.Current; yield break; }
            var menu = UI.MainMenu.Instance;
            if (menu == null) { failures.Add("no hay MainMenu"); yield break; }
            yield return 1f;
            if (UI.MainMenu.IsActive) menu.EnterSandbox();
            yield return 0.5f;

            // 1) el editor hospeda
            var session = NetSession.Instance;
            string joinArg;
            if (mode == "relay")
            {
                var task = session.HostAsync(BotCount + 1);
                float limit = Time.realtimeSinceStartup + 40f;
                while (!task.IsCompleted && Time.realtimeSinceStartup < limit) yield return 0.2f;
                Check(task.IsCompleted && task.Result, "el anfitrion crea la sala en Relay (" + session.LastError + ")");
                if (!session.IsConnected) yield break;
                joinArg = "-botjoin " + session.JoinCode;
            }
            else
            {
                Check(session.StartDirectHost(Port), "el anfitrion abre el puerto local");
                if (!session.IsConnected) yield break;
                joinArg = "-botdirect 127.0.0.1:" + Port;
            }
            yield return 1f;

            // el anfitrion camina en circulo para que los bots lo vean moverse
            NetPlayer hostAvatar = Object.FindObjectsByType<NetPlayer>(FindObjectsSortMode.None).FirstOrDefault(p => p.IsOwner);
            Transform driver = new GameObject("HostBotDriver").transform;
            Vector3 center = PlayerRegistry.Local != null && PlayerRegistry.Local.Root != null ? PlayerRegistry.Local.Root.position : Vector3.zero;
            driver.position = center;
            if (hostAvatar != null) hostAvatar.SetDriver(driver);
            else notes.Add("no encontre el avatar del anfitrion (no se mueve)");

            // 2) lanzar bots
            bool churn = mode == "churn";
            float botSeconds = churn ? 8f : 35f;
            Directory.CreateDirectory(BotLogDir);
            foreach (string old in Directory.GetFiles(BotLogDir)) { try { File.Delete(old); } catch { } }
            for (int i = 1; i <= BotCount; i++)
            {
                string name = "Bot" + i;
                string args = $"-batchmode -nographics -logFile \"{Path.Combine(BotLogDir, name + "-unity.log")}\" -bot {joinArg} -botname {name} "
                              + $"-botscript {(churn ? "churn" : "walk")} -botseconds {botSeconds:F0} -botexpect {(churn ? 1 : BotCount)} -botcycles 3 "
                              + $"-botlog \"{Path.Combine(BotLogDir, name + ".txt")}\"";
                var psi = new ProcessStartInfo(BotBuild.ExePath, args) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = BotBuild.OutputDir };
                var p = Process.Start(psi);
                processes.Add(p);
                yield return 1.5f; // escalonado: no cargar todos a la vez
            }
            Check(processes.Count == BotCount && processes.All(p => p != null), "se lanzaron los " + BotCount + " bots");

            // 3) observar desde el anfitrion
            float t0 = Time.realtimeSinceStartup;
            float deadline = t0 + (churn ? 220f : 150f);
            int maxRemotes = 0;
            var lastPos = new Dictionary<int, Vector3>();
            var traveled = new Dictionary<int, float>();
            float angle = 0f;
            while (Time.realtimeSinceStartup < deadline && processes.Any(p => { try { return !p.HasExited; } catch { return false; } }))
            {
                angle += 0.6f * 0.25f;
                driver.position = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 3f;
                driver.rotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg + 90f, 0f);
                var players = PlayerRegistry.Players;
                int remotes = 0;
                for (int i = 0; i < players.Count; i++)
                {
                    var p = players[i];
                    if (p == null || !p.IsAlive || p.IsLocal || p.Root == null) continue;
                    remotes++;
                    Vector3 pos = p.Root.position;
                    if (lastPos.TryGetValue(p.OwnerId, out Vector3 prev)) { traveled.TryGetValue(p.OwnerId, out float d); Vector3 flat = pos - prev; flat.y = 0f; traveled[p.OwnerId] = d + flat.magnitude; }
                    lastPos[p.OwnerId] = pos;
                }
                if (remotes > maxRemotes) maxRemotes = remotes;
                yield return 0.25f;
            }
            bool allExited = processes.All(p => { try { return p.HasExited; } catch { return true; } });
            Check(allExited, "los bots terminaron a tiempo");

            // 4) resultados
            int needAtOnce = churn ? 2 : BotCount;
            Check(maxRemotes >= needAtOnce, $"el anfitrion llego a ver {maxRemotes} bots a la vez (minimo {needAtOnce})");
            if (!churn)
            {
                int movers = traveled.Count(kv => kv.Value >= 4f);
                Check(movers >= BotCount, $"el anfitrion vio moverse a {movers} de {BotCount} bots (recorridos: {string.Join(", ", traveled.Select(kv => "#" + kv.Key + "=" + kv.Value.ToString("F1") + "m"))})");
            }
            for (int i = 0; i < processes.Count; i++)
            {
                int code = -1;
                try { code = processes[i].HasExited ? processes[i].ExitCode : -2; } catch { }
                string report = Path.Combine(BotLogDir, "Bot" + (i + 1) + ".txt");
                string resultLine = File.Exists(report) ? File.ReadAllLines(report).LastOrDefault(l => l.Contains("RESULT")) : "sin informe";
                Check(code == 0, $"Bot{i + 1} termina bien (codigo {code}): {resultLine}");
            }
            yield return 4f;
            int left = RemoteCount();
            Check(left == 0, $"al irse los bots no quedan avatares remotos ({left})");
            Object.Destroy(driver.gameObject);
            session.Leave();
            yield return 1f;
        }
    }
}
