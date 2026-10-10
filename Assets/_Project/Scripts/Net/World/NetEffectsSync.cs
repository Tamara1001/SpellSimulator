using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using SpellSimulator.Output;
using SpellSimulator.Reactions;
using SpellSimulator.World;

namespace SpellSimulator.Net
{
    /// <summary>
    /// Efectos del agua compartidos (cierra el limite de KI-121): solo el anfitrion simula charcos, hielo e inundaciones y los clientes copian el resultado.
    /// - Formas solidas de tierra (muros, puentes, estacas: <c>fx.sol</c>) y cortes de estructuras (<c>fx.cut</c>, signo Crush): el anfitrion avisa cada una y
    ///   a quien entra tarde le manda lo vivo (con la edad de cada pieza).
    /// - Charcos (<see cref="WaterPuddleManager"/>, pool de 16): los que cambiaron, cada 0,25 s.
    /// - Placas de hielo (<see cref="IceSheet"/>) y zonas inundadas (<see cref="FloodZone"/>): un aviso al crearse (el cliente arma la suya con el tiempo que lleva,
    ///   y la deja correr sola) y, para la zona, otro al congelarse. Quien entra tarde recibe todo lo vivo. Reiniciar el mundo las borra en todos.
    /// </summary>
    public sealed class NetEffectsSync
    {
        private const string MPuddles = "fx.pud", MNew = "fx.new", MFreeze = "fx.zf", MSolid = "fx.sol", MCut = "fx.cut";
        private const int PieceBytes = 12 + 12 + 16 + 12 + 1 + 1;
        private const float PuddleInterval = 0.25f;
        private const byte KindZone = 1, KindIce = 2;

        /// <summary>True mientras un cliente arma un efecto que vino del anfitrion (las fabricas lo dejan pasar).</summary>
        public static bool Applying { get; private set; }

        private struct PuddleCache { public bool active, frozen; public Vector3 pos; public float liters; public bool init; }

        private readonly NetworkManager nm;
        private readonly bool isServer;
        private readonly PuddleCache[] cache = new PuddleCache[WaterPuddleManager.MaxPuddles];
        private readonly Dictionary<int, FloodZone> zonesById = new Dictionary<int, FloodZone>(4);
        private float timer;

        public NetEffectsSync(NetworkManager manager)
        {
            nm = manager;
            isServer = manager.IsServer;
        }

        public int PuddlesActive => WaterPuddleManager.ActiveCount;
        public int ZonesActive => FloodZone.ActiveCount;
        public int IceActive => IceSheet.ActiveCount;

        public void Begin()
        {
            var cm = nm.CustomMessagingManager;
            cm.RegisterNamedMessageHandler(MPuddles, OnPuddles);
            cm.RegisterNamedMessageHandler(MNew, OnNew);
            cm.RegisterNamedMessageHandler(MFreeze, OnFreeze);
            cm.RegisterNamedMessageHandler(MSolid, OnSolid);
            cm.RegisterNamedMessageHandler(MCut, OnCut);
            if (isServer)
            {
                EarthShapes.Spawned += OnShapes;
                MeshBreaker.Broke += OnBroke;
                IceSheet.Spawned += OnIce;
                FloodZone.Spawned += OnZone;
                FloodZone.Frozen += OnZoneFrozen;
            }
        }

        public void End()
        {
            IceSheet.Spawned -= OnIce;
            FloodZone.Spawned -= OnZone;
            FloodZone.Frozen -= OnZoneFrozen;
            EarthShapes.Spawned -= OnShapes;
            MeshBreaker.Broke -= OnBroke;
            Applying = false;
            if (nm != null && nm.CustomMessagingManager != null)
            {
                nm.CustomMessagingManager.UnregisterNamedMessageHandler(MPuddles);
                nm.CustomMessagingManager.UnregisterNamedMessageHandler(MNew);
                nm.CustomMessagingManager.UnregisterNamedMessageHandler(MFreeze);
                nm.CustomMessagingManager.UnregisterNamedMessageHandler(MSolid);
                nm.CustomMessagingManager.UnregisterNamedMessageHandler(MCut);
            }
            zonesById.Clear();
        }

        // ---------------------------------------------------------------- servidor

        public void Tick(float dt)
        {
            if (!isServer || nm.ConnectedClientsIds.Count <= 1) return;
            timer += dt;
            if (timer < PuddleInterval) return;
            timer = 0f;
            SendPuddles(null, false);
        }

        public void SendFull(ulong clientId)
        {
            SendPuddles(clientId, true);
            foreach (FloodZone z in FloodZone.Zones) if (z != null) SendZone(z, clientId);
            foreach (IceSheet s in IceSheet.All) if (s != null && s.SecondsLeft > 0f) SendIce(s, clientId);
            SendLiveShapes(clientId);
            foreach (MeshBreaker.CutRecord cut in MeshBreaker.Log) SendCut(cut.Center, cut.Radius, clientId);
        }

        private void OnShapes(List<EarthShapes.PieceData> pieces) => SendPieces(pieces, 0f, null);

