using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using SpellSimulator.Core;
using SpellSimulator.Player;

namespace SpellSimulator.Net.Bots
{
    /// <summary>
    /// Jugador automatico para probar la red sin gente (F1 de MULTIPLAYER_PLAN). Solo existe si el ejecutable se lanza con "-bot":
    /// se une a la sala por codigo de Relay o por IP directa, entra al juego, camina y salta con un gamepad virtual (la misma cadena de
    /// entrada que una persona), anota lo que ve de los demas jugadores y al terminar escribe un informe y sale con codigo 0 (bien) o 1 (mal).
    /// Guion "churn": entra y sale de la sala varias veces. Opciones en <see cref="BotOptions"/>.
    /// </summary>
    public sealed class NetBot : MonoBehaviour
    {
        private const float ReportEvery = 2f;
        private const float MovedMeters = 0.75f;

        private BotOptions options;
        private Gamepad pad;
        private readonly StringBuilder log = new StringBuilder(2048);
        private readonly Dictionary<int, Vector3> lastSeen = new Dictionary<int, Vector3>(8);
        private readonly Dictionary<int, float> traveled = new Dictionary<int, float>(8);
        private readonly List<string> failures = new List<string>(8);
        private int errorLogs, exceptionLogs, maxRemotes, joinsDone;
        private float startedAt;
        private Vector3 selfLast;
        private bool selfLastSet;
        private float selfTraveled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            BotOptions o = BotOptions.Parse(Environment.GetCommandLineArgs());
            if (!o.Enabled) return;
            var go = new GameObject("NetBot");
            DontDestroyOnLoad(go);
            go.AddComponent<NetBot>().options = o;
        }

        private void Start()
        {
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
            startedAt = Time.realtimeSinceStartup;
            Note($"bot '{options.Name}' guion={options.Script} destino={(string.IsNullOrEmpty(options.JoinCode) ? options.DirectIp + ":" + options.DirectPort : options.JoinCode)} segundos={options.Seconds} espera={options.ExpectRemotes} remotos");
            if (!options.HasTarget) { failures.Add("sin destino (-botjoin o -botdirect)"); Finish(); return; }
            StartCoroutine(Run());
        }

        private void OnDestroy() => Application.logMessageReceived -= OnLog;

        private void OnLog(string condition, string stack, LogType type)
        {
            if (type == LogType.Exception) { exceptionLogs++; if (exceptionLogs <= 5) Note("EXCEPCION: " + condition); }
            else if (type == LogType.Error) { errorLogs++; if (errorLogs <= 5) Note("ERROR: " + condition); }
        }

        private void Note(string text)
        {
            string line = $"[{Time.realtimeSinceStartup - startedAt,6:F1}s] {text}";
            log.AppendLine(line);
            Debug.Log("[Bot] " + line);
        }

        // ---------------------------------------------------------------- guion

        private IEnumerator Run()
        {
            var settings = InputSystem.settings;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            pad = InputSystem.AddDevice<Gamepad>("NetBotPad");
            NetSession.PlayerName = options.Name;
            NetSession.AuthProfile = SanitizeProfile(options.Name);
            NetSession.SimulatedLatencyMs = options.LatencyMs;

            // espera al menu principal (se arma en Start)
            float wait = 0f;
            while (UI.MainMenu.Instance == null && wait < 15f) { wait += Time.unscaledDeltaTime; yield return null; }
            if (UI.MainMenu.Instance == null) { failures.Add("no aparecio el MainMenu"); Finish(); yield break; }
            yield return new WaitForSecondsRealtime(1f);

            int cycles = options.Script == BotScript.Churn ? options.Cycles : 1;
            for (int c = 1; c <= cycles; c++)
            {
                Note($"ciclo {c}/{cycles}: conectando");
                yield return Connect();
                if (!NetSession.Instance.IsConnected) { failures.Add($"ciclo {c}: no se pudo conectar ({NetSession.Instance.LastError})"); break; }
                joinsDone++;
                if (UI.MainMenu.IsActive) UI.MainMenu.Instance.EnterSandbox();
                yield return new WaitForSecondsRealtime(0.5f);
                if (options.Script == BotScript.Grab) yield return GrabScenario();
                else yield return Play(options.Seconds, c == cycles);
                if (c < cycles)
                {
                    Note($"ciclo {c}: saliendo de la sala");
                    NetSession.Instance.Leave();
                    yield return new WaitForSecondsRealtime(3f);
                    if (NetSession.Instance.State != NetState.Offline) failures.Add($"ciclo {c}: no volvio a Offline ({NetSession.Instance.State})");
                    lastSeen.Clear();
                }
            }
            Finish();
        }

