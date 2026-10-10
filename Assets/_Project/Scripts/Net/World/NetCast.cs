using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using SpellSimulator.Canvas;
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
        private const float MinInterval = 0.2f, MaxReach = 15f;

        /// <summary>Instancia de la sesion actual (null sin red).</summary>
        public static NetCast Current { get; private set; }

        /// <summary>True mientras se reproduce un hechizo que vino de la red (no se vuelve a proponer ni a reenviar).</summary>
        public static bool ApplyingRemote { get; private set; }

        /// <summary>True si esta maquina es un cliente (no el anfitrion) de una sesion: sus hechizos son solo visuales y no dispara runas.</summary>
        public static bool IsClientOnly => Current != null && !Current.isServer;

        private readonly NetworkManager nm;
        private readonly NetCanvasSync canvases;
        private readonly bool isServer;
        private readonly Dictionary<ulong, float> lastCastAt = new Dictionary<ulong, float>(8);
        private SpellOutputController output;
        private SpellCaster caster;

        public int ProposalsAccepted { get; private set; }
        public int ProposalsRejected { get; private set; }
        public int EffectsReceived { get; private set; }

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
            // Cheap checks: alcance y frecuencia.
            IPlayerContext p = PlayerRegistry.ById((int)sender);
            if (p != null && p.Root != null && Vector3.Distance(p.Root.position, canvas.WorldCenter) > MaxReach) { ProposalsRejected++; return; }
            float now = Time.unscaledTime;
            if (lastCastAt.TryGetValue(sender, out float last) && now - last < MinInterval) { ProposalsRejected++; return; }
            lastCastAt[sender] = now;
            if (output == null) return;

            ProposalsAccepted++;
            ApplyingRemote = true;
            try { output.SpawnSpellEffect(spell, canvas, uv, p); }
            finally { ApplyingRemote = false; }
            if (caster != null) caster.MarkSpent(canvas);
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