        private void SendLiveShapes(ulong clientId)
        {
            var pieces = new List<EarthShapes.PieceData>();
            var ages = new List<float>();
            EarthShapes.CollectLive(pieces, ages);
            // Un mensaje por grupo de piezas con la misma edad aproximada (las de una forma nacieron juntas): se agrupan de a 14.
            var group = new List<EarthShapes.PieceData>(14);
            float groupAge = 0f;
            for (int i = 0; i < pieces.Count; i++)
            {
                if (group.Count == 0) groupAge = ages[i];
                group.Add(pieces[i]);
                bool last = i == pieces.Count - 1;
                if (group.Count >= 14 || last || Mathf.Abs(ages[i + (last ? 0 : 1)] - groupAge) > 0.5f)
                {
                    SendPieces(group, groupAge, clientId);
                    group.Clear();
                }
            }
        }

        private void SendPieces(List<EarthShapes.PieceData> pieces, float age, ulong? only)
        {
            if (pieces.Count == 0) return;
            if (!only.HasValue && nm.ConnectedClientsIds.Count <= 1) return;
            using (var w = new FastBufferWriter(8 + pieces.Count * PieceBytes, Allocator.Temp))
            {
                w.WriteValueSafe(age);
                w.WriteValueSafe((byte)pieces.Count);
                foreach (EarthShapes.PieceData p in pieces)
                {
                    w.WriteValueSafe(p.start); w.WriteValueSafe(p.final); w.WriteValueSafe(p.rot); w.WriteValueSafe(p.scale);
                    w.WriteValueSafe(p.variant);
                    w.WriteValueSafe((byte)(p.fixedShape ? 1 : 0));
                }
                Send(MSolid, w, only);
            }
        }

        private void OnBroke(Vector3 center, float radius) => SendCut(center, radius, null);

        private void SendCut(Vector3 center, float radius, ulong? only)
        {
            using (var w = new FastBufferWriter(16, Allocator.Temp))
            {
                w.WriteValueSafe(center);
                w.WriteValueSafe(radius);
                Send(MCut, w, only);
            }
        }

        private void SendPuddles(ulong? only, bool all)
        {
            var changed = new List<int>(WaterPuddleManager.MaxPuddles);
            for (int i = 0; i < WaterPuddleManager.MaxPuddles; i++)
            {
                WaterPuddleManager.NetRead(i, out bool active, out Vector3 pos, out float liters, out bool frozen);
                PuddleCache c = cache[i];
                bool same = c.init && c.active == active && c.frozen == frozen && (!active || ((c.pos - pos).sqrMagnitude < 1e-4f && Mathf.Abs(c.liters - liters) < 0.01f));
                if (all ? active : !same) changed.Add(i);
                if (!only.HasValue) cache[i] = new PuddleCache { active = active, frozen = frozen, pos = pos, liters = liters, init = true };
            }
            if (changed.Count == 0) return;
            using (var w = new FastBufferWriter(1 + changed.Count * 19 + 8, Allocator.Temp))
            {
                w.WriteValueSafe((byte)changed.Count);
                foreach (int i in changed)
                {
                    WaterPuddleManager.NetRead(i, out bool active, out Vector3 pos, out float liters, out bool frozen);
                    w.WriteValueSafe((byte)i);
                    w.WriteValueSafe((byte)((active ? 1 : 0) | (frozen ? 2 : 0)));
                    w.WriteValueSafe(pos);
                    w.WriteValueSafe(liters);
                }
                if (only.HasValue) nm.CustomMessagingManager.SendNamedMessage(MPuddles, only.Value, w, NetworkDelivery.Reliable);
                else nm.CustomMessagingManager.SendNamedMessageToAll(MPuddles, w, NetworkDelivery.Reliable);
            }
        }

        private void OnZone(FloodZone zone) => SendZone(zone, null);
        private void OnIce(IceSheet sheet) => SendIce(sheet, null);

        private void SendZone(FloodZone z, ulong? only)
        {
            using (var w = new FastBufferWriter(64, Allocator.Temp))
            {
                w.WriteValueSafe(KindZone);
                w.WriteValueSafe(z.NetId);
                w.WriteValueSafe(z.Ground);
                w.WriteValueSafe(z.Size); w.WriteValueSafe(z.Life); w.WriteValueSafe(z.Delay); w.WriteValueSafe(z.Age);
                Send(MNew, w, only);
            }
        }

        private void SendIce(IceSheet s, ulong? only)
        {
            using (var w = new FastBufferWriter(64, Allocator.Temp))
            {
                w.WriteValueSafe(KindIce);
                w.WriteValueSafe(0);
                w.WriteValueSafe(s.Top);
                w.WriteValueSafe(s.HalfX); w.WriteValueSafe(s.HalfZ); w.WriteValueSafe(s.Thickness); w.WriteValueSafe(s.SecondsLeft);
                Send(MNew, w, only);
            }
        }

        private void OnZoneFrozen(FloodZone zone, float seconds)
        {
            using (var w = new FastBufferWriter(16, Allocator.Temp))
            {
                w.WriteValueSafe(zone.NetId);
                w.WriteValueSafe(seconds);
                Send(MFreeze, w, null);
            }
        }

