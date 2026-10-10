using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Combat;
using SpellSimulator.Core;
using SpellSimulator.Player;
using SpellSimulator.World;

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
                else if (options.Script == BotScript.Draw) yield return DrawScenario();
                else if (options.Script == BotScript.Cast) yield return CastScenario();
                else if (options.Script == BotScript.World) yield return WorldScenario();
                else if (options.Script == BotScript.Portal) yield return PortalScenario();
                else if (options.Script == BotScript.Duel) yield return DuelScenario();
                else if (options.Script == BotScript.DuelInk) yield return DuelInkScenario();
                else if (options.Script == BotScript.DuelX) yield return DuelXScenario();
                else if (options.Script == BotScript.Fx) yield return FxScenario();
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

        // ---------------------------------------------------------------- guion World (F4)

        /// <summary>
        /// Observador del mundo reactivo: Bot1 dibuja un trazo en un papel de suministro (cuerpo creado en juego con su lienzo); el anfitrion enciende pasto
        /// y un objeto (+3.5 s: se cuenta lo que se ve), los apaga con agua (+10 s: se cuenta de nuevo). Cada bot vuelca lo que ve.
        /// </summary>
        private IEnumerator WorldScenario()
        {
            NetWorld world = NetWorld.Active;
            if (world == null || world.Reactive == null || world.Canvases == null) { failures.Add("sin NetWorld/mundo reactivo"); yield break; }
            long t0 = options.StartAtMs > 0 ? options.StartAtMs : NowMs() + 3000;
            int idx = BotOptions.IndexOfName(options.Name);
            yield return WaitUntilMs(t0);
            if (idx == 1)
            {
                // un papel de suministro (cuerpo dinamico con lienzo): se dibuja un trazo en el
                foreach (var body in world.Bodies)
                {
                    if (body != null && body.IsDynamic && body.TryGetComponent(out DrawingCanvas dc) && dc.isActiveAndEnabled)
                    {
                        var stroke = new Stroke();
                        dc.Session.AddStroke(stroke);
                        for (int i = 0; i < 30; i++) { Vector2 p = new Vector2(0.2f + i * 0.02f, 0.5f); stroke.AddPoint(p); }
                        dc.Redraw();
                        Note($"dibuja un trazo en el papel de suministro {body.name} (id {body.Id})");
                        break;
                    }
                }
            }
            yield return WaitUntilMs(t0 + 3500);
            Note("REACT(fuego) " + world.Reactive.Describe());
            yield return WaitUntilMs(t0 + 10000);
            Note("REACT(final) " + world.Reactive.Describe());
            Note("CANVAS " + world.Canvases.Describe());
            DumpBodies(world);
            yield return new WaitForSecondsRealtime(0.5f);
        }

        // ---------------------------------------------------------------- guion Fx (efectos del agua)

        /// <summary>A T0+14 s anota cuantos charcos, zonas inundadas y placas de hielo ve (el anfitrion los crea a T0+0,5 y congela la zona a T0+4).</summary>
        private IEnumerator FxScenario()
        {
            NetWorld world = NetWorld.Active;
            if (world == null || world.Effects == null) { failures.Add("sin NetWorld/efectos"); yield break; }
            long t0 = options.StartAtMs > 0 ? options.StartAtMs : NowMs();
            yield return WaitUntilMs(t0 + 14000);
            Note("FX " + world.Effects.Describe());
            yield return new WaitForSecondsRealtime(0.5f);
        }

        // ---------------------------------------------------------------- guion Duel (F5)

        /// <summary>Rival del duelo: espera el estado del anfitrion y anota cada cambio de fase, la vida que ve y donde reaparece.</summary>
        private IEnumerator DuelScenario()
        {
            long t0 = options.StartAtMs > 0 ? options.StartAtMs : NowMs();
            float limit = Time.realtimeSinceStartup + 80f;
            DuelPhase lastPhase = (DuelPhase)255;
            int lastRound = -1;
            float minOwn = 1f, minRival = 1f;
            int maxBroken = 0, restored = 0, prevBroken = 0;
            bool roleNoted = false;
            while (Time.realtimeSinceStartup < limit)
            {
                NetDuel duel = NetDuel.Active;
                DuelMatch m = duel != null ? duel.Match : null;
                if (m != null)
                {
                    if (!roleNoted)
                    {
                        roleNoted = true;
                        yield return new WaitForSecondsRealtime(1.5f); // que lo teletransporten
                        Transform rt = PlayerRegistry.Local != null ? PlayerRegistry.Local.Root : null;
                        Note($"ROL espectador={duel.IsSpectator}" + (rt != null ? $" pos={rt.position.x.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} {rt.position.y.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} {rt.position.z.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}" : ""));
                    }
                    int broken = 0;
                    foreach (var cover in DuelArena.Covers) if (cover != null && cover.Broken) broken++;
                    if (broken > maxBroken) maxBroken = broken;
                    if (prevBroken > 0 && broken == 0) restored++;
                    prevBroken = broken;
                    int me = duel.LocalIndex;
                    minOwn = Mathf.Min(minOwn, duel.Life(me)); minRival = Mathf.Min(minRival, duel.Life(1 - me));
                    if (m.Phase != lastPhase || m.RoundNumber != lastRound)
                    {
                        lastPhase = m.Phase; lastRound = m.RoundNumber;
                        Note($"FASE {m.Phase} ronda={m.RoundNumber} victorias={m.Wins[0]}-{m.Wins[1]} ganadorRonda={m.RoundWinner} ganador={m.Winner}");
                        if (m.Phase == DuelPhase.Countdown) { yield return new WaitForSecondsRealtime(1f); Transform root = PlayerRegistry.Local != null ? PlayerRegistry.Local.Root : null; if (root != null) Note($"POS {root.position.x.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} {root.position.z.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}"); }
                    }
                    if (m.Over) { yield return new WaitForSecondsRealtime(1f); break; }
                }
                yield return null;
            }
            Note($"VIDA minima propia={minOwn.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)} rival={minRival.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}");
            Note($"COBERTURAS max={maxBroken} restauradas={restored}");
            yield return new WaitForSecondsRealtime(0.5f);
        }

        /// <summary>
        /// Rival del duelo con un sello COMPUESTO (dos circulos interiores dentro de uno exterior): propone el lanzamiento 12 veces seguidas, de golpe.
        /// El anfitrion tiene que cobrar la tinta una sola vez, ejecutar los DOS interiores con su propia lectura y cortar el exceso de propuestas por
        /// frecuencia. Despues sigue como un rival normal (mira las fases y las coberturas).
        /// </summary>
        private IEnumerator DuelXScenario()
        {
            NetWorld world = NetWorld.Active;
            SpellCaster spellCaster = FindFirstObjectByType<SpellCaster>();
            if (world == null || world.Canvases == null || spellCaster == null || spellCaster.Glyphs == null || world.Casts == null) { failures.Add("sin NetWorld/lienzos/diccionario"); yield break; }
            float limit = Time.realtimeSinceStartup + 40f;
            while (Time.realtimeSinceStartup < limit && !(NetDuel.Active != null && NetDuel.Active.Match != null && NetDuel.Active.Match.Phase == DuelPhase.Round)) yield return null;
            yield return new WaitForSecondsRealtime(1f);
            DrawingCanvas paper = null;
            float best = float.MaxValue;
            Transform me = PlayerRegistry.Local != null ? PlayerRegistry.Local.Root : null;
            foreach (var body in world.Bodies)
            {
                if (body == null || !body.IsDynamic || !body.TryGetComponent(out DrawingCanvas dc) || me == null) continue;
                float d = Vector3.Distance(dc.transform.position, me.position);
                if (d < best) { best = d; paper = dc; }
            }
            if (paper == null) { failures.Add("no hay papeles en la arena"); yield break; }
            DrawNested(paper, spellCaster.Glyphs, "earth", "earth");
            yield return new WaitForSecondsRealtime(1.5f);
            var spells = new List<Compiler.SpellIR>();
            var centers = new List<Vector2>();
            bool read = spellCaster.CompileAll(paper, spells, centers);
            Note($"COMPUESTO leido={read} interiores={spells.Count}");
            if (!read) { failures.Add("el sello compuesto no se leyo"); yield break; }
            for (int k = 0; k < 12; k++) world.Casts.ProposeForTest(spells[0], paper, centers[0]);
            Note("RAFAGA enviada=12");
            yield return new WaitForSecondsRealtime(1f);
            yield return DuelScenario();
        }

        /// <summary>Sello compuesto: un anillo exterior grande con dos sellos chicos adentro (a izquierda y derecha).</summary>
        private static void DrawNested(DrawingCanvas canvas, Glyphs.GlyphDictionary dictionary, string sigilA, string sigilB)
        {
            canvas.Clear();
            const float innerRadius = 0.17f;
            for (int k = 0; k < 2; k++)
            {
                var c = new Vector2(k == 0 ? 0.29f : 0.71f, 0.5f);
                AddTemplate(canvas.Session, dictionary, k == 0 ? sigilA : sigilB, c, 0.24f * innerRadius / 0.42f, 0f);
                var inner = new Stroke();
                for (int i = 0; i <= 48; i++) inner.AddPoint(c + GeometryUtils.VectorFromAngleDeg(i / 48f * 360f) * innerRadius);
                canvas.Session.AddStroke(inner);
            }
            var outer = new Stroke();
            for (int i = 0; i <= 96; i++) outer.AddPoint(new Vector2(0.5f, 0.5f) + GeometryUtils.VectorFromAngleDeg(i / 96f * 360f) * 0.48f);
            canvas.Session.AddStroke(outer);
            canvas.Redraw();
        }

        /// <summary>
        /// Rival tramposo del duelo: dibuja un sello de TIERRA y, en plena ronda, le propone al anfitrion un hechizo que dice ser de FUEGO, una y otra vez
        /// hasta que se le acabe la tinta del servidor. El anfitrion tiene que ejecutar siempre la lectura propia (tierra) y cortar cuando no hay tinta.
        /// </summary>
        private IEnumerator DuelInkScenario()
        {
            NetWorld world = NetWorld.Active;
            SpellCaster spellCaster = FindFirstObjectByType<SpellCaster>();
            if (world == null || world.Canvases == null || spellCaster == null || spellCaster.Glyphs == null || world.Casts == null) { failures.Add("sin NetWorld/lienzos/diccionario"); yield break; }
            float limit = Time.realtimeSinceStartup + 40f;
            while (Time.realtimeSinceStartup < limit && !(NetDuel.Active != null && NetDuel.Active.Match != null && NetDuel.Active.Match.Phase == DuelPhase.Round)) yield return null;
            yield return new WaitForSecondsRealtime(1f); // que lleguen los papeles de la arena
            DrawingCanvas paper = null;
            float best = float.MaxValue;
            Transform me = PlayerRegistry.Local != null ? PlayerRegistry.Local.Root : null;
            foreach (var body in world.Bodies)
            {
                if (body == null || !body.IsDynamic || !body.TryGetComponent(out DrawingCanvas dc) || me == null) continue;
                float d = Vector3.Distance(dc.transform.position, me.position);
                if (d < best) { best = d; paper = dc; }
            }
            if (paper == null) { failures.Add("no hay papeles en la arena"); yield break; }
            Note($"PAPEL {paper.name} a {best.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} m");
            DrawSeal(paper, spellCaster.Glyphs, "earth");
            yield return new WaitForSecondsRealtime(1.5f); // que los trazos lleguen al anfitrion
            if (!spellCaster.Compile(paper, out Compiler.SpellIR real, out Vector2 uv)) { failures.Add("el bot no pudo leer su propio sello"); yield break; }
            Note($"LECTURA propia {real.element} {real.sigilId}");
            Compiler.SpellIR forged = real;
            forged.element = Compiler.ElementType.Fire;
            int sent = 0;
            while (sent < 25 && NetDuel.Active != null && NetDuel.Active.Match != null && NetDuel.Active.Match.Phase == DuelPhase.Round)
            {
                world.Casts.ProposeForTest(forged, paper, uv);
                sent++;
                yield return new WaitForSecondsRealtime(0.7f); // mas lento que la ventana que ignora repetidos del mismo sello (0,6 s)
            }
            Note($"PROPUESTAS enviadas={sent} tintaVista={(NetDuel.Active != null ? NetDuel.Active.InkFraction(NetDuel.Active.LocalIndex).ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "?")}");
            yield return new WaitForSecondsRealtime(1f);
        }

        // ---------------------------------------------------------------- guion Portal (F4)

        private int travelsSeen;

        private void TeleportTo(Vector3 position)
        {
            IPlayerContext me = PlayerRegistry.Local;
            if (me == null || me.Root == null) return;
            if (me.Root.TryGetComponent(out Rigidbody prb)) { prb.position = position; prb.linearVelocity = Vector3.zero; }
            me.Root.position = position;
            Physics.SyncTransforms();
        }

        /// <summary>
        /// +0 Bot1 dibuja la identidad de 'watchtower' en la placa de 'tower-study' (el anfitrion la lee y abre la ruta); +6 Bot2 y Bot3 entran al cruce
        /// de 'tower-study' casi a la vez: el anfitrion los autoriza a los dos (el enfriamiento es por jugador) y llegan a 'watchtower'.
        /// </summary>
        private IEnumerator PortalScenario()
        {
            NetWorld world = NetWorld.Active;
            SpellCaster spellCaster = FindFirstObjectByType<SpellCaster>();
            Portal origin = Portal.Find("tower-study"), dest = Portal.Find("watchtower");
            if (world == null || spellCaster == null || spellCaster.Glyphs == null || origin == null || dest == null || origin.Plate == null || origin.CrossTrigger == null)
            { failures.Add("faltan NetWorld/diccionario/portales tower-study y watchtower"); yield break; }
            long t0 = options.StartAtMs > 0 ? options.StartAtMs : NowMs() + 3000;
            int idx = BotOptions.IndexOfName(options.Name);
            Portal.Traveled += OnTraveled;
            yield return WaitUntilMs(t0);

            if (idx == 1)
            {
                // cerca de la placa (el anfitrion mide el alcance con el avatar), un instante para que llegue, y se dibuja la identidad
                Vector3 plate = origin.Plate.WorldCenter;
                TeleportTo(plate + Vector3.back * 2.5f);
                yield return new WaitForSecondsRealtime(0.8f);
                var identity = dest.Identity;
                var signs = new (string id, float angleDeg)[identity.Signs.Length];
                for (int i = 0; i < signs.Length; i++) signs[i] = (identity.Signs[i], 90f + i * 360f / signs.Length);
                DrawSeal(origin.Plate, spellCaster.Glyphs, identity.Sigil, signs);
                spellCaster.OnStrokeFinished(origin.Plate);
                Note("dibuja la identidad de watchtower en la placa de tower-study");
            }
            else if (idx == 2 || idx == 3)
            {
                yield return WaitUntilMs(t0 + 6000 + (idx - 2) * 200);
                Vector3 gate = origin.CrossTrigger.bounds.center;
                TeleportTo(gate + Vector3.back * 3f);
                yield return new WaitForSecondsRealtime(0.6f); // que el anfitrion vea el avatar cerca
                TeleportTo(gate);
                yield return new WaitForSecondsRealtime(2f);
                IPlayerContext me = PlayerRegistry.Local;
                float far = me != null && me.Root != null && dest.Arrival != null ? Vector3.Distance(me.Root.position, dest.Arrival.position) : -1f;
                Note($"PORTAL cruces={travelsSeen} distanciaALaLlegada={far:F1}");
            }
            yield return WaitUntilMs(t0 + 12000);
            Note($"PORTAL estado tower-study={origin.State} destino={origin.DestinationId} watchtower={dest.State}");
            Portal.Traveled -= OnTraveled;
            yield return new WaitForSecondsRealtime(0.5f);
        }

        private void OnTraveled(Portal from, Portal to) { travelsSeen++; Note($"cruzo {from.Id} -> {to.Id}"); }

        // ---------------------------------------------------------------- guion Cast (F3)

        private int spellsSeen;

        /// <summary>
        /// +0 Bot1 dibuja un sello de TIERRA (roca) en Paper_M y Bot2 uno de VIENTO en Paper_S: cierran el anillo, esperan la carga y lanzan. El anfitrion
        /// ejecuta lo que proponen (la roca se crea alla y se replica); cada bot cuenta los hechizos que vio. Bot3 entra tarde y debe ver la roca.
        /// +25 todos vuelcan los cuerpos (incluye la roca) y los efectos recibidos.
        /// </summary>
        private IEnumerator CastScenario()
        {
            NetWorld world = NetWorld.Active;
            SpellCaster spellCaster = FindFirstObjectByType<SpellCaster>();
            if (world == null || world.Canvases == null || spellCaster == null || spellCaster.Glyphs == null) { failures.Add("sin NetWorld/lienzos/diccionario"); yield break; }
            long t0 = options.StartAtMs > 0 ? options.StartAtMs : NowMs() + 3000;
            int idx = BotOptions.IndexOfName(options.Name);
            spellCaster.SpellCompiled += OnSpellSeen;
            yield return WaitUntilMs(t0);
            if (idx == 1 && world.Canvases.TryFindByName("Paper_M", out DrawingCanvas a))
            {
                DrawSeal(a, spellCaster.Glyphs, "earth");
                spellCaster.OnStrokeFinished(a);
                Note("cierra el sello de tierra en Paper_M");
            }
            else if (idx == 2 && world.Canvases.TryFindByName("Paper_S", out DrawingCanvas b))
            {
                DrawSeal(b, spellCaster.Glyphs, "wind-directs-air", ("column", 270f), ("column", 90f));
                spellCaster.OnStrokeFinished(b);
                Note("cierra el sello de viento en Paper_S");
            }
            yield return WaitUntilMs(t0 + 25000);
            DumpBodies(world);
            Note("CANVAS " + world.Canvases.Describe());
            NetCast cast = world.Casts;
            Note($"CASTSTATS vistos={spellsSeen} aceptadas={(cast != null ? cast.ProposalsAccepted : -1)} efectosRecibidos={(cast != null ? cast.EffectsReceived : -1)}");
            spellCaster.SpellCompiled -= OnSpellSeen;
            yield return new WaitForSecondsRealtime(0.5f);
        }

        private void OnSpellSeen(Compiler.SpellIR spell, DrawingCanvas canvas)
        {
            if (!spell.valid || !spell.active) return;
            spellsSeen++;
            Note($"SPELL {spell.element} {spell.sigilId} en {(canvas != null ? canvas.name : "?")}");
        }

        /// <summary>Dibuja un sello de plantilla (sigilo + signos + anillo cerrado al final) como lo hace HarnessSeals en el editor.</summary>
        private static void DrawSeal(DrawingCanvas canvas, Glyphs.GlyphDictionary dictionary, string sigil, params (string id, float angleDeg)[] signs)
        {
            var center = new Vector2(0.5f, 0.5f);
            canvas.Clear();
            AddTemplate(canvas.Session, dictionary, sigil, center, 0.24f, 0f);
            foreach (var (id, angle) in signs)
            {
                Vector2 at = center + GeometryUtils.VectorFromAngleDeg(angle) * 0.3f;
                AddTemplate(canvas.Session, dictionary, id, at, 0.12f, angle - 270f);
            }
            var ring = new Stroke();
            for (int i = 0; i <= 64; i++) ring.AddPoint(center + GeometryUtils.VectorFromAngleDeg(i / 64f * 360f) * 0.42f);
            canvas.Session.AddStroke(ring);
            canvas.Redraw();
        }

        private static void AddTemplate(DrawingSession session, Glyphs.GlyphDictionary dictionary, string id, Vector2 center, float size, float rotationDeg)
        {
            foreach (var points in dictionary.Get(id).TemplateStrokes)
            {
                var stroke = new Stroke();
                foreach (var p in points)
                {
                    Vector2 q = GeometryUtils.RotatePointAroundPivot(p, new Vector2(0.5f, 0.5f), rotationDeg);
                    stroke.AddPoint((q - new Vector2(0.5f, 0.5f)) * size + center);
                }
                session.AddStroke(stroke);
            }
        }

        // ---------------------------------------------------------------- guion Draw (F3)

        /// <summary>
        /// +0 Bot1 y Bot2 dibujan a la vez sobre Paper_M (el anfitrion tambien); Bot3 dibuja dos trazos en Paper_S y deshace el ultimo; Bot4 entra
        /// tarde y no dibuja. +30 todos vuelcan cuantos trazos y puntos ve cada lienzo con dibujo y el editor compara.
        /// </summary>
        private IEnumerator DrawScenario()
        {
            NetWorld world = NetWorld.Active;
            if (world == null || world.Canvases == null) { failures.Add("sin NetWorld/lienzos"); yield break; }
            long t0 = options.StartAtMs > 0 ? options.StartAtMs : NowMs() + 3000;
            int idx = BotOptions.IndexOfName(options.Name);
            Note($"lienzos compartidos: {world.Canvases.CanvasCount}");
            yield return WaitUntilMs(t0);
            if (idx == 1 && world.Canvases.TryFindByName("Paper_M", out DrawingCanvas a))
                yield return DrawPolyline(a, new Vector2(0.15f, 0.2f), new Vector2(0.85f, 0.8f), 40);
            else if (idx == 2 && world.Canvases.TryFindByName("Paper_M", out DrawingCanvas b))
                yield return DrawPolyline(b, new Vector2(0.15f, 0.8f), new Vector2(0.85f, 0.2f), 40);
            else if (idx == 3 && world.Canvases.TryFindByName("Paper_S", out DrawingCanvas c))
            {
                yield return DrawPolyline(c, new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.5f), 30);
                yield return DrawPolyline(c, new Vector2(0.5f, 0.1f), new Vector2(0.5f, 0.9f), 30);
                yield return new WaitForSecondsRealtime(1f);
                c.Session.UndoLastStroke();
                c.Redraw();
                Note("deshace el ultimo trazo de Paper_S");
            }
            yield return WaitUntilMs(t0 + 30000);
            Note("CANVAS " + world.Canvases.Describe());
            yield return new WaitForSecondsRealtime(0.5f);
        }

        /// <summary>Dibuja un trazo punto a punto como lo hace el lapiz (agrega el trazo vacio y le va sumando puntos a lo largo de varios cuadros).</summary>
        private IEnumerator DrawPolyline(DrawingCanvas canvas, Vector2 from, Vector2 to, int points)
        {
            var stroke = new Stroke();
            canvas.Session.AddStroke(stroke);
            for (int i = 0; i < points; i++)
            {
                Vector2 p = Vector2.Lerp(from, to, i / (float)(points - 1));
                stroke.AddPoint(p);
                canvas.DrawAt(p, i == 0, 0f);
                if (i % 3 == 2) yield return null;
            }
            Note($"dibuja trazo {stroke.OwnerId}/{stroke.StrokeId} en {canvas.name} ({points} puntos)");
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
