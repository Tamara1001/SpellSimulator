using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Combat;
using SpellSimulator.Casting;
using SpellSimulator.Compiler;
using SpellSimulator.Core;
using SpellSimulator.Output;

namespace SpellSimulator.Net
{
    /// <summary>
    /// Lanzamiento compartido (F3 de MULTIPLAYER_PLAN, 2.4-2.5; modo coop: «el cliente propone y el servidor dispone»).
    /// Quien cierra el anillo lo lee con el modelo en su maquina (brillo, carga y anotaciones al instante) y, al lanzar, el hechizo compilado
    /// (<see cref="SpellIRCodec"/>) viaja al servidor, que lo valida (lienzo, alcance, frecuencia) y lo ejecuta: ese es el efecto con cuerpos y
    /// fuerzas. Los demas clientes reciben el mismo hechizo y lo muestran. En el cliente que lanzo, el efecto local es solo visual (sin
    /// cuerpos propios: los crea el servidor). Los disparadores (runas, placas) solo los decide el servidor.
    /// </summary>
    public sealed class NetCast
    {
        private const string MPropose = "sc.cast", MEffect = "sc.fx";
        private const float MaxReach = 15f, DedupeWindow = 0.6f;

        /// <summary>Sala cooperativa con tinta limitada (la elige el anfitrion en el menu): el servidor cobra la tinta de cada hechizo como en el duelo.</summary>
        public static bool CoopInk;

        /// <summary>Instancia de la sesion actual (null sin red).</summary>
        public static NetCast Current { get; private set; }

        /// <summary>True mientras se reproduce un hechizo que vino de la red (no se vuelve a proponer ni a reenviar).</summary>
        public static bool ApplyingRemote { get; private set; }

        /// <summary>True si esta maquina es un cliente (no el anfitrion) de una sesion: sus hechizos son solo visuales y no dispara runas.</summary>
        public static bool IsClientOnly => Current != null && !Current.isServer;

        private readonly NetworkManager nm;
        private readonly NetCanvasSync canvases;
        private readonly bool isServer;
        private readonly Dictionary<ulong, InkLedger> coopInk = new Dictionary<ulong, InkLedger>(4);
        private readonly Dictionary<ulong, float> coopInkAt = new Dictionary<ulong, float>(4);
        private readonly Dictionary<int, float> chargedAt = new Dictionary<int, float>(8); // lienzo -> ultimo cobro (un sello compuesto propone varias veces)
        private readonly List<SpellIR> ownSpells = new List<SpellIR>(4);
        private readonly List<Vector2> ownCenters = new List<Vector2>(4);
        private SpellOutputController output;
        private SpellCaster caster;

        public int ProposalsAccepted { get; private set; }
        public int ProposalsRejected { get; private set; }
        public int EffectsReceived { get; private set; }
        /// <summary>Propuestas de un duelo cuyo hechizo el servidor leyo distinto y ejecuto con su propia lectura.</summary>
        public int ProposalsCorrected { get; private set; }
        /// <summary>Propuestas rechazadas por falta de tinta o por no estar en ronda (duelo).</summary>
        public int ProposalsNoInk { get; private set; }
        public ElementType LastExecutedElement { get; private set; }
        /// <summary>Hechizos ejecutados por la lectura propia del servidor en el duelo (un sello compuesto cuenta uno por interior).</summary>
        public int DuelSpellsExecuted { get; private set; }

        /// <summary>Anfitrion: el cobro de tinta de su propio lanzamiento (duelo, o coop con tinta limitada). True = el hechizo no se ejecuta.</summary>
        public static bool DeniedForHost(DrawingCanvas canvas)
        {
            NetCast c = Current;
            if (c == null || ApplyingRemote || !c.isServer || canvas == null) return false;
            bool duel = NetDuel.Active != null && NetDuel.Active.InDuel;
            if (!duel && !CoopInk) return false;
            if (c.ChargedRecently(canvas)) return false; // otro interior del mismo sello compuesto: ya se cobro
            bool ok = duel ? NetDuel.Active.TryCharge(0, canvas, out _) : c.ChargeCoop(NetworkManager.ServerClientId, canvas);
            if (ok) { c.MarkCharged(canvas); return false; }
            c.ProposalsNoInk++;
            return true;
        }

        private bool ChargedRecently(DrawingCanvas canvas) => chargedAt.TryGetValue(canvas.GetInstanceID(), out float t) && Time.unscaledTime - t < DedupeWindow;
        private void MarkCharged(DrawingCanvas canvas) => chargedAt[canvas.GetInstanceID()] = Time.unscaledTime;