        private IEnumerator Connect()
        {
            var s = NetSession.Instance;
            if (!string.IsNullOrEmpty(options.JoinCode))
            {
                var task = s.JoinAsync(options.JoinCode);
                float limit = Time.realtimeSinceStartup + 45f;
                while (!task.IsCompleted && Time.realtimeSinceStartup < limit) yield return null;
            }
            else
            {
                s.StartDirectClient(options.DirectIp, options.DirectPort);
                float limit = Time.realtimeSinceStartup + 15f;
                while (Time.realtimeSinceStartup < limit && !(Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsConnectedClient)) yield return null;
            }
        }

        /// <summary>Camina con rumbos al azar, mira alrededor y salta de vez en cuando; informa cada pocos segundos.</summary>
        private IEnumerator Play(float seconds, bool lastCycle)
        {
            float end = Time.realtimeSinceStartup + seconds;
            float nextTurn = 0f, nextReport = 0f, nextJump = Time.realtimeSinceStartup + UnityEngine.Random.Range(3f, 6f);
            float turn = 0f;
            while (Time.realtimeSinceStartup < end)
            {
                float now = Time.realtimeSinceStartup;
                if (!NetSession.Instance.IsConnected)
                {
                    failures.Add("se perdio la conexion jugando");
                    break;
                }
                if (now >= nextTurn)
                {
                    turn = UnityEngine.Random.value < 0.35f ? 0f : UnityEngine.Random.Range(-1f, 1f);
                    nextTurn = now + UnityEngine.Random.Range(0.6f, 2.2f);
                }
                bool jump = now >= nextJump;
                if (jump) nextJump = now + UnityEngine.Random.Range(3f, 7f);
                var state = new GamepadState { leftStick = new Vector2(0f, 0.9f), rightStick = new Vector2(turn, 0f) };
                if (jump) state = state.WithButton(GamepadButton.South);
                InputSystem.QueueStateEvent(pad, state);
                // sin ventana el cursor no se captura y el jugador no rota con la vista: el bot gira el cuerpo directamente
                if (!PlatformDetector.IsLookCaptured)
                {
                    IPlayerContext me = PlayerRegistry.Local;
                    if (me != null && me.Root != null) me.Root.Rotate(0f, turn * 110f * Time.deltaTime, 0f);
                }
                if (now >= nextReport) { nextReport = now + ReportEvery; Report(); }
                yield return null;
            }
            InputSystem.QueueStateEvent(pad, new GamepadState());
            Report();
        }

        private void Report()
        {
            int remotes = 0;
            var sb = new StringBuilder(160);
            IPlayerContext local = PlayerRegistry.Local;
            sb.Append("yo=").Append(local != null && local.Root != null ? local.Root.position.ToString("F1") : "?");
            if (local != null && local.Root != null)
            {
                Vector3 me = local.Root.position;
                if (selfLastSet) selfTraveled += Flat(me - selfLast);
                selfLast = me; selfLastSet = true;
            }
            var players = PlayerRegistry.Players;
            for (int i = 0; i < players.Count; i++)
            {
                IPlayerContext p = players[i];
                if (p == null || !p.IsAlive || p.IsLocal || p.Root == null) continue;
                remotes++;
                int id = p.OwnerId;
                Vector3 pos = p.Root.position;
                if (lastSeen.TryGetValue(id, out Vector3 prev))
                {
                    traveled.TryGetValue(id, out float t);
                    traveled[id] = t + Flat(pos - prev);
                }
                lastSeen[id] = pos;
                sb.Append(" | #").Append(id).Append('=').Append(pos.ToString("F1"));
            }
            if (remotes > maxRemotes) maxRemotes = remotes;
            Note($"remotos={remotes} {sb}");
        }

        // ---------------------------------------------------------------- guion Grab (F2)

        private PlayerInteractor interactor;
        private static int Me => PlayerRegistry.LocalOwnerId;

