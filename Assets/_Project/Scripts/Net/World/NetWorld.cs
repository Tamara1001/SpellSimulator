using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using SpellSimulator.Core;
using SpellSimulator.Interfaces;
using SpellSimulator.Player;
using SpellSimulator.Reactions;
using SpellSimulator.World;

namespace SpellSimulator.Net
{
    /// <summary>
    /// Mundo compartido (F2 de MULTIPLAYER_PLAN): replica los cuerpos fisicos de la escena (cajas, herramientas, lienzos, runas, tinteros)
    /// entre el anfitrion y los clientes con mensajes propios de Netcode, sin NetworkObject por cuerpo (los objetos son de la escena, no prefabs).
    /// <list type="bullet">
    /// <item>El ANFITRION simula la fisica de lo suelto y manda las poses de lo que cambio a ~15 Hz (+ un cuadro completo rotativo por si se pierde un paquete).</item>
    /// <item>Quien AGARRA un objeto lo lleva con su propia fisica (el agarre empieza al instante) y avisa al servidor; si otro ya lo tenia, el servidor
    /// lo rechaza y se suelta. Mientras lo sostiene manda su pose a ~20 Hz; al soltar manda pose y velocidad y el servidor retoma la simulacion.</item>
    /// <item>Al entrar un cliente recibe el estado completo. Todos cargan la misma escena: el id de cada cuerpo sale del orden de la jerarquia y una
    /// huella avisa si las versiones del mundo difieren.</item>
    /// <item>Si el anfitrion se va, la partida termina (no hay guardado): <see cref="NetSession"/> avisa y el menu reinicia el mundo.</item>
    /// </list>
    /// Vive en el GameObject del NetworkManager (lo agrega <see cref="NetSession.CreateNetworkManager"/>) y solo actua en el NetworkManager singleton.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetWorld : MonoBehaviour
    {
        private const string MSpawn = "sw.spawn", MRemove = "sw.remove";
        private const string MPortalState = "pt.state", MPortalCross = "pt.cross", MPortalTp = "pt.tp";
        private const float PortalReach = 8f;
        private const ushort FirstDynamicId = 40000;
        private const ushort FirstDynamicCanvasId = 1000;

        /// <summary>Id del lienzo de un cuerpo creado en juego: se deriva del id del cuerpo (igual en todas las maquinas).</summary>
        public static ushort CanvasIdFor(ushort bodyId) => (ushort)(FirstDynamicCanvasId + (bodyId - FirstDynamicId));
        private ushort nextDynamicId = FirstDynamicId;
        private const string MFull = "sw.full", MSnap = "sw.snap", MGrab = "sw.grab", MDeny = "sw.deny", MHeld = "sw.held",
                             MHold = "sw.hold", MRelease = "sw.rel", MReset = "sw.reset", MResetReq = "sw.resetreq";
        private const float SnapInterval = 1f / 15f, HoldInterval = 1f / 20f, KeyframeInterval = 0.5f;
        private const int PosesPerPacket = 40, KeyframeBodies = 12;
        private const int PoseBytes = 2 + 12 + 8 + 1;

        public static NetWorld Active { get; private set; }

        // Los cuerpos que ya estaban al cargar la escena (antes de cualquier Start). Lo que se crea despues (reposicion, hechizos) no entra a la
        // lista: si no, el momento en que cada maquina la arma cambiaria los ids. Esos objetos se replican en F3 y F4.
        private static readonly Dictionary<int, string> sceneBodyKeys = new Dictionary<int, string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SnapshotSceneBodies()
        {
            sceneBodyKeys.Clear();
            foreach (var r in FindObjectsByType<Respawnable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (r != null) sceneBodyKeys[r.GetInstanceID()] = NetBodyMath.PathKey(r.transform);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { sceneBodyKeys.Clear(); Active = null; }

        private NetworkManager nm;
        private bool began, mismatch;
        private readonly List<NetBody> bodies = new List<NetBody>(256);
        private readonly Dictionary<ushort, NetBody> byId = new Dictionary<ushort, NetBody>(256);
        private readonly List<NetBody> scratch = new List<NetBody>(64);
        private uint fingerprint;
        private float snapTimer, holdTimer, keyframeTimer;
        private int keyframeCursor;
        private bool applyingNet, applyingReset;
        private PlayerInteractor interactor;
        private NetCanvasSync canvasSync;
        private NetCast castSync;
        private NetDuel duel;
        private NetReactiveSync reactiveSync;

        /// <summary>Dibujo compartido (trazos de los lienzos); null si no hay sesion.</summary>
        public NetCanvasSync Canvases => canvasSync;

        /// <summary>Lanzamiento compartido; null si no hay sesion.</summary>
        public NetCast Casts => castSync;

        /// <summary>Mundo reactivo compartido (fuego, humedad, quemado); null si no hay sesion.</summary>
        public NetReactiveSync Reactive => reactiveSync;

        public bool IsServer => nm != null && nm.IsServer;
        public bool Began => began;
        public int BodyCount => bodies.Count;
        public bool Mismatch => mismatch;
        /// <summary>Cruces de portal de clientes que el anfitrion autorizo / rechazo (pruebas y diagnostico).</summary>
        public int PortalCrossingsApproved { get; private set; }
        public int PortalCrossingsDenied { get; private set; }

        // ---------------------------------------------------------------- ciclo de vida

        private void Awake()
        {
            nm = GetComponent<NetworkManager>();
            if (nm == null) { enabled = false; return; }
            nm.OnServerStarted += Begin;
            nm.OnClientStarted += Begin;
            nm.OnServerStopped += OnStopped;
            nm.OnClientStopped += OnStopped;
            nm.OnClientConnectedCallback += OnClientConnected;
            nm.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void OnDestroy()
        {
            if (nm != null)
            {
                nm.OnServerStarted -= Begin;
                nm.OnClientStarted -= Begin;
                nm.OnServerStopped -= OnStopped;
                nm.OnClientStopped -= OnStopped;
                nm.OnClientConnectedCallback -= OnClientConnected;
                nm.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            if (Active == this) End();
        }

        private void Begin()
        {
            if (began || nm == null || nm != NetworkManager.Singleton) return;
            began = true;
            Active = this;
            mismatch = false;
            BuildRegistry();
            var cm = nm.CustomMessagingManager;
            cm.RegisterNamedMessageHandler(MFull, OnFull);
            cm.RegisterNamedMessageHandler(MSnap, OnSnap);
            cm.RegisterNamedMessageHandler(MGrab, OnGrab);
            cm.RegisterNamedMessageHandler(MDeny, OnDeny);
            cm.RegisterNamedMessageHandler(MHeld, OnHeldMessage);
            cm.RegisterNamedMessageHandler(MHold, OnHold);
            cm.RegisterNamedMessageHandler(MRelease, OnRelease);
            cm.RegisterNamedMessageHandler(MReset, OnResetMessage);
            cm.RegisterNamedMessageHandler(MResetReq, OnResetRequest);
            cm.RegisterNamedMessageHandler(MSpawn, OnSpawnMessage);
            cm.RegisterNamedMessageHandler(MRemove, OnRemoveMessage);
            cm.RegisterNamedMessageHandler(MPortalState, OnPortalStateMessage);
            cm.RegisterNamedMessageHandler(MPortalCross, OnPortalCrossMessage);
            cm.RegisterNamedMessageHandler(MPortalTp, OnPortalTeleportMessage);
            MagicWorld.WorldReset += OnWorldReset;
            canvasSync = new NetCanvasSync(nm);
            canvasSync.Begin();
            castSync = new NetCast(nm, canvasSync);
            castSync.Begin();
            reactiveSync = new NetReactiveSync(nm);
            reactiveSync.Begin();
            duel = new NetDuel(nm);
            duel.Begin();
            bool client = !nm.IsServer;
            foreach (var b in bodies)
            {
                if (client) { b.BecomeFollower(); if (b.TryGetComponent(out Respawnable r)) r.enabled = false; }
            }
            SupplyPoint.NetBegin(this, !client);
            Debug.Log($"[NetWorld] {(client ? "cliente" : "anfitrion")}: {bodies.Count} cuerpos, huella {fingerprint:X8}");
        }

        private void OnStopped(bool wasHost) => End();

        private void End()
        {
            if (!began && Active != this) return;
            Debug.Log("[NetWorld] fin de la sesion de mundo compartido:\n" + System.Environment.StackTrace);
            began = false;
            if (Active == this) Active = null;
            MagicWorld.WorldReset -= OnWorldReset;
            if (reactiveSync != null) { reactiveSync.End(); reactiveSync = null; }
            if (duel != null) { duel.End(); duel = null; }
            if (castSync != null) { castSync.End(); castSync = null; }
            if (canvasSync != null) { canvasSync.End(); canvasSync = null; }
            if (nm != null && nm.CustomMessagingManager != null)
            {
                var cm = nm.CustomMessagingManager;
                cm.UnregisterNamedMessageHandler(MFull); cm.UnregisterNamedMessageHandler(MSnap); cm.UnregisterNamedMessageHandler(MGrab);
                cm.UnregisterNamedMessageHandler(MDeny); cm.UnregisterNamedMessageHandler(MHeld); cm.UnregisterNamedMessageHandler(MHold);
                cm.UnregisterNamedMessageHandler(MRelease); cm.UnregisterNamedMessageHandler(MReset); cm.UnregisterNamedMessageHandler(MResetReq);
                cm.UnregisterNamedMessageHandler(MSpawn); cm.UnregisterNamedMessageHandler(MRemove);
                cm.UnregisterNamedMessageHandler(MPortalState); cm.UnregisterNamedMessageHandler(MPortalCross); cm.UnregisterNamedMessageHandler(MPortalTp);
            }
            applyingNet = true;
            foreach (var b in bodies)
            {
                if (b == null) continue;
                b.BecomeSimulated();
                b.LocalHeld = false; b.PendingRelease = false;
                if (b.Holdable != null && b.HeldBy != Holdable.NoOwner) b.Holdable.Release();
                if (b.TryGetComponent(out Respawnable r)) r.enabled = true;
            }
            applyingNet = false;
            PlayerRegistry.LocalOwnerId = 0;
            // los cuerpos quedan registrados con su Holdable; el siguiente Begin reconstruye la lista
            bool wasServer = nm != null && nm.IsServer;
            foreach (var b in bodies)
            {
                if (b == null) continue;
                bool replica = b.IsDynamic && !wasServer;
                b.SuppressDestroyNotify = true; // cerrar la sesion no es una baja del mundo: que no avise a nadie
                if (replica) Destroy(b.gameObject); else Destroy(b);
            }
            nextDynamicId = FirstDynamicId;
            bodies.Clear();
            byId.Clear();
        }

        /// <summary>Reunir los cuerpos de la escena (Rigidbody + Respawnable, sin el jugador), ordenados por su ruta, y darles un id.</summary>
        private void BuildRegistry()
        {
            bodies.Clear();
            byId.Clear();
            var found = FindObjectsByType<Respawnable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var keyed = new List<KeyValuePair<string, Respawnable>>(found.Length);
            for (int i = 0; i < found.Length; i++)
            {
                Respawnable r = found[i];
                if (r == null || !r.TryGetComponent(out Rigidbody _) || r.GetComponent<PlayerInputReader>() != null) continue;
                if (!sceneBodyKeys.TryGetValue(r.GetInstanceID(), out string key))
                {
                    if (sceneBodyKeys.Count > 0) continue; // creado despues de cargar la escena
                    key = NetBodyMath.PathKey(r.transform);
                }
                keyed.Add(new KeyValuePair<string, Respawnable>(key, r));
            }
            keyed.Sort((a, b) => NetBodyMath.CompareKeys(a.Key, b.Key));
            var keys = new List<string>(keyed.Count);
            for (int i = 0; i < keyed.Count && i < ushort.MaxValue; i++)
            {
                keys.Add(keyed[i].Key);
                GameObject go = keyed[i].Value.gameObject;
                NetBody body = go.TryGetComponent(out NetBody existing) ? existing : go.AddComponent<NetBody>();
                body.Init((ushort)i);
                bodies.Add(body);
                byId[(ushort)i] = body;
            }
            fingerprint = NetBodyMath.Fingerprint(keys);
            Debug.Log("[NetWorld] claves de los cuerpos (" + keys.Count + "):\n" + string.Join("\n", keys));
        }

        // ---------------------------------------------------------------- bucle

        private void FixedUpdate()
        {
            if (!began || mismatch) return;
            if (canvasSync != null) canvasSync.Tick(Time.fixedDeltaTime);
            if (reactiveSync != null) reactiveSync.Tick(Time.fixedDeltaTime);
            if (duel != null) duel.Tick(Time.fixedDeltaTime);
            if (nm.IsServer) ServerTick(); else ClientTick();
        }

        private void LateUpdate()
        {
            if (!began || mismatch || nm.IsServer) return;
            // El agarre se suelta antes de arrojar (la velocidad se pone despues): se avisa al final del cuadro con el valor definitivo.
            for (int i = 0; i < bodies.Count; i++)
            {
                NetBody b = bodies[i];
                if (b == null || !b.PendingRelease) continue;
                b.PendingRelease = false;
                SendRelease(b);
                b.GhostUntil = Time.time + NetBody.GhostSeconds;
            }
        }

        private void ServerTick()
        {
            if (nm.ConnectedClientsIds.Count <= 1) { snapTimer = holdTimer = 0f; return; }
            snapTimer += Time.fixedDeltaTime;
            keyframeTimer += Time.fixedDeltaTime;
            if (snapTimer < SnapInterval) return;
            snapTimer = 0f;

            scratch.Clear();
            for (int i = 0; i < bodies.Count; i++)
            {
                NetBody b = bodies[i];
                if (b == null) continue;
                Rigidbody rb = b.Rb;
                bool active = b.gameObject.activeSelf;
                if (!b.EverSent || active != b.LastSentActive || NetBodyMath.NeedsSend(b.LastSentPos, b.LastSentRot, rb.position, rb.rotation)) scratch.Add(b);
            }
            if (keyframeTimer >= KeyframeInterval && bodies.Count > 0)
            {
                keyframeTimer = 0f;
                for (int k = 0; k < KeyframeBodies; k++)
                {
                    NetBody b = bodies[keyframeCursor % bodies.Count];
                    keyframeCursor++;
                    if (b != null && !scratch.Contains(b)) scratch.Add(b);
                }
            }
            SendPoses(MSnap, scratch, NetworkManager.ServerClientId, true);
        }

        private void ClientTick()
        {
            holdTimer += Time.fixedDeltaTime;
            if (holdTimer < HoldInterval) return;
            holdTimer = 0f;
            scratch.Clear();
            for (int i = 0; i < bodies.Count; i++) if (bodies[i] != null && bodies[i].LocalHeld) scratch.Add(bodies[i]);
            if (scratch.Count > 0) SendPoses(MHold, scratch, NetworkManager.ServerClientId, false);
        }

        // ---------------------------------------------------------------- envio

        private static void WritePose(FastBufferWriter w, NetBody b)
        {
            Rigidbody rb = b.Rb;
            w.WriteValueSafe(b.Id);
            w.WriteValueSafe(rb.position);
            NetBodyMath.PackRotation(rb.rotation, out short x, out short y, out short z, out short ww);
            w.WriteValueSafe(x); w.WriteValueSafe(y); w.WriteValueSafe(z); w.WriteValueSafe(ww);
            w.WriteValueSafe((byte)(b.gameObject.activeSelf ? 1 : 0));
        }

        private struct Pose { public ushort id; public Vector3 pos; public Quaternion rot; public bool active; }

        private static Pose ReadPose(FastBufferReader r)
        {
            var p = new Pose();
            r.ReadValueSafe(out p.id);
            r.ReadValueSafe(out p.pos);
            r.ReadValueSafe(out short x); r.ReadValueSafe(out short y); r.ReadValueSafe(out short z); r.ReadValueSafe(out short w);
            r.ReadValueSafe(out byte flags);
            p.rot = NetBodyMath.UnpackRotation(x, y, z, w);
            p.active = (flags & 1) != 0;
            return p;
        }

        /// <summary>Manda las poses en paquetes de PosesPerPacket. toServer = false: del servidor a todos los clientes; true no se usa para clientes.</summary>
        private void SendPoses(string message, List<NetBody> list, ulong target, bool fromServer)
        {
            if (list.Count == 0) return;
            int chunks = NetBodyMath.ChunkCount(list.Count, PosesPerPacket);
            for (int c = 0; c < chunks; c++)
            {
                int from = c * PosesPerPacket;
                int n = Mathf.Min(PosesPerPacket, list.Count - from);
                using (var w = new FastBufferWriter(1 + n * PoseBytes, Allocator.Temp))
                {
                    w.WriteValueSafe((byte)n);
                    for (int i = 0; i < n; i++)
                    {
                        NetBody b = list[from + i];
                        WritePose(w, b);
                        if (fromServer)
                        {
                            b.LastSentPos = b.Rb.position; b.LastSentRot = b.Rb.rotation; b.LastSentActive = b.gameObject.activeSelf; b.EverSent = true;
                        }
                    }
                    if (fromServer) nm.CustomMessagingManager.SendNamedMessageToAll(message, w, NetworkDelivery.UnreliableSequenced);
                    else nm.CustomMessagingManager.SendNamedMessage(message, target, w, NetworkDelivery.UnreliableSequenced);
                }
            }
        }

        private void SendRelease(NetBody b)
        {
            using (var w = new FastBufferWriter(PoseBytes + 12 + 12 + 1, Allocator.Temp))
            {
                WritePose(w, b);
                Rigidbody rb = b.Rb;
                bool kin = rb.isKinematic;
                w.WriteValueSafe(kin ? Vector3.zero : rb.linearVelocity);
                w.WriteValueSafe(kin ? Vector3.zero : rb.angularVelocity);
                w.WriteValueSafe((byte)(b.TryGetComponent(out PinnedBody _) || IsFloatingCanvas(b) ? 1 : 0));
                nm.CustomMessagingManager.SendNamedMessage(MRelease, NetworkManager.ServerClientId, w, NetworkDelivery.Reliable);
            }
        }

        private static bool IsFloatingCanvas(NetBody b) => b.TryGetComponent(out PhysicalCanvas canvas) && canvas.IsFloating;

        private void SendHeld(NetBody b, int owner)
        {
            using (var w = new FastBufferWriter(2 + 4, Allocator.Temp))
            {
                w.WriteValueSafe(b.Id);
                w.WriteValueSafe(owner);
                nm.CustomMessagingManager.SendNamedMessageToAll(MHeld, w, NetworkDelivery.Reliable);
            }
        }

        private void SendFull(ulong clientId)
        {
            int held = 0;
            var dynamics = new List<NetBody>();
            for (int i = 0; i < bodies.Count; i++)
            {
                if (bodies[i] == null) continue;
                if (bodies[i].HeldBy != Holdable.NoOwner) held++;
                if (bodies[i].IsDynamic) dynamics.Add(bodies[i]);
            }
            var payloads = new List<byte[]>(dynamics.Count);
            int spawnBytes = 0;
            foreach (var d in dynamics) { byte[] p = CurrentPayload(d); payloads.Add(p); spawnBytes += 2 + 1 + 4 + p.Length; }
            int size = 4 + 2 + spawnBytes + 2 + bodies.Count * PoseBytes + 2 + held * 6;
            using (var w = new FastBufferWriter(size + 16, Allocator.Temp))
            {
                w.WriteValueSafe(fingerprint);
                w.WriteValueSafe((ushort)dynamics.Count);
                for (int i = 0; i < dynamics.Count; i++)
                {
                    w.WriteValueSafe(dynamics[i].Id);
                    w.WriteValueSafe(dynamics[i].SpawnKind);
                    w.WriteValueSafe(payloads[i].Length);
                    w.WriteBytesSafe(payloads[i]);
                }
                w.WriteValueSafe((ushort)bodies.Count);
                for (int i = 0; i < bodies.Count; i++) WritePose(w, bodies[i]);
                w.WriteValueSafe((ushort)held);
                for (int i = 0; i < bodies.Count; i++)
                {
                    NetBody b = bodies[i];
                    if (b == null || b.HeldBy == Holdable.NoOwner) continue;
                    w.WriteValueSafe(b.Id);
                    w.WriteValueSafe(b.HeldBy);
                }
                nm.CustomMessagingManager.SendNamedMessage(MFull, clientId, w, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        /// <summary>Datos para armar el cuerpo creado en juego tal como esta ahora (pose actual y cuanto lleva formandose).</summary>
        private static byte[] CurrentPayload(NetBody b)
        {
            if ((NetSpawnKind)b.SpawnKind == NetSpawnKind.Rock && NetSpawn.TryDecodeRock(b.SpawnPayload, out NetSpawn.RockData d))
            {
                d.pos = b.Rb.position;
                d.rot = b.Rb.rotation;
                d.elapsed = Mathf.Max(0f, Time.time - b.SpawnedAt);
                return NetSpawn.EncodeRock(d);
            }
            return b.SpawnPayload ?? new byte[0];
        }

        // ---------------------------------------------------------------- conexion

        private void OnClientConnected(ulong clientId)
        {
            if (!began) return;
            if (clientId == nm.LocalClientId) { PlayerRegistry.LocalOwnerId = (int)clientId; if (canvasSync != null) canvasSync.RefreshOwner(); }
            if (nm.IsServer && clientId != NetworkManager.ServerClientId)
            {
                SendFull(clientId);
                if (canvasSync != null) canvasSync.SendFull(clientId);
                if (reactiveSync != null) reactiveSync.SendFull(clientId);
                foreach (var portal in Portal.All) if (portal != null && portal.State != PortalState.Dormant) SendPortalState(portal, clientId);
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (!began || !nm.IsServer) return;
            // lo que sostenia un jugador que se fue se suelta donde esta
            for (int i = 0; i < bodies.Count; i++)
            {
                NetBody b = bodies[i];
                if (b != null && b.HeldBy == (int)clientId) ServerFinishHold(b, b.Rb.position, b.Rb.rotation, Vector3.zero, Vector3.zero, false);
            }
        }

        // ---------------------------------------------------------------- agarre (hooks locales)

        /// <summary>Lo llama <see cref="NetBody"/> cuando cambia quien sostiene el objeto (por una accion local o por la red).</summary>
        public void OnLocalHeldChanged(NetBody body, int owner)
        {
            if (!began || mismatch || applyingNet) return;
            int me = PlayerRegistry.LocalOwnerId;
            if (nm.IsServer)
            {
                // El anfitrion manda sobre todo: anuncia a todos el nuevo estado.
                SendHeld(body, owner);
                return;
            }
            if (owner == me)
            {
                body.LocalHeld = true;
                body.SuppressRelease = false;
                using (var w = new FastBufferWriter(2, Allocator.Temp))
                {
                    w.WriteValueSafe(body.Id);
                    nm.CustomMessagingManager.SendNamedMessage(MGrab, NetworkManager.ServerClientId, w, NetworkDelivery.Reliable);
                }
            }
            else if (owner == Holdable.NoOwner && body.LocalHeld)
            {
                body.LocalHeld = false;
                if (!body.SuppressRelease) body.PendingRelease = true;
                body.SuppressRelease = false;
            }
        }

        /// <summary>El interactor consulta antes de agarrar: rechaza lo que ya sostiene otro y deja el cuerpo listo para simularse aca.</summary>
        public static bool AllowLocalGrab(GameObject target)
        {
            NetWorld w = Active;
            if (w == null || !w.began || w.mismatch || target == null) return true;
            NetBody body = target.GetComponentInParent<NetBody>();
            if (body == null) return true;
            int held = body.HeldBy;
            if (held != Holdable.NoOwner && held != PlayerRegistry.LocalOwnerId) return false;
            if (!w.IsServer) { body.BecomeSimulated(); body.GhostUntil = Time.time + 1f; }
            return true;
        }

        private void ForceRelease(NetBody body)
        {
            body.SuppressRelease = true;
            if (interactor == null) interactor = FindFirstObjectByType<PlayerInteractor>();
            if (interactor != null) interactor.ForceRelease(body.gameObject);
            body.LocalHeld = false;
            body.PendingRelease = false;
            body.SuppressRelease = false;
            body.BecomeFollower();
        }

        // ---------------------------------------------------------------- mensajes: servidor

        private void OnGrab(ulong sender, FastBufferReader r)
        {
            if (!began || !nm.IsServer) return;
            r.ReadValueSafe(out ushort id);
            byId.TryGetValue(id, out NetBody body);
            float distance = 0f;
            if (body != null)
            {
                IPlayerContext p = PlayerRegistry.ById((int)sender);
                if (p != null && p.Root != null) distance = Vector3.Distance(p.Root.position, body.Rb.position);
            }
            GrabVerdict verdict = NetBodyMath.ValidateGrab(body != null, body != null ? body.HeldBy : Holdable.NoOwner, Holdable.NoOwner, (int)sender, distance);
            if (verdict != GrabVerdict.Ok)
            {
                using (var w = new FastBufferWriter(2, Allocator.Temp))
                {
                    w.WriteValueSafe(id);
                    nm.CustomMessagingManager.SendNamedMessage(MDeny, sender, w, NetworkDelivery.Reliable);
                }
                return;
            }
            if (body.HeldBy == (int)sender) return; // pedido repetido
            applyingNet = true;
            PinnedBody.Unpin(body.Rb);
            body.Holdable.SetHeldBy((int)sender);
            applyingNet = false;
            IRemoteGrabbable grabbable = body.GetComponentInParent<IRemoteGrabbable>();
            if (grabbable != null) grabbable.OnRemoteGrab();
            body.BecomeRemoteDriven();
            SendHeld(body, (int)sender);
        }

        private void OnHold(ulong sender, FastBufferReader r)
        {
            if (!began || !nm.IsServer) return;
            r.ReadValueSafe(out byte n);
            for (int i = 0; i < n; i++)
            {
                Pose p = ReadPose(r);
                if (!byId.TryGetValue(p.id, out NetBody b) || b.CurrentMode != NetBody.Mode.RemoteDriven || b.HeldBy != (int)sender) continue;
                b.SetTarget(p.pos, p.rot, false);
                if (b.gameObject.activeSelf != p.active) b.gameObject.SetActive(p.active);
            }
        }

        private void OnRelease(ulong sender, FastBufferReader r)
        {
            if (!began || !nm.IsServer) return;
            Pose p = ReadPose(r);
            r.ReadValueSafe(out Vector3 vel);
            r.ReadValueSafe(out Vector3 ang);
            r.ReadValueSafe(out byte flags);
            if (!byId.TryGetValue(p.id, out NetBody b) || b.HeldBy != (int)sender) return;
            if (b.gameObject.activeSelf != p.active) b.gameObject.SetActive(p.active);
            ServerFinishHold(b, p.pos, p.rot, vel, ang, (flags & 1) != 0);
        }

        /// <summary>El servidor retoma un cuerpo que sostenia un cliente: pose final, fisica propia y, si hace falta, flotando.</summary>
        private void ServerFinishHold(NetBody b, Vector3 pos, Quaternion rot, Vector3 vel, Vector3 ang, bool pin)
        {
            Rigidbody rb = b.Rb;
            if (rb.isKinematic) { rb.position = pos; rb.rotation = rot; b.transform.SetPositionAndRotation(pos, rot); }
            applyingNet = true;
            b.Holdable.Release();
            applyingNet = false;
            IRemoteGrabbable grabbable = b.GetComponentInParent<IRemoteGrabbable>();
            if (grabbable != null)
            {
                b.MarkSimulated();
                grabbable.OnRemoteRelease(pin);
            }
            else
            {
                b.BecomeSimulated();
                if (b.TryGetComponent(out IInteractable interactable)) interactable.OnDrop();
                if (pin) PinnedBody.Pin(rb, b.OriginalKinematic, b.OriginalGravity);
            }
            if (!rb.isKinematic) { rb.linearVelocity = vel; rb.angularVelocity = ang; }
            b.EverSent = false; // que el proximo cuadro lo mande entero
            SendHeld(b, Holdable.NoOwner);
        }

        private void OnResetRequest(ulong sender, FastBufferReader r)
        {
            if (!began || !nm.IsServer) return;
            var world = FindFirstObjectByType<MagicWorld>();
            if (world != null) world.ResetWorld();
        }

        // ---------------------------------------------------------------- portales

        private static byte[] EncodePortal(string id, byte state, string destId, bool origin, float elapsed)
        {
            using (var ms = new System.IO.MemoryStream(64))
            using (var w = new System.IO.BinaryWriter(ms))
            {
                w.Write(id ?? ""); w.Write(state); w.Write(destId ?? ""); w.Write(origin); w.Write(elapsed);
                w.Flush();
                return ms.ToArray();
            }
        }

        private void SendBlob(string message, List<ulong> targets, byte[] bytes)
        {
            if (targets.Count == 0) return;
            using (var w = new FastBufferWriter(4 + bytes.Length + 8, Allocator.Temp))
            {
                w.WriteValueSafe(bytes.Length);
                w.WriteBytesSafe(bytes);
                nm.CustomMessagingManager.SendNamedMessage(message, targets, w, NetworkDelivery.Reliable);
            }
        }

        private static byte[] ReadBlob(FastBufferReader r)
        {
            r.ReadValueSafe(out int len);
            if (len < 0 || len > 1024) return null;
            var bytes = new byte[len];
            r.ReadBytesSafe(ref bytes, len);
            return bytes;
        }

        /// <summary>Lo llama <see cref="Portal"/> al cambiar de estado: el anfitrion avisa a los clientes.</summary>
        public void OnPortalState(Portal portal)
        {
            if (!began || !nm.IsServer) return;
            SendPortalState(portal, null);
        }

        private void SendPortalState(Portal portal, ulong? only)
        {
            var targets = new List<ulong>(4);
            if (only.HasValue) targets.Add(only.Value); else targets = ClientTargets();
            SendBlob(MPortalState, targets, EncodePortal(portal.Id, (byte)portal.State, portal.DestinationId, portal.IsRouteOrigin, portal.SecondsInState));
        }

        private void OnPortalStateMessage(ulong sender, FastBufferReader r)
        {
            if (!began || nm.IsServer) return;
            byte[] bytes = ReadBlob(r);
            if (bytes == null) return;
            try
            {
                using (var ms = new System.IO.MemoryStream(bytes))
                using (var br = new System.IO.BinaryReader(ms))
                {
                    string id = br.ReadString(); byte state = br.ReadByte(); string destId = br.ReadString(); bool origin = br.ReadBoolean(); float elapsed = br.ReadSingle();
                    Portal portal = Portal.Find(id);
                    if (portal == null || state > (byte)PortalState.Cooldown) return;
                    portal.NetApply((PortalState)state, Portal.Find(destId), origin, elapsed);
                }
            }
            catch (System.IO.EndOfStreamException) { }
        }

        /// <summary>Cliente: pide cruzar un portal (lo valida el anfitrion).</summary>
        public void RequestPortalCross(Portal portal)
        {
            if (!began || nm.IsServer || portal == null) return;
            byte[] bytes = EncodePortal(portal.Id, 0, "", false, 0f);
            using (var w = new FastBufferWriter(4 + bytes.Length + 8, Allocator.Temp))
            {
                w.WriteValueSafe(bytes.Length);
                w.WriteBytesSafe(bytes);
                nm.CustomMessagingManager.SendNamedMessage(MPortalCross, NetworkManager.ServerClientId, w, NetworkDelivery.Reliable);
            }
        }

        private void OnPortalCrossMessage(ulong sender, FastBufferReader r)
        {
            if (!began || !nm.IsServer) return;
            byte[] bytes = ReadBlob(r);
            if (bytes == null) return;
            string id;
            try { using (var ms = new System.IO.MemoryStream(bytes)) using (var br = new System.IO.BinaryReader(ms)) id = br.ReadString(); }
            catch (System.IO.EndOfStreamException) { return; }
            Portal portal = Portal.Find(id);
            if (portal == null || portal.State != PortalState.Open || portal.Destination == null || portal.Destination.Arrival == null) { PortalCrossingsDenied++; return; }
            IPlayerContext traveler = PlayerRegistry.ById((int)sender);
            if (traveler == null || traveler.Root == null) { PortalCrossingsDenied++; return; }
            Vector3 at = portal.CrossTrigger != null ? portal.CrossTrigger.bounds.center : portal.transform.position;
            if (Vector3.Distance(traveler.Root.position, at) > PortalReach) { PortalCrossingsDenied++; return; }
            if (!PortalTravelerCooldowns.Ready((int)sender, GameClock.Time)) { PortalCrossingsDenied++; return; } // el enfriamiento es por jugador
            PortalCrossingsApproved++;
            PortalTravelerCooldowns.Mark((int)sender, GameClock.Time, portal.TravelerCooldown);
            var target = new List<ulong>(1) { sender };
            SendBlob(MPortalTp, target, EncodePortal(portal.Id, 0, portal.DestinationId, false, 0f));
            Debug.Log($"[Portal] {portal.Id} -> {portal.DestinationId}: cruce de {sender} autorizado");
        }

        private void OnPortalTeleportMessage(ulong sender, FastBufferReader r)
        {
            if (!began || nm.IsServer) return;
            byte[] bytes = ReadBlob(r);
            if (bytes == null) return;
            try
            {
                using (var ms = new System.IO.MemoryStream(bytes))
                using (var br = new System.IO.BinaryReader(ms))
                {
                    string id = br.ReadString(); br.ReadByte(); string destId = br.ReadString();
                    Portal origin = Portal.Find(id), dest = Portal.Find(destId);
                    if (origin == null || dest == null) return;
                    PortalTravelerCooldowns.Mark(PlayerRegistry.LocalOwnerId, GameClock.Time, origin.TravelerCooldown);
                    origin.NetTeleportLocal(dest);
                }
            }
            catch (System.IO.EndOfStreamException) { }
        }

        // ---------------------------------------------------------------- cuerpos creados en juego

        /// <summary>Servidor: registra un cuerpo creado durante el juego (roca) y avisa a los clientes para que armen su copia.</summary>
        public ushort RegisterSpawned(GameObject go, NetSpawnKind kind, byte[] payload)
        {
            if (!began || !nm.IsServer || go == null || nextDynamicId >= ushort.MaxValue - 1) return 0;
            if (!go.TryGetComponent(out Rigidbody _)) return 0;
            ushort id = nextDynamicId++;
            NetBody body = go.TryGetComponent(out NetBody existing) ? existing : go.AddComponent<NetBody>();
            body.Init(id);
            if (kind == NetSpawnKind.Rock) body.SetOriginals(false, true); // una roca formada es un cuerpo normal con gravedad
            body.IsDynamic = true;
            body.SpawnKind = (byte)kind;
            body.SpawnPayload = payload;
            body.SpawnedAt = Time.time;
            bodies.Add(body);
            byId[id] = body;
            RegisterBodyCanvas(body);
            var targets = ClientTargets();
            if (targets.Count > 0)
            {
                using (var w = new FastBufferWriter(2 + 1 + 4 + payload.Length + 8, Allocator.Temp))
                {
                    w.WriteValueSafe(id);
                    w.WriteValueSafe((byte)kind);
                    w.WriteValueSafe(payload.Length);
                    w.WriteBytesSafe(payload);
                    nm.CustomMessagingManager.SendNamedMessage(MSpawn, targets, w, NetworkDelivery.Reliable);
                }
            }
            return id;
        }

        /// <summary>Servidor: un clon de plantilla (suministro, reciclaje) se replica: los clientes clonan la misma plantilla en la misma pose.</summary>
        public ushort RegisterClone(GameObject clone, GameObject template, int ownerId = 0)
        {
            if (!began || !nm.IsServer || clone == null || template == null) return 0;
            return RegisterSpawned(clone, NetSpawnKind.Clone, NetSpawn.EncodeClone(new NetSpawn.CloneData
            {
                templateKey = NetTemplates.KeyOf(template), pos = clone.transform.position, rot = clone.transform.rotation, scale = clone.transform.localScale, owner = ownerId
            }));
        }

        private void RegisterBodyCanvas(NetBody body)
        {
            if (canvasSync == null || !body.TryGetComponent(out Canvas.DrawingCanvas dc)) return;
            canvasSync.RegisterDynamic(dc, CanvasIdFor(body.Id));
            body.HasDynamicCanvas = true;
        }

        private List<ulong> ClientTargets()
        {
            var targets = new List<ulong>(nm.ConnectedClientsIds.Count);
            foreach (ulong id in nm.ConnectedClientsIds) if (id != NetworkManager.ServerClientId) targets.Add(id);
            return targets;
        }

        /// <summary>Lo llama <see cref="NetBody"/> al destruirse un cuerpo (reciclaje, expiracion, tope de objetos): sale de la lista y el servidor avisa a los clientes.</summary>
        public void OnBodyDestroyed(NetBody body)
        {
            if (!began) return;
            if (body.HasDynamicCanvas && canvasSync != null) canvasSync.UnregisterDynamic(CanvasIdFor(body.Id));
            bodies.Remove(body);
            byId.Remove(body.Id);
            if (!nm.IsServer) return;
            var targets = ClientTargets();
            if (targets.Count == 0) return;
            using (var w = new FastBufferWriter(2, Allocator.Temp))
            {
                w.WriteValueSafe(body.Id);
                nm.CustomMessagingManager.SendNamedMessage(MRemove, targets, w, NetworkDelivery.Reliable);
            }
        }

        private void OnSpawnMessage(ulong sender, FastBufferReader r)
        {
            if (!began || nm.IsServer || mismatch) return;
            r.ReadValueSafe(out ushort id);
            r.ReadValueSafe(out byte kind);
            r.ReadValueSafe(out int len);
            if (len < 0 || len > 4096) return;
            var bytes = new byte[len];
            r.ReadBytesSafe(ref bytes, len);
            CreateReplica(id, (NetSpawnKind)kind, bytes);
        }

        private void OnRemoveMessage(ulong sender, FastBufferReader r)
        {
            if (!began || nm.IsServer) return;
            r.ReadValueSafe(out ushort id);
            if (!byId.TryGetValue(id, out NetBody b) || b == null) return;
            if (b.LocalHeld) ForceRelease(b);
            if (b.HasDynamicCanvas && canvasSync != null) canvasSync.UnregisterDynamic(CanvasIdFor(id));
            bodies.Remove(b);
            byId.Remove(id);
            b.SuppressDestroyNotify = true;
            Destroy(b.gameObject);
        }

        /// <summary>Cliente: arma la copia de un cuerpo que creo el anfitrion y la deja siguiendo sus poses.</summary>
        private void CreateReplica(ushort id, NetSpawnKind kind, byte[] payload)
        {
            if (byId.ContainsKey(id)) return;
            GameObject go = NetSpawn.Create(kind, payload);
            if (go == null) return;
            NetBody body = go.TryGetComponent(out NetBody existing) ? existing : go.AddComponent<NetBody>(); // un clon de plantilla hereda el NetBody de la plantilla
            body.Init(id);
            if (kind == NetSpawnKind.Rock) body.SetOriginals(false, true);
            body.IsDynamic = true;
            body.SpawnKind = (byte)kind;
            body.SpawnPayload = payload;
            body.SpawnedAt = Time.time;
            bodies.Add(body);
            byId[id] = body;
            RegisterBodyCanvas(body);
            body.BecomeFollower();
            body.SetTarget(go.transform.position, go.transform.rotation, true);
        }

        // ---------------------------------------------------------------- mensajes: cliente

        private void OnFull(ulong sender, FastBufferReader r)
        {
            if (!began || nm.IsServer) return;
            r.ReadValueSafe(out uint remoteFingerprint);
            if (remoteFingerprint != fingerprint)
            {
                mismatch = true;
                Debug.LogError($"[NetWorld] El mundo del anfitrion es distinto del tuyo (huella {remoteFingerprint:X8} contra {fingerprint:X8}): usen la misma version del juego. La replicacion de objetos queda apagada.");
                return;
            }
            r.ReadValueSafe(out ushort spawnCount);
            for (int i = 0; i < spawnCount; i++)
            {
                r.ReadValueSafe(out ushort sid);
                r.ReadValueSafe(out byte skind);
                r.ReadValueSafe(out int slen);
                if (slen < 0 || slen > 4096) return;
                var sbytes = new byte[slen];
                r.ReadBytesSafe(ref sbytes, slen);
                CreateReplica(sid, (NetSpawnKind)skind, sbytes);
            }
            r.ReadValueSafe(out ushort count);
            for (int i = 0; i < count; i++) ApplyPose(ReadPose(r), true);
            r.ReadValueSafe(out ushort heldCount);
            for (int i = 0; i < heldCount; i++)
            {
                r.ReadValueSafe(out ushort id);
                r.ReadValueSafe(out int owner);
                ApplyHeld(id, owner);
            }
        }

        private void OnSnap(ulong sender, FastBufferReader r)
        {
            if (!began || nm.IsServer || mismatch) return;
            r.ReadValueSafe(out byte n);
            for (int i = 0; i < n; i++) ApplyPose(ReadPose(r), false);
        }

        private void ApplyPose(Pose p, bool snap)
        {
            if (!byId.TryGetValue(p.id, out NetBody b) || b == null) return;
            if (b.LocalHeld || b.CurrentMode == NetBody.Mode.Simulated) return;
            if (b.gameObject.activeSelf != p.active) b.gameObject.SetActive(p.active);
            if (b.CurrentMode != NetBody.Mode.Follower) b.BecomeFollower();
            b.SetTarget(p.pos, p.rot, snap);
        }

        private void OnHeldMessage(ulong sender, FastBufferReader r)
        {
            if (!began || nm.IsServer || mismatch) return;
            r.ReadValueSafe(out ushort id);
            r.ReadValueSafe(out int owner);
            ApplyHeld(id, owner);
        }

        private void ApplyHeld(ushort id, int owner)
        {
            if (!byId.TryGetValue(id, out NetBody b) || b == null) return;
            int me = PlayerRegistry.LocalOwnerId;
            if (owner == me) return; // confirmacion de lo que ya sostengo
            if (b.LocalHeld) ForceRelease(b); // el servidor decidio otra cosa (lo tiene otro, o se reinicio el mundo)
            applyingNet = true;
            if (owner == Holdable.NoOwner) b.Holdable.Release(); else b.Holdable.SetHeldBy(owner);
            applyingNet = false;
        }

        private void OnDeny(ulong sender, FastBufferReader r)
        {
            if (!began || nm.IsServer) return;
            r.ReadValueSafe(out ushort id);
            if (byId.TryGetValue(id, out NetBody b) && b != null && b.LocalHeld) ForceRelease(b);
        }

        private void OnResetMessage(ulong sender, FastBufferReader r)
        {
            if (!began || nm.IsServer) return;
            var world = FindFirstObjectByType<MagicWorld>();
            if (world == null) return;
            applyingReset = true;
            world.ResetWorld();
            applyingReset = false;
        }

        // ---------------------------------------------------------------- reinicio del mundo

        /// <summary>Lo llama <c>MagicWorld.ResetWorld</c> al empezar: un cliente no reinicia por su cuenta, se lo pide al servidor.</summary>
        public static bool InterceptReset()
        {
            NetWorld w = Active;
            if (w == null || !w.began || w.mismatch || w.applyingReset || w.nm.IsServer) return false;
            using (var wr = new FastBufferWriter(1, Allocator.Temp))
            {
                wr.WriteValueSafe((byte)1);
                w.nm.CustomMessagingManager.SendNamedMessage(MResetReq, NetworkManager.ServerClientId, wr, NetworkDelivery.Reliable);
            }
            return true;
        }

        private void OnWorldReset()
        {
            if (!began || applyingReset) return;
            if (!nm.IsServer) return;
            // Servidor: lo que sostenian los clientes se suelta y todos reinician.
            for (int i = 0; i < bodies.Count; i++)
            {
                NetBody b = bodies[i];
                if (b == null) continue;
                if (b.CurrentMode == NetBody.Mode.RemoteDriven)
                {
                    applyingNet = true; b.Holdable.Release(); applyingNet = false;
                    b.BecomeSimulated();
                }
                b.EverSent = false;
            }
            using (var w = new FastBufferWriter(1, Allocator.Temp))
            {
                w.WriteValueSafe((byte)1);
                nm.CustomMessagingManager.SendNamedMessageToAll(MReset, w, NetworkDelivery.Reliable);
            }
        }

        // ---------------------------------------------------------------- consulta (pruebas)

        /// <summary>Posicion del cuerpo con ese nombre (null si no hay): para que los bots y el harness comparen lo que ve cada maquina.</summary>
        public bool TryGetBody(string objectName, out NetBody body)
        {
            for (int i = 0; i < bodies.Count; i++)
                if (bodies[i] != null && bodies[i].name == objectName) { body = bodies[i]; return true; }
            body = null;
            return false;
        }

        public IReadOnlyList<NetBody> Bodies => bodies;
    }
}