        /// <summary>Coop con tinta limitada: cada jugador tiene su saldo (mismas reglas que el duelo), que se recarga solo.</summary>
        private bool ChargeCoop(ulong who, DrawingCanvas canvas)
        {
            DuelRules rules = DuelRules.Default;
            float now = Time.unscaledTime;
            if (!coopInk.TryGetValue(who, out InkLedger ledger)) { ledger = new InkLedger(rules.inkMax, rules.inkRegenPerSecond); coopInk[who] = ledger; coopInkAt[who] = now; }
            ledger.Tick(now - coopInkAt[who]);
            coopInkAt[who] = now;
            return ledger.TryCharge(DuelInk.CanvasCost(canvas.Session.Strokes, canvas.WorldSize, rules.inkPerMeter));
        }

        /// <summary>Solo harness: propone al servidor un hechizo ya armado (para probar propuestas falsas).</summary>
        public void ProposeForTest(SpellIR spell, DrawingCanvas canvas, Vector2 ringCenterUV) => Propose(spell, canvas, ringCenterUV);

        public NetCast(NetworkManager manager, NetCanvasSync canvasSync)
        {
            nm = manager;
            canvases = canvasSync;
            isServer = manager.IsServer;
        }

        public void Begin()
        {
            Current = this;
            output = Object.FindFirstObjectByType<SpellOutputController>();
            caster = Object.FindFirstObjectByType<SpellCaster>();
            nm.CustomMessagingManager.RegisterNamedMessageHandler(MPropose, OnPropose);
            nm.CustomMessagingManager.RegisterNamedMessageHandler(MEffect, OnEffect);
        }

        public void End()
        {
            if (Current == this) Current = null;
            ApplyingRemote = false;
            CoopInk = false;
            if (nm != null && nm.CustomMessagingManager != null)
            {
                nm.CustomMessagingManager.UnregisterNamedMessageHandler(MPropose);
                nm.CustomMessagingManager.UnregisterNamedMessageHandler(MEffect);
            }
        }

        // ---------------------------------------------------------------- lo llama SpellOutputController

        /// <summary>
        /// Se llama al empezar a instanciar un hechizo. Devuelve true si el efecto local debe ser solo visual (cliente). En el cliente propone el
        /// hechizo al servidor; en el anfitrion lo anuncia a todos. Sin red no hace nada (false).
        /// </summary>
        public static bool Intercept(SpellIR spell, DrawingCanvas canvas, Vector2 ringCenterUV, IPlayerContext casterContext)
        {
            NetCast c = Current;
            if (c == null) return false;
            if (ApplyingRemote) return !c.isServer; // el servidor ejecutando una propuesta: efecto completo; un cliente mostrando lo del servidor: visual
            if (!c.isServer)
            {
                c.Propose(spell, canvas, ringCenterUV);
                return true;
            }
            int ownerId = casterContext != null ? casterContext.OwnerId : PlayerRegistry.LocalOwnerId;
            c.Announce(spell, canvas, ringCenterUV, ownerId, NetworkManager.ServerClientId);
            return false;
        }

        // ---------------------------------------------------------------- cliente -> servidor

