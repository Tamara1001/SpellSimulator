using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Core;

namespace SpellSimulator.Net
{
    /// <summary>
    /// Dibujo compartido (F3 de MULTIPLAYER_PLAN, 2.3): replica los TRAZOS de cada lienzo de la escena (no texturas). Cada maquina pinta al instante lo
    /// suyo (prediccion) y cada 50 ms compara la sesion de cada lienzo con lo ultimo que sincronizo: trazos nuevos, puntos agregados a un trazo en
    /// curso, trazos quitados (goma, deshacer) o lienzo borrado. Asi sirve igual para el lapiz, la goma, el trapo, las estampas y lo que rompe o
    /// limpia un lienzo, sin tocar cada herramienta. El servidor valida lo barato y reenvia a los demas; quien entra tarde recibe todos los
    /// lienzos con trazos. Los trazos se identifican por (duenio, id): dos jugadores dibujando a la vez en el mismo lienzo no chocan.
    /// Lo que llega de la red no dispara un lanzamiento: el lanzamiento lo propone quien cerro el anillo (ver <see cref="NetCast"/>); aca solo se
    /// actualiza la fase del sello (preparado, armado) de forma diferida.
    /// </summary>
    public sealed class NetCanvasSync
    {
        // Un sello entero dibujado de golpe pasa de 1264 bytes (maximo de un mensaje Reliable): todo va fragmentado y en orden.
        private const string MOps = "sc.ops", MFull = "sc.full", MRej = "sc.rej";
        private const float TickInterval = 0.05f, EvalQuiet = 0.25f;
        private const int MaxPointsPerOp = 20000;
        private const float MaxReach = 15f;

        private const byte OpAdd = 0, OpExtend = 1, OpRemove = 2, OpClear = 3;

        private struct Known { public int owner; public uint id; public int points; }

        private sealed class State
        {
            public DrawingCanvas canvas;
            public ushort id;
            public readonly List<Known> known = new List<Known>(16);
            public int lastCount = -1, lastRevision = -1, lastTailPoints = -1;
            public float evalAt = -1f;
        }