        private static long NowMs() => (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

        private static IEnumerator WaitUntilMs(long ms) { while (NowMs() < ms) yield return null; }

        /// <summary>
        /// Linea de tiempo (T0 = -botat; todos los bots y el anfitrion la comparten):
        /// +0 todos piden Crate_0 a la vez (debe quedar uno solo) | +4 el ganador lo suelta | +6 Bot1 agarra Crate_1, lo lleva y lo arroja |
        /// +14 Bot2 lo agarra y lo deja flotando | +15 Bot3 intenta agarrarlo (rechazado) | +19 Bot3 lo agarra (estaba flotando) |
        /// +9 el anfitrion empuja 30 cajas | +24 cada maquina vuelca las posiciones de todos los cuerpos.
        /// </summary>
        private IEnumerator GrabScenario()
        {
            interactor = FindFirstObjectByType<PlayerInteractor>();
            NetWorld world = NetWorld.Active;
            if (interactor == null || world == null || !world.Began) { failures.Add("sin interactor o sin NetWorld"); yield break; }
            long t0 = options.StartAtMs > 0 ? options.StartAtMs : NowMs() + 3000;
            int idx = BotOptions.IndexOfName(options.Name);
            Note($"mundo compartido: {world.BodyCount} cuerpos; arranca en {(t0 - NowMs()) / 1000f:F1} s");
            yield return WaitUntilMs(t0);

            // A: pelea por la misma caja
            yield return GrabNamed("Crate_0"); bool asked = grabResult;
            yield return new WaitForSecondsRealtime(1.5f);
            bool won = asked && HoldingNamed("Crate_0");
            Note($"GRAB Crate_0 {(won ? "WIN" : "LOSE")} (pedido={asked})");
            yield return WaitUntilMs(t0 + 4000);
            if (won) { interactor.Release(Me, false); Note("suelta Crate_0"); }

            if (idx == 1)
            {
                yield return WaitUntilMs(t0 + 6000);
                yield return GrabNamed("Crate_1"); bool ok = grabResult;
                Note($"GRAB Crate_1 {(ok ? "OK" : "DENIED")}");
                if (ok) { yield return CarryFor(3.5f); interactor.Throw(Me, ForwardOfPlayer() * 6f + Vector3.up * 2f); Note("arroja Crate_1"); }
            }
            else if (idx == 2)
            {
                yield return WaitUntilMs(t0 + 14000);
                yield return GrabNamed("Crate_1"); bool ok = grabResult;
                Note($"GRAB Crate_1 {(ok ? "OK" : "DENIED")}");
                if (ok) { yield return CarryFor(2.5f); interactor.Release(Me, true); Note("deja Crate_1 flotando"); }
            }
            else if (idx == 3)
            {
                yield return WaitUntilMs(t0 + 15000);
                yield return GrabNamed("Crate_1"); bool early = grabResult;
                Note($"GRAB Crate_1 (ocupado) {(early ? "OK" : "DENIED")}");
                if (early) interactor.Release(Me, false);
                yield return WaitUntilMs(t0 + 19000);
                yield return GrabNamed("Crate_1"); bool ok = grabResult;
                Note($"GRAB Crate_1 (flotando) {(ok ? "OK" : "DENIED")}");
                if (ok) { yield return CarryFor(2.5f); interactor.Release(Me, false); Note("suelta Crate_1"); }
            }

            yield return WaitUntilMs(t0 + 24000);
            DumpBodies(world);
            yield return new WaitForSecondsRealtime(1f);
        }

        private static Vector3 ForwardOfPlayer()
        {
            IPlayerContext me = PlayerRegistry.Local;
            Vector3 f = me != null && me.Head != null ? me.Head.forward : Vector3.forward;
            f.y = 0f;
            return f.sqrMagnitude < 1e-4f ? Vector3.forward : f.normalized;
        }

        private bool HoldingNamed(string name)
            => NetWorld.Active != null && NetWorld.Active.TryGetBody(name, out NetBody b) && b.HeldBy == Me;

        private bool grabResult;

        /// <summary>
        /// Se para delante de la caja, espera un instante a que el servidor vea el avatar en su sitio (el servidor mide el alcance con el
        /// avatar; un teletransporte, que solo hacen los bots, llega con retraso) y la pide como lo haria el interactor (TryGrab).
        /// El resultado queda en <see cref="grabResult"/> (false si se rechazo al instante).
        /// </summary>
        private IEnumerator GrabNamed(string name)
        {
            grabResult = false;
            NetWorld world = NetWorld.Active;
            if (world == null) { failures.Add("NetWorld ya no esta activo al pedir " + name); yield break; }
            if (!world.TryGetBody(name, out NetBody b) || b.Rb == null) { failures.Add("no existe " + name); yield break; }
            IPlayerContext me = PlayerRegistry.Local;
            if (me == null || me.Root == null) yield break;
            Vector3 target = b.Rb.position;
            Vector3 dir = ForwardOfPlayer();
            Vector3 flat = target - me.Root.position; flat.y = 0f;
            if (flat.sqrMagnitude > 1e-3f) dir = flat.normalized;
            if (me.Root.TryGetComponent(out Rigidbody prb))
            {
                prb.position = target - dir * 2f + Vector3.up * 0.6f;
                prb.linearVelocity = Vector3.zero;
            }
            me.Root.rotation = Quaternion.LookRotation(dir, Vector3.up);
            Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(0.4f + options.LatencyMs * 2f / 1000f);
            if (b == null || b.Rb == null) yield break;
            grabResult = interactor.TryGrab(new GrabRequest(Me, b.gameObject, 2f));
        }

        /// <summary>Camina hacia adelante (gamepad virtual) mientras lleva lo agarrado.</summary>
        private IEnumerator CarryFor(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(0f, 0.9f) });
                yield return null;
            }
            InputSystem.QueueStateEvent(pad, new GamepadState());
        }

        /// <summary>Una linea por cuerpo con su posicion final: el editor las compara con las del anfitrion.</summary>
        private void DumpBodies(NetWorld world)
        {
            var sb = new StringBuilder(world.BodyCount * 40);
            var list = world.Bodies;
            for (int i = 0; i < list.Count; i++)
            {
                NetBody b = list[i];
                if (b == null) continue;
                Vector3 p = b.Rb.position;
                sb.Append("BODY ").Append(b.Id).Append(' ').Append(p.x.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).Append(' ')
                  .Append(p.y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).Append(' ')
                  .Append(p.z.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
            }
            log.Append(sb);
            Note($"cuerpos volcados: {list.Count}");
        }

        private static string SanitizeProfile(string name)
        {
            var sb = new StringBuilder(name.Length);
            foreach (char ch in name) if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_') sb.Append(ch);
            if (sb.Length == 0) sb.Append("bot");
            return sb.Length > 30 ? sb.ToString(0, 30) : sb.ToString();
        }

        private static float Flat(Vector3 d) { d.y = 0f; return d.magnitude; }

        // ---------------------------------------------------------------- final

        private void Finish()
        {
            if (options.ExpectRemotes > 0)
            {
                if (maxRemotes < options.ExpectRemotes) failures.Add($"vio como mucho {maxRemotes} remotos (esperaba {options.ExpectRemotes})");
                int movers = 0;
                foreach (var kv in traveled) if (kv.Value >= MovedMeters) movers++;
                if (movers < Math.Min(options.ExpectRemotes, 1)) failures.Add("ningun remoto se movio mas de " + MovedMeters + " m");
            }
            if (options.Script == BotScript.Walk && selfTraveled < 4f) failures.Add($"el bot casi no camino ({selfTraveled:F1} m)");
            if (exceptionLogs > 0) failures.Add(exceptionLogs + " excepciones");
            bool pass = failures.Count == 0;
            string summary = (pass ? "PASS" : "FAIL " + string.Join(" | ", failures))
                             + $" (uniones={joinsDone}, recorrido={selfTraveled:F1} m, remotos_max={maxRemotes}, errores={errorLogs}, excepciones={exceptionLogs})";
            Note("RESULT " + summary);
            WriteLog();
            if (NetSession.Exists) NetSession.Instance.Leave();
            Application.Quit(pass ? 0 : 1);
        }

        private void WriteLog()
        {
            try
            {
                string path = options.LogPath;
                if (string.IsNullOrEmpty(path)) path = Path.Combine(Application.persistentDataPath, "bot-" + options.Name + ".txt");
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                File.WriteAllText(path, log.ToString());
            }
            catch (Exception e) { Debug.LogWarning("[Bot] no pude escribir el informe: " + e.Message); }
        }
    }
}
