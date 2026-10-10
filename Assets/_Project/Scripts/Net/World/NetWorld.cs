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

        public bool IsServer => nm != null && nm.IsServer;
        public bool Began => began;
        public int BodyCount => bodies.Count;
        public bool Mismatch => mismatch;

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
            MagicWorld.WorldReset += OnWorldReset;
            bool client = !nm.IsServer;
            foreach (var b in bodies)
            {
                if (client) { b.BecomeFollower(); if (b.TryGetComponent(out Respawnable r)) r.enabled = false; }
            }
            Debug.Log($"[NetWorld] {(client ? "cliente" : "anfitrion")}: {bodies.Count} cuerpos, huella {fingerprint:X8}");
        }

        private void OnStopped(bool wasHost) => End();

        private void End()
        {
            if (!began && Active != this) return;
            began = false;
            if (Active == this) Active = null;
            MagicWorld.WorldReset -= OnWorldReset;
            if (nm != null && nm.CustomMessagingManager != null)
            {
                var cm = nm.CustomMessagingManager;
                cm.UnregisterNamedMessageHandler(MFull); cm.UnregisterNamedMessageHandler(MSnap); cm.UnregisterNamedMessageHandler(MGrab);
                cm.UnregisterNamedMessageHandler(MDeny); cm.UnregisterNamedMessageHandler(MHeld); cm.UnregisterNamedMessageHandler(MHold);
                cm.UnregisterNamedMessageHandler(MRelease); cm.UnregisterNamedMessageHandler(MReset); cm.UnregisterNamedMessageHandler(MResetReq);
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
            foreach (var b in bodies) if (b != null) Destroy(b);
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
            for (int i = 0; i < bodies.Count; i++) if (bodies[i] != null && bodies[i].HeldBy != Holdable.NoOwner) held++;
            int size = 4 + 2 + bodies.Count * PoseBytes + 2 + held * 6;
            using (var w = new FastBufferWriter(size, Allocator.Temp))
            {
                w.WriteValueSafe(fingerprint);
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

        // ---------------------------------------------------------------- conexion

        private void OnClientConnected(ulong clientId)
        {
            if (!began) return;
            if (clientId == nm.LocalClientId) PlayerRegistry.LocalOwnerId = (int)clientId;
            if (nm.IsServer && clientId != NetworkManager.ServerClientId) SendFull(clientId);
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
