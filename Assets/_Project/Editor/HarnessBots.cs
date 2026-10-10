using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using SpellSimulator.Combat;
using SpellSimulator.Core;
using SpellSimulator.Canvas;
using SpellSimulator.Net;
using SpellSimulator.Reactions;
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

        [MenuItem("SpellSimulator/Harness/Run Bots World (local)")]
        public static void RunWorld() => Start("world");

        [MenuItem("SpellSimulator/Harness/Run Bots Duel (local)")]
        public static void RunDuel() => Start("duel");

        [MenuItem("SpellSimulator/Harness/Run Bots Duel Ink (local)")]
        public static void RunDuelInk() => Start("duelink");

        [MenuItem("SpellSimulator/Harness/Run Bots Portal (local)")]
        public static void RunPortal() => Start("portal");

        [MenuItem("SpellSimulator/Harness/Run Bots Cast (local)")]
        public static void RunCast() => Start("cast");

        [MenuItem("SpellSimulator/Harness/Run Bots Draw (local)")]
        public static void RunDraw() => Start("draw");

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

        // Escenario de dibujo de F3: dos bots dibujan a la vez en el mismo lienzo, otro deshace, el anfitrion tambien dibuja y un cuarto bot entra tarde.
        private static IEnumerator ProbeDraw()
        {
            var menu = UI.MainMenu.Instance;
            yield return 1f;
            if (UI.MainMenu.IsActive) menu.EnterSandbox();
            yield return 0.5f;
            var session = NetSession.Instance;
            Check(session.StartDirectHost(Port), "el anfitrion abre el puerto local");
            if (!session.IsConnected) yield break;
            yield return 1f;
            NetWorld world = NetWorld.Active;
            Check(world != null && world.Canvases != null && world.Canvases.CanvasCount > 20, "el dibujo compartido esta activo con lienzos (" + (world != null && world.Canvases != null ? world.Canvases.CanvasCount : 0) + ")");
            if (world == null || world.Canvases == null) yield break;

            Directory.CreateDirectory(BotLogDir);
            foreach (string old in Directory.GetFiles(BotLogDir)) { try { File.Delete(old); } catch { } }
            long t0 = NowMs() + 30000;
            for (int i = 1; i <= 4; i++)
            {
                if (i == 4) { while (NowMs() < t0 + 6000) yield return null; } // el cuarto entra tarde, con los trazos ya dibujados
                string name = "Bot" + i;
                string args = "-batchmode -nographics -logFile \"" + Path.Combine(BotLogDir, name + "-unity.log") + "\" -bot -botdirect 127.0.0.1:" + Port + " -botname " + name + " "
                              + "-botscript draw -botseconds 45 -botat " + t0 + " -botlog \"" + Path.Combine(BotLogDir, name + ".txt") + "\"";
                var psi = new ProcessStartInfo(BotBuild.ExePath, args) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = BotBuild.OutputDir };
                processes.Add(Process.Start(psi));
                if (i < 4) yield return 1.5f;
            }

            // el anfitrion dibuja un trazo sobre Paper_M a la vez que los bots
            while (NowMs() < t0 + 500) yield return null;
            DrawingCanvas paper;
            Check(world.Canvases.TryFindByName("Paper_M", out paper), "existe Paper_M");
            var hostStroke = new Stroke();
            if (paper != null)
            {
                paper.Session.AddStroke(hostStroke);
                for (int i = 0; i < 40; i++) { Vector2 p = new Vector2(0.15f + i * 0.0175f, 0.5f); hostStroke.AddPoint(p); paper.DrawAt(p, i == 0, 0f); if (i % 3 == 2) yield return null; }
            }

            // al terminar, todos vuelcan: comparar con lo que ve el anfitrion
            while (NowMs() < t0 + 31000) yield return null;
            string hostView = world.Canvases.Describe();
            float deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline && processes.Any(p => { try { return !p.HasExited; } catch { return false; } })) yield return 0.5f;
            Check(processes.All(p => { try { return p.HasExited; } catch { return true; } }), "los bots terminaron a tiempo");
            notes.Add("anfitrion ve: " + hostView);
            for (int i = 1; i <= 4; i++)
            {
                string report = Path.Combine(BotLogDir, "Bot" + i + ".txt");
                string line = File.Exists(report) ? File.ReadAllLines(report).LastOrDefault(l => l.Contains("CANVAS ")) : null;
                string view = line != null ? line.Substring(line.IndexOf("CANVAS ") + 7).Trim() : "(sin informe)";
                int code = -1; try { code = processes[i - 1].HasExited ? processes[i - 1].ExitCode : -2; } catch { }
                Check(code == 0, "Bot" + i + " termina bien (codigo " + code + ")");
                Check(view == hostView, "Bot" + i + (i == 4 ? " (entro tarde)" : "") + " ve los mismos trazos que el anfitrion (" + view + ")");
            }
            // el contenido esperado: Paper_M tiene los trazos del anfitrion, Bot1 y Bot2; Paper_S uno (Bot3 deshizo el otro)
            Check(hostView.Contains("/") && CountStrokes(hostView, world, "Paper_M") == 3, "Paper_M tiene 3 trazos (anfitrion, Bot1, Bot2)");
            Check(CountStrokes(hostView, world, "Paper_S") == 1, "Paper_S tiene 1 trazo (Bot3 deshizo el segundo)");
        }

        private static int CountStrokes(string view, NetWorld world, string canvasName)
        {
            if (!world.Canvases.TryFindByName(canvasName, out DrawingCanvas canvas) || !world.Canvases.TryGetId(canvas, out ushort id)) return -1;
            foreach (string part in view.Split(';'))
            {
                string[] kv = part.Split(':');
                if (kv.Length != 2 || kv[0] != id.ToString()) continue;
                return int.Parse(kv[1].Split('/')[0]);
            }
            return 0;
        }

        // Escenario de lanzamiento de F3: dos bots cierran un sello (tierra y viento) y el anfitrion ejecuta lo que proponen; un tercero entra tarde.
        private static IEnumerator ProbeCast()
        {
            var menu = UI.MainMenu.Instance;
            yield return 1f;
            if (UI.MainMenu.IsActive) menu.EnterSandbox();
            yield return 0.5f;
            var session = NetSession.Instance;
            Check(session.StartDirectHost(Port), "el anfitrion abre el puerto local");
            if (!session.IsConnected) yield break;
            yield return 1f;
            NetWorld world = NetWorld.Active;
            Check(world != null && world.Casts != null, "el lanzamiento compartido esta activo");
            if (world == null || world.Casts == null) yield break;

            Directory.CreateDirectory(BotLogDir);
            foreach (string old in Directory.GetFiles(BotLogDir)) { try { File.Delete(old); } catch { } }
            long t0 = NowMs() + 30000;
            int windFieldsSeen = 0;
            for (int i = 1; i <= 2; i++)
            {
                LaunchCastBot(i, t0);
                yield return 1.5f;
            }
            bool lateLaunched = false; // el tercero entra tarde, con la roca ya creada

            // mientras los bots lanzan, el anfitrion mira si aparece un campo de viento y una roca; dos cajas esperan quietas (una dentro del chorro de
            // viento de Bot2 y otra lejos, de control) y se sueltan cuando aparece el campo: si el viento del cliente funciona, se mueven distinto
            NetBody crateA = null, crateB = null;
            Vector3 startA = default, startB = default;
            float releasedAt = -1f, maxYA = float.NegativeInfinity, maxYB = float.NegativeInfinity;
            bool prepared = false, measured = false;
            while (NowMs() < t0 + 24000)
            {
                windFieldsSeen = Mathf.Max(windFieldsSeen, CountWindFields());
                if (!lateLaunched && NowMs() >= t0 + 9000) { lateLaunched = true; LaunchCastBot(3, t0); }
                if (!prepared && NowMs() > t0 - 2500)
                {
                    prepared = true;
                    NetPlayer bot2 = Object.FindObjectsByType<NetPlayer>(FindObjectsSortMode.None).FirstOrDefault(pl => !pl.IsOwner && pl.DisplayName == "Bot2");
                    if (bot2 != null && world.Canvases.TryFindByName("Paper_S", out DrawingCanvas windCanvas) && world.TryGetBody("Crate_2", out crateA) && world.TryGetBody("Crate_3", out crateB))
                    {
                        Vector3 eyes = bot2.Head != null ? bot2.Head.position : bot2.transform.position + Vector3.up * 1.6f;
                        Vector3 n = windCanvas.EmissionNormal(eyes);
                        Vector3 c = windCanvas.WorldCenter;
                        HoldStill(crateA, c + n * 1.5f);
                        HoldStill(crateB, c + windCanvas.transform.right * 6f + n * 1.5f);
                        notes.Add("cajas de viento listas: normal " + n.ToString("F2") + ", centro " + c.ToString("F2"));
                    }
                    else { crateA = crateB = null; notes.Add("no se pudieron preparar las cajas de viento"); }
                }
                if (crateA != null && releasedAt < 0f && Output.WindColumn.UpdraftAt(crateA.Rb.position) > 0.3f) // la columna de aire ya esta activa sobre la caja
                {
                    startA = crateA.Rb.position; startB = crateB.Rb.position;
                    crateA.Rb.isKinematic = false; crateB.Rb.isKinematic = false;
                    releasedAt = Time.time;
                }
                if (releasedAt > 0f && !measured && Time.time - releasedAt >= 0.3f)
                {
                    // a los 0,3 s de soltarlas con la columna activa: con gravedad sola las dos llevan la misma velocidad vertical; el viento sube la de la caja alcanzada
                    measured = true;
                    float vyA = crateA.Rb.linearVelocity.y, vyB = crateB.Rb.linearVelocity.y;
                    notes.Add("velocidad vertical a los 0,3 s: con viento " + vyA.ToString("F2") + " m/s, de control " + vyB.ToString("F2") + " m/s");
                    Check(vyA - vyB > 0.8f, "el viento del hechizo del cliente empuja la caja que alcanza (" + vyA.ToString("F2") + " contra " + vyB.ToString("F2") + " m/s de la de control)");
                }
                yield return null;
            }
            if (crateA != null && !measured) failures.Add("el campo de viento nunca aparecio para soltar las cajas");
            string hostCanvases = world.Canvases != null ? world.Canvases.Describe() : "";
            var hostPos = new Dictionary<int, Vector3>();
            int dynamics = 0;
            foreach (var b in world.Bodies)
            {
                if (b == null) continue;
                hostPos[b.Id] = b.Rb.position;
                if (b.IsDynamic) dynamics++;
            }
            yield return 2f;
            float deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline && processes.Any(p => { try { return !p.HasExited; } catch { return false; } })) yield return 0.5f;
            Check(processes.All(p => { try { return p.HasExited; } catch { return true; } }), "los bots terminaron a tiempo");

            Check(world.Casts.ProposalsAccepted >= 2, "el anfitrion acepto las 2 propuestas (" + world.Casts.ProposalsAccepted + ", rechazadas " + world.Casts.ProposalsRejected + ")");
            Check(windFieldsSeen > 0, "el anfitrion creo el campo de viento del hechizo del cliente (" + windFieldsSeen + ")");
            Check(dynamics >= 1, "el anfitrion creo la roca del hechizo de tierra (" + dynamics + " cuerpos nuevos)");

            var inv = System.Globalization.CultureInfo.InvariantCulture;
            for (int i = 1; i <= 3; i++)
            {
                string report = Path.Combine(BotLogDir, "Bot" + i + ".txt");
                string[] l = File.Exists(report) ? File.ReadAllLines(report) : new string[0];
                int code = -1; try { code = processes[i - 1].HasExited ? processes[i - 1].ExitCode : -2; } catch { }
                Check(code == 0, "Bot" + i + " termina bien (codigo " + code + ")");
                string stats = l.LastOrDefault(x => x.Contains("CASTSTATS")) ?? "(sin informe)";
                string canvasLine = l.LastOrDefault(x => x.Contains("CANVAS "));
                string botCanvases = canvasLine != null ? canvasLine.Substring(canvasLine.IndexOf("CANVAS ") + 7).Trim() : "(sin informe)";
                Check(botCanvases == hostCanvases, "Bot" + i + " ve los mismos lienzos dibujados que el anfitrion (" + botCanvases + " contra " + hostCanvases + ")");
                if (i <= 2) Check(l.Any(x => x.Contains("SPELL ")), "Bot" + i + " vio salir su propio hechizo");
                if (i == 1) Check(stats.Contains("efectosRecibidos=1") || stats.Contains("efectosRecibidos=2"), "Bot1 vio el hechizo de viento de Bot2 (" + stats + ")");
                if (i == 2) Check(stats.Contains("efectosRecibidos=1") || stats.Contains("efectosRecibidos=2"), "Bot2 vio el hechizo de tierra de Bot1 (" + stats + ")");
                int total = 0, close = 0, dynSeen = 0; float worst = 0f; string worstId = "";
                foreach (string line in l)
                {
                    if (!line.StartsWith("BODY ")) continue;
                    string[] f = line.Split(' ');
                    if (f.Length < 5 || !int.TryParse(f[1], out int id) || !hostPos.TryGetValue(id, out Vector3 hp)) continue;
                    var bp = new Vector3(float.Parse(f[2], inv), float.Parse(f[3], inv), float.Parse(f[4], inv));
                    float d = Vector3.Distance(hp, bp);
                    total++;
                    if (id >= 40000) dynSeen++;
                    if (d <= 0.05f) close++;
                    if (d > worst) { worst = d; worstId = id.ToString(); }
                }
                Check(dynSeen >= 1, "Bot" + i + (i == 3 ? " (entro tarde)" : "") + " tiene la roca replicada (" + dynSeen + ")");
                Check(total > 50 && close >= total * 0.98f && worst < 0.5f, "Bot" + i + " ve el mundo igual que el anfitrion: " + close + "/" + total + "; peor " + worst.ToString("F2") + " m (cuerpo " + worstId + ")");
            }
            notes.Add("campos de viento vistos por el anfitrion: " + windFieldsSeen);
        }

        private static void LaunchCastBot(int i, long t0)
        {
            string name = "Bot" + i;
            string args = "-batchmode -nographics -logFile \"" + Path.Combine(BotLogDir, name + "-unity.log") + "\" -bot -botdirect 127.0.0.1:" + Port + " -botname " + name + " "
                          + "-botscript cast -botseconds 45 -botat " + t0 + " -botlog \"" + Path.Combine(BotLogDir, name + ".txt") + "\"";
            var psi = new ProcessStartInfo(BotBuild.ExePath, args) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = BotBuild.OutputDir };
            processes.Add(Process.Start(psi));
        }

        private static void HoldStill(NetBody b, Vector3 position)
        {
            b.Rb.isKinematic = true;
            b.Rb.position = position;
            b.transform.position = position;
        }

        private static int CountWindFields()
        {
            int n = 0;
            foreach (var f in Output.MagicField.Active) if (f != null && f.name.Contains("Wind")) n++;
            return n;
        }

        private static void LaunchScriptBot(int i, long t0, string script)
        {
            string name = "Bot" + i;
            string args = "-batchmode -nographics -logFile \"" + Path.Combine(BotLogDir, name + "-unity.log") + "\" -bot -botdirect 127.0.0.1:" + Port + " -botname " + name + " "
                          + "-botscript " + script + " -botseconds 45 -botat " + t0 + " -botlog \"" + Path.Combine(BotLogDir, name + ".txt") + "\"";
            var psi = new ProcessStartInfo(BotBuild.ExePath, args) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = BotBuild.OutputDir };
            processes.Add(Process.Start(psi));
        }

        // Compara dos resumenes "clave=numero ..." con una tolerancia por cifra.
        private static bool ReactiveClose(string a, string b, int tolerance)
        {
            var na = System.Text.RegularExpressions.Regex.Matches(a ?? "", @"=(-?\d+)");
            var nb = System.Text.RegularExpressions.Regex.Matches(b ?? "", @"=(-?\d+)");
            if (na.Count == 0 || na.Count != nb.Count) return false;
            for (int k = 0; k < na.Count; k++)
                if (Mathf.Abs(int.Parse(na[k].Groups[1].Value) - int.Parse(nb[k].Groups[1].Value)) > tolerance) return false;
            return true;
        }

        private static string LastAfter(string[] lines, string marker)
        {
            string line = lines.LastOrDefault(x => x.Contains(marker));
            return line != null ? line.Substring(line.IndexOf(marker) + marker.Length).Trim() : "(sin informe)";
        }

        // Tinta del servidor y re-lectura (F5): un rival tramposo propone fuego sobre un sello de tierra hasta quedarse sin tinta.
        private static IEnumerator ProbeDuelInk()
        {
            var menu = UI.MainMenu.Instance;
            yield return 1f;
            if (UI.MainMenu.IsActive) menu.EnterSandbox();
            yield return 0.5f;
            NetDuel.WantDuel = true;
            var session = NetSession.Instance;
            Check(session.StartDirectHost(Port), "el anfitrion abre el puerto local");
            if (!session.IsConnected) yield break;
            yield return 1f;
            Directory.CreateDirectory(BotLogDir);
            foreach (string old in Directory.GetFiles(BotLogDir)) { try { File.Delete(old); } catch { } }
            LaunchScriptBot(1, 0, "duelink");

            float deadline = Time.realtimeSinceStartup + 40f;
            while (Time.realtimeSinceStartup < deadline && (NetDuel.Active == null || NetDuel.Active.Match == null)) yield return 0.25f;
            Check(NetDuel.Active != null && NetDuel.Active.Match != null, "el duelo empieza al entrar el rival");
            NetWorld world = NetWorld.Active;
            if (world == null || world.Casts == null) yield break;

            deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline && processes.Any(p => { try { return !p.HasExited; } catch { return false; } })) yield return 0.5f;
            NetCast cast = world.Casts;
            notes.Add($"aceptadas={cast.ProposalsAccepted} corregidas={cast.ProposalsCorrected} sinTinta={cast.ProposalsNoInk} rechazadas={cast.ProposalsRejected} ultimoElemento={cast.LastExecutedElement}");
            Check(cast.ProposalsAccepted >= 1, "el anfitrion acepto al menos un hechizo del rival (" + cast.ProposalsAccepted + ")");
            Check(cast.ProposalsCorrected == cast.ProposalsAccepted && cast.ProposalsAccepted >= 1, "todas las propuestas falsas se corrigieron con la lectura del servidor (" + cast.ProposalsCorrected + "/" + cast.ProposalsAccepted + ")");
            Check(cast.LastExecutedElement == SpellSimulator.Compiler.ElementType.Earth, "se ejecuto tierra y no el fuego declarado (" + cast.LastExecutedElement + ")");
            Check(cast.ProposalsNoInk >= 1, "el servidor corto por falta de tinta (" + cast.ProposalsNoInk + " rechazadas)");
            Check(cast.ProposalsAccepted <= 12, "la tinta limito los hechizos (" + cast.ProposalsAccepted + " de 25)");
            string[] l = File.Exists(Path.Combine(BotLogDir, "Bot1.txt")) ? File.ReadAllLines(Path.Combine(BotLogDir, "Bot1.txt")) : new string[0];
            int code = -1; try { code = processes[0].HasExited ? processes[0].ExitCode : -2; } catch { }
            Check(code == 0, "el rival termina bien (codigo " + code + ")");
            notes.Add("rival: " + LastAfter(l, "PROPUESTAS ") + " | " + LastAfter(l, "LECTURA propia "));
        }

        // Escenario del Duelo (F5): el anfitrion hospeda un duelo, un bot entra de rival; se mata a uno por ronda (1-0, 1-1, 2-1) y se comprueba lo que ve el cliente.
        private static IEnumerator ProbeDuel()
        {
            var menu = UI.MainMenu.Instance;
            yield return 1f;
            if (UI.MainMenu.IsActive) menu.EnterSandbox();
            yield return 0.5f;
            NetDuel.WantDuel = true;
            var session = NetSession.Instance;
            Check(session.StartDirectHost(Port), "el anfitrion abre el puerto local");
            if (!session.IsConnected) yield break;
            yield return 1f;

            Directory.CreateDirectory(BotLogDir);
            foreach (string old in Directory.GetFiles(BotLogDir)) { try { File.Delete(old); } catch { } }
            LaunchScriptBot(1, 0, "duel");

            float deadline = Time.realtimeSinceStartup + 40f;
            while (Time.realtimeSinceStartup < deadline && (NetDuel.Active == null || NetDuel.Active.Match == null)) yield return 0.25f;
            NetDuel duel = NetDuel.Active;
            Check(duel != null && duel.Match != null, "el duelo empieza al entrar el rival");
            if (duel == null || duel.Match == null) yield break;
            DuelMatch m = duel.Match;
            Vector3 hostCenter = duel.Center;

            int[] kill = { 1, 0, 1 }; // jugador que cae en cada ronda: gana el anfitrion, gana el rival, gana el anfitrion
            for (int round = 1; round <= 3; round++)
            {
                deadline = Time.realtimeSinceStartup + 30f;
                while (Time.realtimeSinceStartup < deadline && !(m.Phase == DuelPhase.Round && m.RoundNumber == round)) yield return 0.1f;
                Check(m.Phase == DuelPhase.Round && m.RoundNumber == round, "empieza la ronda " + round);
                if (m.Phase != DuelPhase.Round) yield break;
                yield return 1f;
                Health victim = duel.PlayerHealth(kill[round - 1]);
                if (victim != null) victim.Damage(1000f);
                deadline = Time.realtimeSinceStartup + 10f;
                while (Time.realtimeSinceStartup < deadline && m.Phase == DuelPhase.Round) yield return 0.1f;
                Check(m.Phase != DuelPhase.Round && m.RoundWinner == 1 - kill[round - 1], "ronda " + round + ": gana " + (1 - kill[round - 1]) + " (ganador real " + m.RoundWinner + ")");
            }
            deadline = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < deadline && !m.Over) yield return 0.25f;
            Check(m.Over && m.Winner == 0 && m.Wins[0] == 2 && m.Wins[1] == 1, "el duelo termina 2-1 a favor del anfitrion (" + m.Wins[0] + "-" + m.Wins[1] + ", ganador " + m.Winner + ")");

            deadline = Time.realtimeSinceStartup + 40f;
            while (Time.realtimeSinceStartup < deadline && processes.Any(p => { try { return !p.HasExited; } catch { return false; } })) yield return 0.5f;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string report = Path.Combine(BotLogDir, "Bot1.txt");
            string[] l = File.Exists(report) ? File.ReadAllLines(report) : new string[0];
            int code = -1; try { code = processes[0].HasExited ? processes[0].ExitCode : -2; } catch { }
            Check(code == 0, "el rival termina bien (codigo " + code + ")");
            int rounds = l.Count(x => x.Contains("FASE Round "));
            Check(rounds == 3, "el rival vio las 3 rondas (" + rounds + ")");
            Check(l.Any(x => x.Contains("FASE MatchEnd") && x.Contains("ganador=0")), "el rival vio el final con ganador 0");
            Check(l.Any(x => x.Contains("FASE RoundEnd") && x.Contains("victorias=1-1")), "el rival vio el 1-1");
            string minLine = LastAfter(l, "VIDA minima ");
            Check(minLine.Contains("propia=0,00") || minLine.Contains("propia=0.00"), "el rival vio caer su propia vida a 0 (" + minLine + ")");
            Check(minLine.Contains("rival=0,00") || minLine.Contains("rival=0.00"), "el rival vio caer la vida del anfitrion a 0 (" + minLine + ")");
            int near = 0, pos = 0;
            foreach (string line in l)
            {
                if (!line.Contains("POS ")) continue;
                string[] f = line.Substring(line.IndexOf("POS ") + 4).Trim().Split(' ');
                if (f.Length < 2 || !float.TryParse(f[0].Replace(',', '.'), System.Globalization.NumberStyles.Float, inv, out float px) || !float.TryParse(f[1].Replace(',', '.'), System.Globalization.NumberStyles.Float, inv, out float pz)) continue;
                pos++;
                if (Mathf.Abs(px - (hostCenter.x + 16f)) < 3f && Mathf.Abs(pz - hostCenter.z) < 3f) near++;
            }
            Check(pos >= 3 && near == pos, "el rival reaparece en su punto al empezar cada ronda (" + near + "/" + pos + ")");
        }

        // Escenario del mundo reactivo (F4): fuego del pasto, un objeto que arde y se moja, suministros y un papel dibujado; todos tienen que ver lo mismo.
        private static IEnumerator ProbeWorld()
        {
            var menu = UI.MainMenu.Instance;
            yield return 1f;
            if (UI.MainMenu.IsActive) menu.EnterSandbox();
            yield return 0.5f;
            var session = NetSession.Instance;
            Check(session.StartDirectHost(Port), "el anfitrion abre el puerto local");
            if (!session.IsConnected) yield break;
            yield return 1f;
            NetWorld world = NetWorld.Active;
            Check(world != null && world.Reactive != null && world.Reactive.ReactiveCount > 20, "el mundo reactivo compartido esta activo (" + (world != null && world.Reactive != null ? world.Reactive.ReactiveCount : 0) + " objetos)");
            if (world == null || world.Reactive == null) yield break;
            int supplied = 0;
            foreach (var b in world.Bodies) if (b != null && b.IsDynamic) supplied++;

            Directory.CreateDirectory(BotLogDir);
            foreach (string old in Directory.GetFiles(BotLogDir)) { try { File.Delete(old); } catch { } }
            long t0 = NowMs() + 30000;
            for (int i = 1; i <= 2; i++) { LaunchScriptBot(i, t0, "world"); yield return 1.5f; }
            bool lateLaunched = false; // el tercero entra tarde: tiene que recibir el estado completo (suministros, papel dibujado)

            // el anfitrion busca pasto que arda y un objeto que quemar
            GrassFire fire = GrassFire.Current;
            Check(fire != null, "hay GrassFire en la escena");
            Vector3 spot = default;
            bool ignited = false;
            ElementReactive crate = null;
            if (world.TryGetBody("Crate_2", out NetBody crateBody)) crate = crateBody.GetComponent<ElementReactive>();
            Check(crate != null, "Crate_2 tiene ElementReactive");

            while (NowMs() < t0 + 500) yield return null;
            // T0+0,5: se enciende el pasto en el primer lugar donde prenda
            for (int ix = -60; ix <= 60 && !ignited && fire != null; ix += 6)
            {
                for (int iz = -60; iz <= 60 && !ignited; iz += 6)
                {
                    if (!Physics.Raycast(new Vector3(ix, 200f, iz), Vector3.down, out RaycastHit hit, 400f, ~0, QueryTriggerInteraction.Ignore) || hit.collider.name != "Terrain" || hit.normal.y < 0.9f) continue;
                    int before = fire.BurningCells;
                    fire.Ignite(hit.point, 3f);
                    if (fire.BurningCells > before) { ignited = true; spot = hit.point; }
                }
            }
            Check(ignited, "el anfitrion enciende pasto (" + spot.ToString("F0") + ")");
            // el objeto empieza a arder
            float until = Time.realtimeSinceStartup + 1.5f;
            while (crate != null && Time.realtimeSinceStartup < until) { crate.AddExposure(new Exposure { Heat = 120f }, 1f); yield return null; }
            Check(crate != null && crate.State.Burning, "Crate_2 arde en el anfitrion");

            while (NowMs() < t0 + 3600)
            {
                if (!lateLaunched && NowMs() >= t0 + 500) { lateLaunched = true; LaunchScriptBot(3, t0, "world"); }
                yield return null;
            }
            string hostFire = world.Reactive.Describe();
            notes.Add("anfitrion (fuego): " + hostFire);

            // T0+5: agua sobre el pasto (todo lo que arde) y sobre el objeto
            while (NowMs() < t0 + 5000) yield return null;
            if (fire != null) fire.ExtinguishAt(spot, 60f);
            until = Time.realtimeSinceStartup + 1.5f;
            while (crate != null && Time.realtimeSinceStartup < until) { crate.AddExposure(new Exposure { Heat = -150f, Wet = 6f }, 1f); yield return null; }

            while (NowMs() < t0 + 10500) yield return null;
            string hostFinal = world.Reactive.Describe();
            string hostCanvases = world.Canvases.Describe();
            var hostPos = new Dictionary<int, Vector3>();
            foreach (var b in world.Bodies) if (b != null) hostPos[b.Id] = b.Rb.position;
            notes.Add("anfitrion (final): " + hostFinal);
            supplied = 0;
            foreach (var b in world.Bodies) if (b != null && b.IsDynamic) supplied++;
            Check(supplied > 0, "el anfitrion repuso suministros que se replican (" + supplied + " cuerpos nuevos)");
            Check(fire != null && fire.BurntCells > 0, "el fuego dejo pasto quemado en el anfitrion (" + (fire != null ? fire.BurntCells : -1) + " celdas)");

            float deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline && processes.Any(p => { try { return !p.HasExited; } catch { return false; } })) yield return 0.5f;
            Check(processes.All(p => { try { return p.HasExited; } catch { return true; } }), "los bots terminaron a tiempo");

            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string firstFinal = null;
            for (int i = 1; i <= 3; i++)
            {
                string report = Path.Combine(BotLogDir, "Bot" + i + ".txt");
                string[] l = File.Exists(report) ? File.ReadAllLines(report) : new string[0];
                int code = -1; try { code = processes[i - 1].HasExited ? processes[i - 1].ExitCode : -2; } catch { }
                Check(code == 0, "Bot" + i + " termina bien (codigo " + code + ")");
                string fireSeen = LastAfter(l, "REACT(fuego) ");
                if (i <= 2) Check(!fireSeen.Contains("pasto ardiendo=0") && !fireSeen.Contains("pasto ardiendo=-1"), "Bot" + i + " ve el pasto ardiendo (" + fireSeen + ")");
                string finalSeen = LastAfter(l, "REACT(final) ");
                if (i == 1) firstFinal = finalSeen;
                // el fuego sigue evolucionando entre la muestra del anfitrion y la de los bots: los bots tienen que coincidir entre si y andar cerca del anfitrion
                Check(finalSeen == firstFinal && ReactiveClose(finalSeen, hostFinal, 6), "Bot" + i + (i == 3 ? " (entro tarde)" : "") + " ve el mundo reactivo final (" + finalSeen + ") igual que los demas bots y cerca del anfitrion (" + hostFinal + ")");
                string canvasSeen = LastAfter(l, "CANVAS ");
                Check(canvasSeen == hostCanvases, "Bot" + i + " ve los mismos lienzos (incluido el papel de suministro dibujado): " + canvasSeen + " contra " + hostCanvases);
                int total = 0, close = 0; float worst = 0f;
                foreach (string line in l)
                {
                    if (!line.StartsWith("BODY ")) continue;
                    string[] f = line.Split(' ');
                    if (f.Length < 5 || !int.TryParse(f[1], out int id) || !hostPos.TryGetValue(id, out Vector3 hp)) continue;
                    var bp = new Vector3(float.Parse(f[2], inv), float.Parse(f[3], inv), float.Parse(f[4], inv));
                    float d = Vector3.Distance(hp, bp);
                    total++;
                    if (d <= 0.05f) close++;
                    worst = Mathf.Max(worst, d);
                }
                Check(total > 50 && close >= total * 0.98f && worst < 0.5f, "Bot" + i + " ve los cuerpos (suministros incluidos) igual que el anfitrion: " + close + "/" + total + "; peor " + worst.ToString("F2") + " m");
            }
        }

        // Escenario de portales (F4): un bot abre una ruta dibujando la identidad; dos bots cruzan casi a la vez y el enfriamiento de uno no afecta al otro.
        private static IEnumerator ProbePortal()
        {
            var menu = UI.MainMenu.Instance;
            yield return 1f;
            if (UI.MainMenu.IsActive) menu.EnterSandbox();
            yield return 0.5f;
            var session = NetSession.Instance;
            Check(session.StartDirectHost(Port), "el anfitrion abre el puerto local");
            if (!session.IsConnected) yield break;
            yield return 1f;
            NetWorld world = NetWorld.Active;
            Portal origin = Portal.Find("tower-study"), dest = Portal.Find("watchtower");
            Check(world != null && origin != null && dest != null, "estan los portales tower-study y watchtower");
            if (world == null || origin == null || dest == null) yield break;

            Directory.CreateDirectory(BotLogDir);
            foreach (string old in Directory.GetFiles(BotLogDir)) { try { File.Delete(old); } catch { } }
            long t0 = NowMs() + 30000;
            for (int i = 1; i <= 3; i++) { LaunchScriptBot(i, t0, "portal"); if (i < 3) yield return 1.5f; }

            bool routeSeen = false, openSeen = false;
            while (NowMs() < t0 + 11000)
            {
                if (origin.Destination == dest) routeSeen = true;
                if (origin.State == PortalState.Open && dest.State == PortalState.Open) openSeen = true;
                yield return null;
            }
            Check(routeSeen, "el anfitrion leyo la identidad dibujada por un cliente y abrio la ruta tower-study -> watchtower");
            Check(openSeen, "los dos portales llegaron a estar abiertos");

            float deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline && processes.Any(p => { try { return !p.HasExited; } catch { return false; } })) yield return 0.5f;
            Check(processes.All(p => { try { return p.HasExited; } catch { return true; } }), "los bots terminaron a tiempo");
            Check(world.PortalCrossingsApproved >= 2, "el anfitrion autorizo el cruce de los dos clientes (aprobados " + world.PortalCrossingsApproved + ", rechazados " + world.PortalCrossingsDenied + ")");
            for (int i = 1; i <= 3; i++)
            {
                string[] l = File.Exists(Path.Combine(BotLogDir, "Bot" + i + ".txt")) ? File.ReadAllLines(Path.Combine(BotLogDir, "Bot" + i + ".txt")) : new string[0];
                int code = -1; try { code = processes[i - 1].HasExited ? processes[i - 1].ExitCode : -2; } catch { }
                Check(code == 0, "Bot" + i + " termina bien (codigo " + code + ")");
                string state = LastAfter(l, "PORTAL estado ");
                Check(state.Contains("destino=watchtower"), "Bot" + i + " ve la ruta abierta (" + state + ")");
                if (i >= 2)
                {
                    string cross = LastAfter(l, "PORTAL cruces=");
                    Check(cross.StartsWith("1"), "Bot" + i + " cruzo el portal (" + cross + ")");
                    int at = cross.IndexOf("distanciaALaLlegada=");
                    float far = at >= 0 && float.TryParse(cross.Substring(at + 20).Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : 999f;
                    Check(far >= 0f && far < 4f, "Bot" + i + " llego al portal de destino (a " + far.ToString("F1") + " m)");
                }
            }
        }

        private static IEnumerator Probe(string mode)
        {
            if (!File.Exists(BotBuild.ExePath)) { failures.Add("falta el ejecutable de los bots (menu SpellSimulator/Build/Windows (bots))"); yield break; }
            if (mode == "duelink") { var inkProbe = ProbeDuelInk(); while (inkProbe.MoveNext()) yield return inkProbe.Current; yield break; }
            if (mode == "duel") { var duelProbe = ProbeDuel(); while (duelProbe.MoveNext()) yield return duelProbe.Current; yield break; }
            if (mode == "world") { var worldProbe = ProbeWorld(); while (worldProbe.MoveNext()) yield return worldProbe.Current; yield break; }
            if (mode == "portal") { var portalProbe = ProbePortal(); while (portalProbe.MoveNext()) yield return portalProbe.Current; yield break; }
            if (mode == "cast") { var cast = ProbeCast(); while (cast.MoveNext()) yield return cast.Current; yield break; }
            if (mode == "draw") { var draw = ProbeDraw(); while (draw.MoveNext()) yield return draw.Current; yield break; }
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
