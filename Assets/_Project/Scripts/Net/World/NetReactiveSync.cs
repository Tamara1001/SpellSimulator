using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using SpellSimulator.Reactions;

namespace SpellSimulator.Net
{
    /// <summary>
    /// Mundo reactivo compartido (F4 de MULTIPLAYER_PLAN): solo el anfitrion simula las reacciones (fuego del pasto, objetos que arden, se mojan,
    /// se rompen o se cristalizan) y manda el ESTADO a los clientes, que solo lo muestran. Asi un fuego que enciende A y apaga B se ve igual en todos.
    /// - Pasto (<see cref="GrassFire"/>): las 4900 celdas a 2 bits (1225 bytes) cada 0,5 s si algo cambio.
    /// - Objetos (<see cref="ElementReactive"/>): humedad, quemado, integridad y banderas, por indice de la lista ordenada de la escena; solo lo que cambio, a 4 Hz.
    /// Quien entra tarde recibe todo. Los charcos, el hielo y las inundaciones salen de los hechizos (que ya corren en todas las maquinas).
    /// </summary>
    public sealed class NetReactiveSync
    {
        private const string MGrass = "rx.grass", MStates = "rx.states";
        private const float ObjectsInterval = 0.25f, GrassInterval = 0.5f;
        private const int EntriesPerPacket = 60, EntryBytes = 2 + 4;

        private struct Packed { public byte wet, burn, integrity, flags; }