        private void Send(string message, FastBufferWriter w, ulong? only)
        {
            if (only.HasValue) nm.CustomMessagingManager.SendNamedMessage(message, only.Value, w, NetworkDelivery.Reliable);
            else if (nm.ConnectedClientsIds.Count > 1) nm.CustomMessagingManager.SendNamedMessageToAll(message, w, NetworkDelivery.Reliable);
        }

        // ---------------------------------------------------------------- cliente

        private void OnPuddles(ulong sender, FastBufferReader r)
        {
            if (isServer) return;
            r.ReadValueSafe(out byte n);
            for (int k = 0; k < n; k++)
            {
                r.ReadValueSafe(out byte i); r.ReadValueSafe(out byte flags); r.ReadValueSafe(out Vector3 pos); r.ReadValueSafe(out float liters);
                if (float.IsNaN(pos.x + pos.y + pos.z + liters)) continue;
                WaterPuddleManager.NetApply(i, (flags & 1) != 0, pos, liters, (flags & 2) != 0);
            }
        }

        private void OnNew(ulong sender, FastBufferReader r)
        {
            if (isServer) return;
            r.ReadValueSafe(out byte kind); r.ReadValueSafe(out int id); r.ReadValueSafe(out Vector3 at);
            Applying = true;
            try
            {
                if (kind == KindZone)
                {
                    r.ReadValueSafe(out float size); r.ReadValueSafe(out float life); r.ReadValueSafe(out float delay); r.ReadValueSafe(out float age);
                    if (float.IsNaN(at.x + at.y + at.z + size + life + delay + age) || size <= 0f || size > 60f || life <= 0f) return;
                    if (zonesById.TryGetValue(id, out FloodZone old) && old != null) return; // ya la tengo (llego dos veces)
                    Material material = WaterPuddleManager.CurrentMaterial;
                    if (material == null && WaterVolume.All.Count > 0) material = WaterVolume.All[0].SurfaceMaterial;
                    FloodZone zone = FloodZone.Create(at, size, life, delay, material, age);
                    if (zone != null) { zone.NetId = id; zonesById[id] = zone; }
                }
                else if (kind == KindIce)
                {
                    r.ReadValueSafe(out float hx); r.ReadValueSafe(out float hz); r.ReadValueSafe(out float thickness); r.ReadValueSafe(out float seconds);
                    if (float.IsNaN(at.x + at.y + at.z + hx + hz + thickness + seconds) || hx > 60f || hz > 60f) return;
                    IceSheet.Create(at, hx, hz, Mathf.Clamp(thickness, 0.02f, 1f), seconds);
                }
            }
            finally { Applying = false; }
        }

        private void OnSolid(ulong sender, FastBufferReader r)
        {
            if (isServer) return;
            r.ReadValueSafe(out float age);
            r.ReadValueSafe(out byte n);
            if (float.IsNaN(age) || n == 0 || n > 20) return;
            var output = Object.FindFirstObjectByType<SpellOutputController>();
            ElementLook look = output != null ? output.LookFor(Compiler.ElementType.Earth, "earth") : null;
            for (int k = 0; k < n; k++)
            {
                var p = new EarthShapes.PieceData();
                r.ReadValueSafe(out p.start); r.ReadValueSafe(out p.final); r.ReadValueSafe(out p.rot); r.ReadValueSafe(out p.scale);
                r.ReadValueSafe(out p.variant);
                r.ReadValueSafe(out byte flags);
                p.fixedShape = (flags & 1) != 0;
                if (float.IsNaN(p.start.x + p.final.x + p.scale.x + p.rot.w) || p.scale.sqrMagnitude > 400f) continue;
                float mag = p.rot.x * p.rot.x + p.rot.y * p.rot.y + p.rot.z * p.rot.z + p.rot.w * p.rot.w;
                if (!(mag > 0.5f && mag < 1.5f)) p.rot = Quaternion.identity;
                EarthShapes.BuildReplica(p, Mathf.Max(0f, age), look);
            }
        }

        private void OnCut(ulong sender, FastBufferReader r)
        {
            if (isServer) return;
            r.ReadValueSafe(out Vector3 center); r.ReadValueSafe(out float radius);
            if (float.IsNaN(center.x + center.y + center.z + radius) || radius <= 0f || radius > 30f) return;
            MeshBreaker.Break(center, radius, announce: false);
        }

        private void OnFreeze(ulong sender, FastBufferReader r)
        {
            if (isServer) return;
            r.ReadValueSafe(out int id); r.ReadValueSafe(out float seconds);
            if (zonesById.TryGetValue(id, out FloodZone zone) && zone != null) zone.NetFreeze(seconds);
        }

        // ---------------------------------------------------------------- consulta (pruebas)

        public string Describe() => $"charcos={WaterPuddleManager.ActiveCount} zonas={FloodZone.ActiveCount} hielo={IceSheet.ActiveCount} piezas={SolidPiece.Count} cortes={MeshBreaker.Log.Count}";
    }
}