        // Claves de los lienzos al cargar la escena (antes de cualquier Start): lo creado despues no entra a la lista.
        private static readonly Dictionary<int, string> sceneCanvasKeys = new Dictionary<int, string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SnapshotSceneCanvases()
        {
            sceneCanvasKeys.Clear();
            foreach (var c in Object.FindObjectsByType<DrawingCanvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c != null && Eligible(c)) sceneCanvasKeys[c.GetInstanceID()] = NetBodyMath.PathKey(c.transform);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => sceneCanvasKeys.Clear();

        /// <summary>Las hojas del grimorio son locales (no se comparten).</summary>
        private static bool Eligible(DrawingCanvas c) => c.GetComponentInParent<Learning.Grimoire>() == null;

        private readonly NetworkManager nm;
        private readonly bool isServer;
        private readonly List<State> states = new List<State>(64);
        private readonly Dictionary<ushort, State> byId = new Dictionary<ushort, State>(64);
        private readonly Dictionary<DrawingCanvas, State> byCanvas = new Dictionary<DrawingCanvas, State>(64);
        private SpellCaster caster;
        private uint fingerprint;
        private bool mismatch;
        private float tickTimer;
        private readonly List<byte[]> scratchBytes = new List<byte[]>(8);

        public int CanvasCount => states.Count;
        public bool Mismatch => mismatch;

        public NetCanvasSync(NetworkManager manager)
        {
            nm = manager;
            isServer = manager.IsServer;
        }

        /// <summary>Arma la lista de lienzos (mismo orden en todas las maquinas), fija el duenio de los trazos nuevos y registra los mensajes.</summary>
        public void Begin()
        {
            caster = Object.FindFirstObjectByType<SpellCaster>();
            var keyed = new List<KeyValuePair<string, DrawingCanvas>>(sceneCanvasKeys.Count);
            foreach (var c in Object.FindObjectsByType<DrawingCanvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (c == null) continue;
                if (!sceneCanvasKeys.TryGetValue(c.GetInstanceID(), out string key))
                {
                    if (sceneCanvasKeys.Count > 0 || !Eligible(c)) continue;
                    key = NetBodyMath.PathKey(c.transform);
                }
                keyed.Add(new KeyValuePair<string, DrawingCanvas>(key, c));
            }
            keyed.Sort((a, b) => NetBodyMath.CompareKeys(a.Key, b.Key));
            var keys = new List<string>(keyed.Count);
            for (int i = 0; i < keyed.Count && i < ushort.MaxValue; i++)
            {
                keys.Add(keyed[i].Key);
                var s = new State { canvas = keyed[i].Value, id = (ushort)i };
                states.Add(s);
                byId[s.id] = s;
                byCanvas[s.canvas] = s;
                s.canvas.Session.SetOwnerForNewStrokes(PlayerRegistry.LocalOwnerId);
                SnapshotKnown(s); // lo que ya hay al empezar es de todos (no se envia: cada maquina lo tiene de la escena)
            }
            fingerprint = NetBodyMath.Fingerprint(keys);
            var cm = nm.CustomMessagingManager;
            cm.RegisterNamedMessageHandler(MOps, OnOps);
            cm.RegisterNamedMessageHandler(MFull, OnFull);
            cm.RegisterNamedMessageHandler(MRej, OnRejected);
        }

        public void End()
        {
            if (nm != null && nm.CustomMessagingManager != null)
            {
                var cm = nm.CustomMessagingManager;
                cm.UnregisterNamedMessageHandler(MOps); cm.UnregisterNamedMessageHandler(MFull); cm.UnregisterNamedMessageHandler(MRej);
            }
            foreach (var s in states) if (s.canvas != null) s.canvas.Session.SetOwnerForNewStrokes(0);
            states.Clear(); byId.Clear(); byCanvas.Clear();
        }

        /// <summary>El id de cliente local se conoce tarde (al conectar): se actualiza el duenio de los trazos nuevos.</summary>
        public void RefreshOwner()
        {
            int me = PlayerRegistry.LocalOwnerId;
            foreach (var s in states) if (s.canvas != null) s.canvas.Session.SetOwnerForNewStrokes(me);
        }

        public bool TryGetId(DrawingCanvas canvas, out ushort id)
        {
            if (canvas != null && byCanvas.TryGetValue(canvas, out State s)) { id = s.id; return true; }
            id = 0;
            return false;
        }

        public bool TryFindByName(string objectName, out DrawingCanvas canvas)
        {
            foreach (var s in states)
                if (s.canvas != null && s.canvas.name == objectName) { canvas = s.canvas; return true; }
            canvas = null;
            return false;
        }

        public DrawingCanvas CanvasById(ushort id) => byId.TryGetValue(id, out State s) ? s.canvas : null;

        // ---------------------------------------------------------------- bucle

        public void Tick(float dt)
        {
            if (mismatch) return;
            float now = Time.unscaledTime;
            tickTimer += dt;
            if (tickTimer >= TickInterval)
            {
                tickTimer = 0f;
                for (int i = 0; i < states.Count; i++) SendLocalChanges(states[i]);
            }
            // lo que llego de la red se evalua (fase del sello) cuando se calma
            for (int i = 0; i < states.Count; i++)
            {
                State s = states[i];
                if (s.evalAt < 0f || now < s.evalAt) continue;
                s.evalAt = -1f;
                if (s.canvas != null && caster != null && s.canvas.isActiveAndEnabled) caster.OnSealEdited(s.canvas);
            }
        }

        // ---------------------------------------------------------------- local -> red

        private static bool SameKey(Known k, Stroke s) => k.owner == s.OwnerId && k.id == s.StrokeId;

        private void SnapshotKnown(State s)
        {
            s.known.Clear();
            var strokes = s.canvas.Session.Strokes;
            for (int i = 0; i < strokes.Count; i++) s.known.Add(new Known { owner = strokes[i].OwnerId, id = strokes[i].StrokeId, points = strokes[i].Points.Count });
            RememberTrackers(s);
        }

        private static void RememberTrackers(State s)
        {
            var session = s.canvas.Session;
            s.lastCount = session.Strokes.Count;
            s.lastRevision = session.Revision;
            s.lastTailPoints = session.Strokes.Count > 0 ? session.Strokes[session.Strokes.Count - 1].Points.Count : 0;
        }

        private static bool LooksChanged(State s)
        {
            var session = s.canvas.Session;
            int count = session.Strokes.Count;
            if (count != s.lastCount || session.Revision != s.lastRevision) return true;
            return count > 0 && session.Strokes[count - 1].Points.Count != s.lastTailPoints;
        }

        private void SendLocalChanges(State s)
        {
            if (s.canvas == null || !LooksChanged(s)) return;
            var strokes = s.canvas.Session.Strokes;

            // Calcula las operaciones comparando con lo ultimo sincronizado.
            var removed = new List<Known>();
            for (int i = 0; i < s.known.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < strokes.Count && !found; j++) found = SameKey(s.known[i], strokes[j]);
                if (!found) removed.Add(s.known[i]);
            }
            bool clearAll = strokes.Count == 0 && s.known.Count > 0;

            var ops = new List<(byte type, Stroke stroke, int from, Known key)>(4);
            if (clearAll) ops.Add((OpClear, null, 0, default));
            else foreach (var k in removed) ops.Add((OpRemove, null, 0, k));
            for (int j = 0; j < strokes.Count; j++)
            {
                Stroke st = strokes[j];
                int idx = -1;
                for (int i = 0; i < s.known.Count; i++) if (SameKey(s.known[i], st)) { idx = i; break; }
                if (idx < 0) ops.Add((OpAdd, st, 0, default));
                else if (st.Points.Count > s.known[idx].points) ops.Add((OpExtend, st, s.known[idx].points, default));
            }

            if (ops.Count > 0) SendOps(s, ops);
            SnapshotKnown(s);
        }