        private static readonly Dictionary<int, string> sceneKeys = new Dictionary<int, string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Snapshot()
        {
            sceneKeys.Clear();
            foreach (var r in Object.FindObjectsByType<ElementReactive>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (r != null) sceneKeys[r.GetInstanceID()] = NetBodyMath.PathKey(r.transform);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => sceneKeys.Clear();

        private readonly NetworkManager nm;
        private readonly bool isServer;
        private readonly List<ElementReactive> reactives = new List<ElementReactive>(128);
        private Packed[] lastSent;
        private bool[] everSent;
        private float objectsTimer, grassTimer;
        private int lastGrassVersion = -1;
        private MagicWorld world;
        private uint fingerprint;
        private bool mismatch;

        public int ReactiveCount => reactives.Count;

        public NetReactiveSync(NetworkManager manager)
        {
            nm = manager;
            isServer = manager.IsServer;
        }

        public void Begin()
        {
            world = Object.FindFirstObjectByType<MagicWorld>();
            var keyed = new List<KeyValuePair<string, ElementReactive>>(sceneKeys.Count);
            foreach (var r in Object.FindObjectsByType<ElementReactive>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r == null) continue;
                if (!sceneKeys.TryGetValue(r.GetInstanceID(), out string key))
                {
                    if (sceneKeys.Count > 0) continue; // creado despues de cargar la escena (rocas, clones): no entra
                    key = NetBodyMath.PathKey(r.transform);
                }
                keyed.Add(new KeyValuePair<string, ElementReactive>(key, r));
            }
            keyed.Sort((a, b) => NetBodyMath.CompareKeys(a.Key, b.Key));
            var keys = new List<string>(keyed.Count);
            foreach (var kv in keyed) { keys.Add(kv.Key); reactives.Add(kv.Value); }
            fingerprint = NetBodyMath.Fingerprint(keys);
            lastSent = new Packed[reactives.Count];
            everSent = new bool[reactives.Count];
            if (GrassFire.Current != null) GrassFire.Current.NetFollower = !isServer;
            var cm = nm.CustomMessagingManager;
            cm.RegisterNamedMessageHandler(MGrass, OnGrass);
            cm.RegisterNamedMessageHandler(MStates, OnStates);
        }

        public void End()
        {
            if (GrassFire.Current != null) GrassFire.Current.NetFollower = false;
            if (nm != null && nm.CustomMessagingManager != null)
            {
                nm.CustomMessagingManager.UnregisterNamedMessageHandler(MGrass);
                nm.CustomMessagingManager.UnregisterNamedMessageHandler(MStates);
            }
            reactives.Clear();
        }

        // ---------------------------------------------------------------- servidor

        public void Tick(float dt)
        {
            if (!isServer || mismatch || nm.ConnectedClientsIds.Count <= 1) return;
            objectsTimer += dt;
            grassTimer += dt;
            if (objectsTimer >= ObjectsInterval) { objectsTimer = 0f; SendObjects(false, null); }
            if (grassTimer >= GrassInterval)
            {
                grassTimer = 0f;
                GrassFire fire = GrassFire.Current;
                if (fire != null && fire.NetVersion != lastGrassVersion) { lastGrassVersion = fire.NetVersion; SendGrass(null); }
            }
        }

        private static Packed Pack(ElementReactive r)
        {
            ReactiveState s = r.State;
            byte flags = 0;
            if (s.Burning) flags |= 1;
            if (s.Broken) flags |= 2;
            if (r.Crystallized) flags |= 4;
            if (!r.isActiveAndEnabled) flags |= 8;
            return new Packed
            {
                wet = (byte)Mathf.RoundToInt(Mathf.Clamp01(s.Wetness) * 255f),
                burn = (byte)Mathf.RoundToInt(Mathf.Clamp01(s.BurnProgress) * 255f),
                integrity = (byte)Mathf.RoundToInt(Mathf.Clamp01(s.Integrity) * 255f),
                flags = flags,
            };
        }

        private static bool Same(Packed a, Packed b) => a.wet == b.wet && a.burn == b.burn && a.integrity == b.integrity && a.flags == b.flags;

        /// <summary>Manda los objetos cuyo estado cambio (o todos, para el que entra tarde).</summary>
        private void SendObjects(bool all, ulong? only)
        {
            var changed = new List<int>(16);
            for (int i = 0; i < reactives.Count; i++)
            {
                if (reactives[i] == null) continue;
                Packed p = Pack(reactives[i]);
                if (all || !everSent[i] || !Same(p, lastSent[i])) changed.Add(i);
                if (!only.HasValue) { lastSent[i] = p; everSent[i] = true; }
            }
            if (!only.HasValue) { /* los marcados ya se anotaron arriba */ }
            int chunks = NetBodyMath.ChunkCount(changed.Count, EntriesPerPacket);
            for (int c = 0; c < chunks; c++)
            {
                int from = c * EntriesPerPacket, n = Mathf.Min(EntriesPerPacket, changed.Count - from);
                using (var w = new FastBufferWriter(2 + 4 + n * EntryBytes + 8, Allocator.Temp))
                {
                    w.WriteValueSafe(fingerprint);
                    w.WriteValueSafe((ushort)n);
                    for (int k = 0; k < n; k++)
                    {
                        int i = changed[from + k];
                        Packed p = Pack(reactives[i]);
                        w.WriteValueSafe((ushort)i);
                        w.WriteValueSafe(p.wet); w.WriteValueSafe(p.burn); w.WriteValueSafe(p.integrity); w.WriteValueSafe(p.flags);
                    }
                    if (only.HasValue) nm.CustomMessagingManager.SendNamedMessage(MStates, only.Value, w, NetworkDelivery.ReliableFragmentedSequenced);
                    else nm.CustomMessagingManager.SendNamedMessageToAll(MStates, w, NetworkDelivery.ReliableFragmentedSequenced);
                }
            }
        }

        private void SendGrass(ulong? only)
        {
            GrassFire fire = GrassFire.Current;
            if (fire == null) return;
            byte[] packed = fire.NetPackState();
            using (var w = new FastBufferWriter(4 + packed.Length + 8, Allocator.Temp))
            {
                w.WriteValueSafe(packed.Length);
                w.WriteBytesSafe(packed);
                if (only.HasValue) nm.CustomMessagingManager.SendNamedMessage(MGrass, only.Value, w, NetworkDelivery.ReliableFragmentedSequenced);
                else nm.CustomMessagingManager.SendNamedMessageToAll(MGrass, w, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        /// <summary>El anfitrion manda todo a quien entra.</summary>
        public void SendFull(ulong clientId)
        {
            SendObjects(true, clientId);
            SendGrass(clientId);
        }

        // ---------------------------------------------------------------- cliente

        private void OnGrass(ulong sender, FastBufferReader r)
        {
            if (isServer || mismatch) return;
            r.ReadValueSafe(out int len);
            if (len <= 0 || len > 8192) return;
            var bytes = new byte[len];
            r.ReadBytesSafe(ref bytes, len);
            if (GrassFire.Current != null) GrassFire.Current.NetApplyState(bytes);
        }

        private void OnStates(ulong sender, FastBufferReader r)
        {
            if (isServer) return;
            r.ReadValueSafe(out uint remote);
            if (remote != fingerprint)
            {
                if (!mismatch) Debug.LogError($"[NetReactive] Los objetos reactivos del anfitrion son distintos de los tuyos (huella {remote:X8} contra {fingerprint:X8}): el mundo reactivo compartido queda apagado.");
                mismatch = true;
                return;
            }
            r.ReadValueSafe(out ushort n);
            for (int k = 0; k < n; k++)
            {
                r.ReadValueSafe(out ushort i);
                r.ReadValueSafe(out byte wet); r.ReadValueSafe(out byte burn); r.ReadValueSafe(out byte integrity); r.ReadValueSafe(out byte flags);
                if (i >= reactives.Count || reactives[i] == null) continue;
                Apply(reactives[i], new Packed { wet = wet, burn = burn, integrity = integrity, flags = flags });
            }
        }

        private void Apply(ElementReactive reactive, Packed p)
        {
            ReactiveState before = reactive.State;
            var next = new ReactiveState
            {
                Heat = before.Heat,
                Wetness = p.wet / 255f,
                BurnProgress = p.burn / 255f,
                Integrity = p.integrity / 255f,
                Burning = (p.flags & 1) != 0,
                Broken = (p.flags & 2) != 0,
            };
            ReactionEvents events = ReactionEvents.None;
            if (!before.Burning && next.Burning) events |= ReactionEvents.Ignited;
            if (before.Burning && !next.Burning) events |= next.BurnProgress >= 0.99f ? ReactionEvents.BurnedOut : ReactionEvents.Extinguished;
            if (!before.Broken && next.Broken) events |= ReactionEvents.Broken;
            reactive.NetApply(next, (p.flags & 4) != 0);
            if (events != ReactionEvents.None && world != null && reactive.isActiveAndEnabled) world.NetHandle(reactive, events);
            // (flags & 8) = apagado en el anfitrion (se rompio y se cambio por fragmentos): lo recoge la replica del cuerpo (activo/inactivo)
        }

        // ---------------------------------------------------------------- consulta (pruebas)

        /// <summary>Una linea con lo que ve esta maquina: cuantas celdas arden o se quemaron y cuantos objetos arden, estan mojados o rotos.</summary>
        public string Describe()
        {
            GrassFire fire = GrassFire.Current;
            int burning = 0, wet = 0, broken = 0;
            foreach (var r in reactives)
            {
                if (r == null) continue;
                if (r.State.Burning) burning++;
                if (r.State.Wetness > 0.3f) wet++;
                if (r.State.Broken) broken++;
            }
            return $"pasto ardiendo={(fire != null ? fire.BurningCells : -1)} quemado={(fire != null ? fire.BurntCells : -1)} objetos ardiendo={burning} mojados={wet} rotos={broken}";
        }
    }
}
