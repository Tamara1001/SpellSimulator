using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Combat;
using SpellSimulator.Core;

namespace SpellSimulator.Net
{
    /// <summary>
    /// Duelo 1 contra 1 en red (F5 de MULTIPLAYER_PLAN). Solo el anfitrion corre las reglas (<see cref="DuelMatch"/>) y aplica el daño: el anfitrion es el
    /// jugador 0 y el primer cliente el 1. Los clientes reciben el estado (fase, ronda, victorias, vidas; mensaje "du.state") y la orden de reaparecer
    /// ("du.tp"). Arena: <see cref="DuelArena"/> (plataforma en el cielo; se arma en cada maquina al empezar y se desarma al terminar, y cada uno vuelve a donde estaba). Fuera de la fase
    /// de ronda nadie pierde vida. Si el rival se va, gana el que queda.
    /// </summary>
    public sealed class NetDuel
    {
        private const string MState = "du.state", MTp = "du.tp";
        private const float SpawnHalfGap = 16f, StateInterval = 0.1f, EndLinger = 8f;

        /// <summary>El menu lo pone en verdad antes de hospedar: la sala es un Duelo y arranca cuando entra el rival.</summary>
        public static bool WantDuel;
        public static NetDuel Active { get; private set; }

        private readonly NetworkManager nm;
        private DuelMatch match;
        private readonly Health[] health = new Health[2];
        private readonly float[] life = { 1f, 1f };
        private readonly float[] inkShown = { 1f, 1f };
        private InkLedger[] ledger;
        private readonly List<GameObject> papers = new List<GameObject>(8);
        private Vector3 center;
        private bool started, finished;
        private ulong rivalId;
        private ushort coverMask;
        private float sendTimer, lingerTimer;
        private static bool homeSaved;
        private static Vector3 homePos;
        private static float homeYaw;

        public NetDuel(NetworkManager manager) { nm = manager; }

        public bool InDuel => match != null && !finished;
        public DuelMatch Match => match;
        public float Life(int player) => life[Mathf.Clamp(player, 0, 1)];
        public float InkFraction(int player) => inkShown[Mathf.Clamp(player, 0, 1)];
        /// <summary>Jugador (0 anfitrion, 1 rival) al que pertenece un cliente de red.</summary>
        public static int PlayerIndexOf(ulong clientId) => Active != null ? Active.IndexOf(clientId) : (clientId == NetworkManager.ServerClientId ? 0 : 1);
        /// <summary>0 = anfitrion, 1 = el rival, -1 = espectador (cualquier otro cliente que entra con el duelo en marcha).</summary>
        public int IndexOf(ulong clientId) => clientId == NetworkManager.ServerClientId ? 0 : (clientId == rivalId ? 1 : -1);
        /// <summary>True si esta maquina mira el duelo sin jugarlo.</summary>
        public bool IsSpectator => match != null && PlayerRegistry.LocalOwnerId != 0 && (ulong)PlayerRegistry.LocalOwnerId != rivalId;
        public bool CastsAllowed => match != null && match.Phase == DuelPhase.Round;
        public Health PlayerHealth(int player) => health[Mathf.Clamp(player, 0, 1)];
        public Vector3 Center => center;
        public int LocalIndex => PlayerRegistry.LocalOwnerId == 0 ? 0 : 1;

        public void Begin()
        {
            Active = this;
            var cm = nm.CustomMessagingManager;
            cm.RegisterNamedMessageHandler(MState, OnState);
            cm.RegisterNamedMessageHandler(MTp, OnTeleport);
        }

        public void End()
        {
            if (Active == this) Active = null;
            if (nm != null && nm.CustomMessagingManager != null)
            {
                nm.CustomMessagingManager.UnregisterNamedMessageHandler(MState);
                nm.CustomMessagingManager.UnregisterNamedMessageHandler(MTp);
            }
            for (int i = 0; i < 2; i++) if (health[i] != null) Object.Destroy(health[i]);
            match = null;
            ClearPapers();
            LeaveArena();
        }

        // ---------------------------------------------------------------- anfitrion