        private void SendOps(State s, List<(byte type, Stroke stroke, int from, Known key)> ops)
        {
            Debug.Log($"[NetCanvas] envio {ops.Count} operaciones de {s.canvas.name} (id {s.id})");
            // Primero se codifican los trazos para saber el tamano del mensaje.
            scratchBytes.Clear();
            int size = 2 + 2;
            foreach (var op in ops)
            {
                size += 1;
                if (op.type == OpAdd || op.type == OpExtend)
                {
                    Stroke send = op.stroke;
                    if (op.type == OpExtend)
                    {
                        send = new Stroke { Width = op.stroke.Width, Ink = op.stroke.Ink, OwnerId = op.stroke.OwnerId, StrokeId = op.stroke.StrokeId };
                        for (int i = op.from; i < op.stroke.Points.Count; i++) send.Points.Add(op.stroke.Points[i]);
                        size += 4;
                    }
                    byte[] bytes = StrokeCodec.Encode(send);
                    scratchBytes.Add(bytes);
                    size += 4 + bytes.Length;
                }
                else if (op.type == OpRemove) size += 8;
            }
            using (var w = new FastBufferWriter(size + 8, Allocator.Temp))
            {
                w.WriteValueSafe(s.id);
                w.WriteValueSafe((ushort)ops.Count);
                int b = 0;
                foreach (var op in ops)
                {
                    w.WriteValueSafe(op.type);
                    if (op.type == OpAdd || op.type == OpExtend)
                    {
                        if (op.type == OpExtend) w.WriteValueSafe(op.from);
                        byte[] bytes = scratchBytes[b++];
                        w.WriteValueSafe(bytes.Length);
                        w.WriteBytesSafe(bytes);
                    }
                    else if (op.type == OpRemove) { w.WriteValueSafe(op.key.owner); w.WriteValueSafe(op.key.id); }
                }
                if (isServer) nm.CustomMessagingManager.SendNamedMessageToAll(MOps, w, NetworkDelivery.ReliableFragmentedSequenced);
                else nm.CustomMessagingManager.SendNamedMessage(MOps, NetworkManager.ServerClientId, w, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        // ---------------------------------------------------------------- red -> local

        private void OnOps(ulong sender, FastBufferReader r)
        {
            if (mismatch) return;
            if (!isServer) { ProcessOps(sender, r, null); return; }
            // El servidor lee el mensaje entero a un arreglo (para reenviarlo tal cual) y lo procesa desde una copia.
            var raw = new byte[r.Length - r.Position];
            r.ReadBytesSafe(ref raw, raw.Length);
            using (var copy = new FastBufferReader(raw, Allocator.Temp)) ProcessOps(sender, copy, raw);
        }

        private void ProcessOps(ulong sender, FastBufferReader r, byte[] raw)
        {
            r.ReadValueSafe(out ushort cid);
            if (!byId.TryGetValue(cid, out State s) || s.canvas == null) return;
            r.ReadValueSafe(out ushort count);
            Debug.Log($"[NetCanvas] llegan {count} operaciones de {sender} para {s.canvas.name}");

            if (isServer && !ValidateSender(sender, s.canvas))
            {
                SendRejectAll(sender, s, r, count);
                return;
            }
            ApplyOps(s, r, count, out bool ok);
            if (!ok) return;
            if (isServer && raw != null) Relay(sender, raw);
        }

        private bool ValidateSender(ulong sender, DrawingCanvas canvas)
        {
            IPlayerContext p = PlayerRegistry.ById((int)sender);
            if (p == null || p.Root == null) return true; // sin avatar todavia: no se puede medir
            return Vector3.Distance(p.Root.position, canvas.WorldCenter) <= MaxReach;
        }

        private void Relay(ulong sender, byte[] raw)
        {
            var targets = new List<ulong>(nm.ConnectedClientsIds.Count);
            foreach (ulong id in nm.ConnectedClientsIds) if (id != NetworkManager.ServerClientId && id != sender) targets.Add(id);
            if (targets.Count == 0) return;
            using (var w = new FastBufferWriter(raw.Length + 4, Allocator.Temp))
            {
                w.WriteBytesSafe(raw);
                nm.CustomMessagingManager.SendNamedMessage(MOps, targets, w, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        private void SendRejectAll(ulong sender, State s, FastBufferReader r, ushort count)
        {
            // Se leen las operaciones para saber que trazos rechazar (los nuevos); los quitados no hace falta deshacerlos.
            for (int i = 0; i < count; i++)
            {
                r.ReadValueSafe(out byte type);
                if (type == OpAdd || type == OpExtend)
                {
                    if (type == OpExtend) r.ReadValueSafe(out int _);
                    r.ReadValueSafe(out int len);
                    var bytes = new byte[len];
                    r.ReadBytesSafe(ref bytes, len);
                    Stroke st = StrokeCodec.Decode(bytes);
                    if (st == null) continue;
                    using (var w = new FastBufferWriter(2 + 4 + 4, Allocator.Temp))
                    {
                        w.WriteValueSafe(s.id); w.WriteValueSafe(st.OwnerId); w.WriteValueSafe(st.StrokeId);
                        nm.CustomMessagingManager.SendNamedMessage(MRej, sender, w, NetworkDelivery.Reliable);
                    }
                }
                else if (type == OpRemove) { r.ReadValueSafe(out int _); r.ReadValueSafe(out uint _); }
            }
        }

        private void OnRejected(ulong sender, FastBufferReader r)
        {
            r.ReadValueSafe(out ushort cid);
            r.ReadValueSafe(out int owner);
            r.ReadValueSafe(out uint id);
            if (!byId.TryGetValue(cid, out State s) || s.canvas == null) return;
            var strokes = s.canvas.Session.Strokes;
            for (int i = strokes.Count - 1; i >= 0; i--)
                if (strokes[i].OwnerId == owner && strokes[i].StrokeId == id) { strokes.RemoveAt(i); s.canvas.Session.MarkChanged(); }
            SnapshotKnown(s);
            if (CanDraw(s.canvas)) s.canvas.Redraw();
            s.evalAt = Time.unscaledTime + EvalQuiet;
        }

        private void ApplyOps(State s, FastBufferReader r, ushort count, out bool ok)
        {
            ok = true;
            DrawingCanvas canvas = s.canvas;
            var session = canvas.Session;
            bool redraw = false;
            for (int n = 0; n < count; n++)
            {
                r.ReadValueSafe(out byte type);
                switch (type)
                {
                    case OpClear:
                        session.Clear();
                        s.known.Clear();
                        redraw = true;
                        break;
                    case OpRemove:
                    {
                        r.ReadValueSafe(out int owner); r.ReadValueSafe(out uint id);
                        for (int i = session.Strokes.Count - 1; i >= 0; i--)
                            if (session.Strokes[i].OwnerId == owner && session.Strokes[i].StrokeId == id) { session.Strokes.RemoveAt(i); session.MarkChanged(); }
                        for (int i = s.known.Count - 1; i >= 0; i--) if (s.known[i].owner == owner && s.known[i].id == id) s.known.RemoveAt(i);
                        redraw = true;
                        break;
                    }
                    case OpAdd:
                    case OpExtend:
                    {
                        int from = 0;
                        if (type == OpExtend) r.ReadValueSafe(out from);
                        r.ReadValueSafe(out int len);
                        if (len < 0 || len > 1 << 20) { ok = false; return; }
                        var bytes = new byte[len];
                        r.ReadBytesSafe(ref bytes, len);
                        Stroke st = StrokeCodec.Decode(bytes);
                        if (st == null || st.Points.Count > MaxPointsPerOp) break;
                        if (type == OpAdd) ApplyAdd(s, st, ref redraw);
                        else ApplyExtend(s, st, from);
                        break;
                    }
                    default: ok = false; return;
                }
            }
            if (redraw && CanDraw(canvas)) canvas.Redraw();
            s.evalAt = Time.unscaledTime + EvalQuiet;
        }

        /// <summary>
        /// Agrega un trazo que viene de la red CONSERVANDO su duenio: AddStroke le pone el duenio local a los que traen duenio 0 (los del anfitrion),
        /// y eso los haria pasar por trazos propios nuevos.
        /// </summary>
        private static void AddRemote(DrawingSession session, Stroke st)
        {
            int previous = session.OwnerForNewStrokes;
            session.SetOwnerForNewStrokes(0);
            session.AddStroke(st);
            session.SetOwnerForNewStrokes(previous);
        }

        private static bool CanDraw(DrawingCanvas c) => c != null && c.isActiveAndEnabled && c.GetRenderTexture() != null;

        private void ApplyAdd(State s, Stroke st, ref bool redraw)
        {
            var session = s.canvas.Session;
            foreach (var existing in session.Strokes) if (existing.OwnerId == st.OwnerId && existing.StrokeId == st.StrokeId) return; // ya estaba
            AddRemote(session, st);
            s.known.Add(new Known { owner = st.OwnerId, id = st.StrokeId, points = st.Points.Count });
            if (CanDraw(s.canvas)) for (int i = 0; i < st.Points.Count; i++) s.canvas.DrawAt(st.Points[i], i == 0, st.Width);
        }

        private void ApplyExtend(State s, Stroke part, int from)
        {
            var strokes = s.canvas.Session.Strokes;
            for (int i = 0; i < strokes.Count; i++)
            {
                Stroke st = strokes[i];
                if (st.OwnerId != part.OwnerId || st.StrokeId != part.StrokeId) continue;
                if (st.Points.Count != from) return; // se perdio algo: la proxima instantanea lo arregla
                for (int p = 0; p < part.Points.Count; p++)
                {
                    st.Points.Add(part.Points[p]);
                    if (CanDraw(s.canvas)) s.canvas.DrawAt(part.Points[p], false, st.Width);
                }
                for (int k = 0; k < s.known.Count; k++)
                    if (s.known[k].owner == st.OwnerId && s.known[k].id == st.StrokeId) { var kk = s.known[k]; kk.points = st.Points.Count; s.known[k] = kk; }
                return;
            }
        }

        // ---------------------------------------------------------------- entrada tardia

        /// <summary>Servidor: manda a un cliente nuevo todos los lienzos que tienen trazos.</summary>
        public void SendFull(ulong clientId)
        {
            var blobs = new List<KeyValuePair<ushort, byte[]>>();
            int size = 4 + 2;
            foreach (var s in states)
            {
                if (s.canvas == null || s.canvas.Session.IsEmpty) continue;
                byte[] bytes = StrokeCodec.EncodeSession(s.canvas.Session);
                blobs.Add(new KeyValuePair<ushort, byte[]>(s.id, bytes));
                size += 2 + 4 + bytes.Length;
            }
            using (var w = new FastBufferWriter(size + 8, Allocator.Temp))
            {
                w.WriteValueSafe(fingerprint);
                w.WriteValueSafe((ushort)blobs.Count);
                foreach (var kv in blobs)
                {
                    w.WriteValueSafe(kv.Key);
                    w.WriteValueSafe(kv.Value.Length);
                    w.WriteBytesSafe(kv.Value);
                }
                nm.CustomMessagingManager.SendNamedMessage(MFull, clientId, w, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        private void OnFull(ulong sender, FastBufferReader r)
        {
            if (isServer) return;
            r.ReadValueSafe(out uint remote);
            if (remote != fingerprint)
            {
                mismatch = true;
                Debug.LogError($"[NetCanvas] Los lienzos del anfitrion son distintos de los tuyos (huella {remote:X8} contra {fingerprint:X8}): el dibujo compartido queda apagado.");
                return;
            }
            r.ReadValueSafe(out ushort count);
            for (int i = 0; i < count; i++)
            {
                r.ReadValueSafe(out ushort cid);
                r.ReadValueSafe(out int len);
                var bytes = new byte[len];
                r.ReadBytesSafe(ref bytes, len);
                if (!byId.TryGetValue(cid, out State s) || s.canvas == null) continue;
                DrawingSession decoded = StrokeCodec.DecodeSession(bytes);
                if (decoded == null) continue;
                s.canvas.Session.Clear();
                foreach (var st in decoded.Strokes) AddRemote(s.canvas.Session, st);
                if (CanDraw(s.canvas)) s.canvas.Redraw();
                SnapshotKnown(s);
                s.evalAt = Time.unscaledTime + EvalQuiet;
            }
        }

        // ---------------------------------------------------------------- consulta (pruebas)

        /// <summary>Una cadena con la cantidad de trazos y de puntos de cada lienzo que tiene dibujo: sirve para comparar lo que ve cada maquina.</summary>
        public string Describe()
        {
            var sb = new System.Text.StringBuilder(256);
            foreach (var s in states)
            {
                if (s.canvas == null || s.canvas.Session.IsEmpty) continue;
                int points = 0;
                foreach (var st in s.canvas.Session.Strokes) points += st.Points.Count;
                sb.Append(s.id).Append(':').Append(s.canvas.Session.Strokes.Count).Append('/').Append(points).Append(';');
            }
            return sb.ToString();
        }
    }
}