        private void Propose(SpellIR spell, DrawingCanvas canvas, Vector2 ringCenterUV)
        {
            if (canvases == null || !canvases.TryGetId(canvas, out ushort cid)) return; // lienzo creado en juego (union de mitades): solo local por ahora
            byte[] bytes = SpellIRCodec.Encode(spell);
            using (var w = new FastBufferWriter(2 + 8 + 4 + bytes.Length + 8, Allocator.Temp))
            {
                w.WriteValueSafe(cid);
                w.WriteValueSafe(ringCenterUV);
                w.WriteValueSafe(bytes.Length);
                w.WriteBytesSafe(bytes);
                nm.CustomMessagingManager.SendNamedMessage(MPropose, NetworkManager.ServerClientId, w, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        // ---------------------------------------------------------------- servidor

        private void OnPropose(ulong sender, FastBufferReader r)
        {
            if (!isServer) return;
            r.ReadValueSafe(out ushort cid);
            r.ReadValueSafe(out Vector2 uv);
            r.ReadValueSafe(out int len);
            if (len < 0 || len > 1 << 16) { ProposalsRejected++; return; }
            var bytes = new byte[len];
            r.ReadBytesSafe(ref bytes, len);

            DrawingCanvas canvas = canvases != null ? canvases.CanvasById(cid) : null;
            if (canvas == null || !SpellIRCodec.TryDecode(bytes, Time.time, out SpellIR spell) || !spell.valid || !spell.active) { ProposalsRejected++; return; }
            // Controles baratos: alcance y frecuencia (tope por cliente; un sello compuesto propone varios hechizos seguidos).
            IPlayerContext p = PlayerRegistry.ById((int)sender);
            if (p != null && p.Root != null && Vector3.Distance(p.Root.position, canvas.WorldCenter) > MaxReach) { ProposalsRejected++; return; }
            NetWorld world = NetWorld.Active;
            if (world != null && !world.Limiter.Allow(sender, NetChannel.Cast, Time.unscaledTime)) { ProposalsRejected++; return; }
            if (output == null) return;

            NetDuel duel = NetDuel.Active;
            if (duel != null && duel.InDuel)
            {
                // Duelo: el servidor no confia en el cliente. Re-lee el lienzo (que el ya tiene) y ejecuta SU lectura COMPLETA (todos los interiores de un
                // sello compuesto); y cobra la tinta de sus trazos. Los espectadores no lanzan. Las demas propuestas del mismo sello se ignoran.
                int index = duel.IndexOf(sender);
                if (index < 0) { ProposalsRejected++; return; }
                if (ChargedRecently(canvas)) return;
                if (!duel.TryCharge(index, canvas, out _)) { ProposalsNoInk++; return; }
                MarkCharged(canvas);
                if (caster == null || !caster.CompileAll(canvas, ownSpells, ownCenters)) { ProposalsRejected++; return; }
                bool corrected = ownSpells.Count != 1 || ownSpells[0].element != spell.element || ownSpells[0].sigilId != spell.sigilId;
                if (corrected) ProposalsCorrected++;
                for (int i = 0; i < ownSpells.Count; i++) Execute(ownSpells[i], canvas, ownCenters[i], p, sender);
                DuelSpellsExecuted += ownSpells.Count;
                caster.MarkSpent(canvas);
                return;
            }
            if (CoopInk && !ChargedRecently(canvas))
            {
                if (!ChargeCoop(sender, canvas)) { ProposalsNoInk++; return; }
                MarkCharged(canvas);
            }

            Execute(spell, canvas, uv, p, sender);
            if (caster != null) caster.MarkSpent(canvas);
        }

        /// <summary>El servidor ejecuta un hechizo de un cliente y lo anuncia a los demas.</summary>
        private void Execute(SpellIR spell, DrawingCanvas canvas, Vector2 uv, IPlayerContext casterPlayer, ulong sender)
        {
            ProposalsAccepted++;
            LastExecutedElement = spell.element;
            ApplyingRemote = true;
            try { output.SpawnSpellEffect(spell, canvas, uv, casterPlayer); }
            finally { ApplyingRemote = false; }
            Announce(spell, canvas, uv, (int)sender, sender);
        }

        /// <summary>Anuncia un hechizo a todos los clientes menos a <paramref name="except"/> (quien lo lanzo ya lo vio).</summary>
        private void Announce(SpellIR spell, DrawingCanvas canvas, Vector2 ringCenterUV, int casterOwner, ulong except)
        {
            if (canvases == null || !canvases.TryGetId(canvas, out ushort cid)) return;
            var targets = new List<ulong>(nm.ConnectedClientsIds.Count);
            foreach (ulong id in nm.ConnectedClientsIds) if (id != NetworkManager.ServerClientId && id != except) targets.Add(id);
            if (targets.Count == 0) return;
            byte[] bytes = SpellIRCodec.Encode(spell);
            using (var w = new FastBufferWriter(2 + 4 + 8 + 4 + bytes.Length + 8, Allocator.Temp))
            {
                w.WriteValueSafe(cid);
                w.WriteValueSafe(casterOwner);
                w.WriteValueSafe(ringCenterUV);
                w.WriteValueSafe(bytes.Length);
                w.WriteBytesSafe(bytes);
                nm.CustomMessagingManager.SendNamedMessage(MEffect, targets, w, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        // ---------------------------------------------------------------- cliente: mostrar lo de los demas

        private void OnEffect(ulong sender, FastBufferReader r)
        {
            if (isServer) return;
            r.ReadValueSafe(out ushort cid);
            r.ReadValueSafe(out int casterOwner);
            r.ReadValueSafe(out Vector2 uv);
            r.ReadValueSafe(out int len);
            if (len < 0 || len > 1 << 16) return;
            var bytes = new byte[len];
            r.ReadBytesSafe(ref bytes, len);
            DrawingCanvas canvas = canvases != null ? canvases.CanvasById(cid) : null;
            if (canvas == null || output == null || !SpellIRCodec.TryDecode(bytes, Time.time, out SpellIR spell) || !spell.valid || !spell.active) return;
            EffectsReceived++;
            ApplyingRemote = true;
            try { output.SpawnSpellEffect(spell, canvas, uv, PlayerRegistry.ById(casterOwner)); }
            finally { ApplyingRemote = false; }
            if (caster != null) caster.MarkSpent(canvas);
        }
    }
}