        public void Tick(float dt)
        {
            if (nm == null) return;
            if (!nm.IsServer)
            {
                if (match != null && match.Over)
                {
                    lingerTimer += dt;
                    if (lingerTimer > EndLinger) { match = null; LeaveArena(); }
                }
                return;
            }
            if (!WantDuel) return;
            if (finished)
            {
                lingerTimer += dt;
                if (lingerTimer > EndLinger) { WantDuel = false; match = null; ClearPapers(); LeaveArena(); }
                return;
            }
            if (!started) { TryStart(); return; }

            if (!ConnectedRival(out _)) { if (!match.Over) match.Forfeit(1); }
            DuelArena.HostTick();
            coverMask = DuelArena.BrokenMask();
            for (int i = 0; i < 2; i++)
            {
                if (health[i] == null) continue;
                if (match.Phase != DuelPhase.Round) health[i].ResetHealth(); // nadie pierde vida fuera de la ronda
                life[i] = health[i].Fraction;
            }
            for (int i = 0; i < 2; i++) { if (match.Phase == DuelPhase.Round) ledger[i].Tick(dt); inkShown[i] = ledger[i].Fraction; }
            match.Tick(dt, life[0], life[1]);
            if (match.Over) { finished = true; lingerTimer = 0f; }

            sendTimer += dt;
            if (sendTimer >= StateInterval || finished) { sendTimer = 0f; BroadcastState(); }
        }

        private bool ConnectedRival(out ulong clientId)
        {
            clientId = rivalId;
            if (started)
            {
                foreach (ulong id in nm.ConnectedClientsIds) if (id == rivalId) return PlayerRegistry.ById((int)id) != null;
                return false;
            }
            foreach (ulong id in nm.ConnectedClientsIds)
            {
                if (id == NetworkManager.ServerClientId) continue;
                clientId = id;
                return PlayerRegistry.ById((int)id) != null;
            }
            return false;
        }

        private void TryStart()
        {
            if (!ConnectedRival(out ulong rival)) return;
            IPlayerContext host = PlayerRegistry.Local, other = PlayerRegistry.ById((int)rival);
            if (host == null || host.Root == null || other == null || other.Root == null) return;
            rivalId = rival;
            health[0] = HealthOf(host.Root);
            health[1] = HealthOf(other.Root);
            center = DuelArena.Center;
            DuelArena.Build();
            match = new DuelMatch(DuelRules.Default);
            ledger = new[] { new InkLedger(match.Rules.inkMax, match.Rules.inkRegenPerSecond), new InkLedger(match.Rules.inkMax, match.Rules.inkRegenPerSecond) };
            match.PhaseChanged += OnPhase;
            started = true;
            Debug.Log("[Duelo] empieza el duelo: anfitrion contra cliente " + rival);
            OnPhase(DuelPhase.Countdown);
        }

        private static Health HealthOf(Transform root)
        {
            Health h = root.GetComponent<Health>();
            if (h == null) h = root.gameObject.AddComponent<Health>();
            h.ResetHealth();
            return h;
        }

        private void OnPhase(DuelPhase phase)
        {
            if (nm == null || !nm.IsServer) return;
            if (phase == DuelPhase.Countdown)
            {
                for (int i = 0; i < 2; i++) { if (health[i] != null) health[i].ResetHealth(); if (ledger != null) ledger[i].Refill(); }
                DuelArena.RestoreAll();
                Respawn(0);
                Respawn(1);
                SpawnPapers();
            }
            BroadcastState();
        }

        private void ClearPapers()
        {
            for (int i = papers.Count - 1; i >= 0; i--) if (papers[i] != null) Object.Destroy(papers[i]);
            papers.Clear();
        }

        /// <summary>Papeles limpios en cada lado de la arena (no hay mesas ahi): se renuevan en cada ronda y los clientes los ven como clones replicados.</summary>
        private void SpawnPapers()
        {
            ClearPapers();
            NetWorld world = NetWorld.Active;
            GameObject template = SpellSimulator.World.SupplyPoint.FindTemplate("PaperTemplate");
            if (world == null || template == null) return;
            for (int side = 0; side < 2; side++)
            {
                Vector3 s = SpawnPoint(side, out float yaw);
                float inward = side == 0 ? 1.5f : -1.5f;
                for (int k = -1; k <= 1; k++)
                {
                    Vector3 pos = s + new Vector3(inward, 0.6f + 0.15f * (k + 1), k * 1.1f);
                    GameObject paper = Object.Instantiate(template, pos, template.transform.rotation);
                    paper.name = template.name;
                    paper.transform.localScale = template.transform.lossyScale;
                    paper.SetActive(true);
                    world.RegisterClone(paper, template, 0);
                    papers.Add(paper);
                }
            }
        }

        /// <summary>Punto de reaparicion del jugador (pegado al suelo), mirando al otro.</summary>
        private Vector3 SpawnPoint(int player, out float yaw)
        {
            float side = player == 0 ? -1f : 1f;
            Vector3 p = center + new Vector3(side * SpawnHalfGap, 0f, 0f);
            if (Physics.Raycast(p + Vector3.up * 60f, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore)) p = hit.point;
            yaw = side < 0f ? 90f : -90f;
            return p + Vector3.up * 0.1f;
        }

        private void Respawn(int player)
        {
            Vector3 p = SpawnPoint(player, out float yaw);
            if (player == 0) { TeleportLocal(p, yaw); return; }
            if (!ConnectedRival(out ulong rival)) return;
            using (var w = new FastBufferWriter(32, Allocator.Temp))
            {
                w.WriteValueSafe(p.x); w.WriteValueSafe(p.y); w.WriteValueSafe(p.z); w.WriteValueSafe(yaw);
                nm.CustomMessagingManager.SendNamedMessage(MTp, rival, w, NetworkDelivery.Reliable);
            }
        }

        private void BroadcastState()
        {
            if (match == null) return;
            var targets = new List<ulong>(2);
            foreach (ulong id in nm.ConnectedClientsIds) if (id != NetworkManager.ServerClientId) targets.Add(id);
            if (targets.Count == 0) return;
            using (var w = new FastBufferWriter(64, Allocator.Temp))
            {
                w.WriteValueSafe((byte)match.Phase); w.WriteValueSafe((byte)match.RoundNumber); w.WriteValueSafe(match.PhaseTime);
                w.WriteValueSafe((byte)match.Wins[0]); w.WriteValueSafe((byte)match.Wins[1]);
                w.WriteValueSafe((sbyte)match.RoundWinner); w.WriteValueSafe((sbyte)match.Winner);
                w.WriteValueSafe(life[0]); w.WriteValueSafe(life[1]);
                w.WriteValueSafe(inkShown[0]); w.WriteValueSafe(inkShown[1]);
                w.WriteValueSafe(coverMask); w.WriteValueSafe((ushort)rivalId);
                nm.CustomMessagingManager.SendNamedMessage(MState, targets, w, NetworkDelivery.Unreliable);
            }
        }

        /// <summary>
        /// Anfitrion: cobra de la tinta del jugador lo que pesan los trazos del lienzo. False si el duelo no esta en ronda o no alcanza
        /// (el hechizo no se ejecuta). Fuera de un duelo siempre es true.
        /// </summary>
        public bool TryCharge(int player, DrawingCanvas canvas, out float cost)
        {
            cost = 0f;
            if (!InDuel || !started || ledger == null) return true;
            if (!CastsAllowed || canvas == null) return false;
            cost = DuelInk.CanvasCost(canvas.Session.Strokes, canvas.WorldSize, match.Rules.inkPerMeter);
            return ledger[Mathf.Clamp(player, 0, 1)].TryCharge(cost);
        }

        // ---------------------------------------------------------------- cliente

        private void OnState(ulong sender, FastBufferReader r)
        {
            if (nm.IsServer) return;
            r.ReadValueSafe(out byte phase); r.ReadValueSafe(out byte round); r.ReadValueSafe(out float phaseTime);
            r.ReadValueSafe(out byte w0); r.ReadValueSafe(out byte w1);
            r.ReadValueSafe(out sbyte rw); r.ReadValueSafe(out sbyte win);
            r.ReadValueSafe(out float l0); r.ReadValueSafe(out float l1);
            r.ReadValueSafe(out float i0); r.ReadValueSafe(out float i1);
            r.ReadValueSafe(out ushort covers); r.ReadValueSafe(out ushort rival);
            if (phase > (byte)DuelPhase.MatchEnd) return;
            if (match == null) { match = new DuelMatch(DuelRules.Default); finished = false; }
            match.Apply((DuelPhase)phase, round, phaseTime, w0, w1, rw, win);
            life[0] = Mathf.Clamp01(l0); life[1] = Mathf.Clamp01(l1);
            inkShown[0] = Mathf.Clamp01(i0); inkShown[1] = Mathf.Clamp01(i1);
            rivalId = rival;
            if (DuelArena.Exists) DuelArena.ApplyMask(covers);
            finished = match.Over;
        }

        /// <summary>Anfitrion: alguien entro. Si el duelo ya esta en marcha y no es el rival, mira desde la tribuna.</summary>
        public void OnClientJoined(ulong clientId)
        {
            if (nm == null || !nm.IsServer || !started || finished || clientId == NetworkManager.ServerClientId || clientId == rivalId) return;
            Vector3 p = DuelArena.StandPoint;
            using (var w = new FastBufferWriter(32, Allocator.Temp))
            {
                w.WriteValueSafe(p.x); w.WriteValueSafe(p.y); w.WriteValueSafe(p.z); w.WriteValueSafe(0f);
                nm.CustomMessagingManager.SendNamedMessage(MTp, clientId, w, NetworkDelivery.Reliable);
            }
            BroadcastState();
        }

        private void OnTeleport(ulong sender, FastBufferReader r)
        {
            if (nm.IsServer) return;
            r.ReadValueSafe(out float x); r.ReadValueSafe(out float y); r.ReadValueSafe(out float z); r.ReadValueSafe(out float yaw);
            if (float.IsNaN(x + y + z + yaw)) return;
            DuelArena.Build(); // el rival arma la misma arena antes de caer en ella
            TeleportLocal(new Vector3(x, y, z), yaw);
        }

        /// <summary>Mueve al jugador local (cuerpo fisico incluido) y lo orienta.</summary>
        public static void TeleportLocal(Vector3 position, float yaw)
        {
            IPlayerContext local = PlayerRegistry.Local;
            if (local == null || local.Root == null) return;
            if (!homeSaved) { homeSaved = true; homePos = local.Root.position; homeYaw = local.Root.eulerAngles.y; }
            Move(local.Root, position, yaw);
        }

        /// <summary>Fin del duelo: se desarma la arena y cada uno vuelve a donde estaba.</summary>
        public static void LeaveArena()
        {
            if (homeSaved && PlayerRegistry.Local != null && PlayerRegistry.Local.Root != null && DuelArena.Exists)
                Move(PlayerRegistry.Local.Root, homePos, homeYaw);
            homeSaved = false;
            DuelArena.Destroy();
        }

        private static void Move(Transform root, Vector3 position, float yaw)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            Rigidbody body = root.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = position;
                body.rotation = rotation;
            }
            root.SetPositionAndRotation(position, rotation);
            Physics.SyncTransforms();
        }

        // ---------------------------------------------------------------- HUD (IMGUI, como NetHud)

        private static GUIStyle small, big;

        public void DrawHud()
        {
            if (match == null || (nm != null && nm.IsServer && !started)) return;
            if (small == null)
            {
                small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 18, fontStyle = FontStyle.Bold };
                big = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 54 };
            }
            bool spectator = IsSpectator;
            int me = spectator ? 0 : LocalIndex, rival = 1 - me;
            float w = Screen.width;
            string head = "Ronda " + match.RoundNumber + "   ·   " + (spectator ? "Jugador 1 " : "Vos ") + match.Wins[me] + " - " + match.Wins[rival] + (spectator ? " Jugador 2" : " Rival") +
                          (match.Phase == DuelPhase.Round ? "   ·   " + Mathf.CeilToInt(match.SecondsLeft) + " s" : "");
            Label(new Rect(w * 0.5f - 250f, 6f, 500f, 28f), head, small);
            Bar(new Rect(w * 0.5f - 250f, 36f, 240f, 14f), life[me], new Color(0.3f, 0.85f, 0.4f));
            Bar(new Rect(w * 0.5f + 10f, 36f, 240f, 14f), life[rival], new Color(0.9f, 0.35f, 0.3f));
            if (!spectator) Bar(new Rect(w * 0.5f - 250f, 54f, 240f, 8f), inkShown[me], new Color(0.35f, 0.55f, 1f));

            string center = null;
            switch (match.Phase)
            {
                case DuelPhase.Countdown: center = Mathf.CeilToInt(match.SecondsLeft).ToString(); break;
                case DuelPhase.RoundEnd:
                    center = match.RoundWinner < 0 ? "Empate" : (spectator ? "Ronda del jugador " + (match.RoundWinner + 1) : (match.RoundWinner == me ? "¡Ronda tuya!" : "Ronda del rival"));
                    break;
                case DuelPhase.MatchEnd:
                    center = match.Winner < 0 ? "Empate" : (spectator ? "Gana el jugador " + (match.Winner + 1) : (match.Winner == me ? "¡Ganaste el duelo!" : "Perdiste el duelo"));
                    break;
            }
            if (center != null) Label(new Rect(0f, Screen.height * 0.3f, w, 80f), center, big);
        }

        private static void Label(Rect rect, string text, GUIStyle style)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
            GUI.color = Color.white;
            GUI.Label(rect, text, style);
        }

        private static void Bar(Rect rect, float fraction, Color color)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * Mathf.Clamp01(fraction), rect.height - 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
