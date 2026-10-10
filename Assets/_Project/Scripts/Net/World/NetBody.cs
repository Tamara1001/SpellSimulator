using UnityEngine;
using SpellSimulator.World;

namespace SpellSimulator.Net
{
    /// <summary>
    /// Un cuerpo del mundo replicado (F2 de MULTIPLAYER_PLAN). Lo agrega <see cref="NetWorld"/> en runtime a cada objeto con
    /// Rigidbody + Respawnable, con el mismo id en todas las maquinas (orden de la jerarquia). Modos:
    /// <list type="bullet">
    /// <item><b>Simulated</b>: su fisica corre en esta maquina (el anfitrion para lo suelto; el jugador que lo sostiene).</item>
    /// <item><b>Follower</b>: cinematico; sigue las poses que manda el servidor (clientes, lo que no sostienen).</item>
    /// <item><b>RemoteDriven</b>: solo en el servidor: lo sostiene un cliente, el cuerpo sigue las poses que ese cliente manda.</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetBody : MonoBehaviour
    {
        public enum Mode { Simulated, Follower, RemoteDriven }

        private const float FollowRate = 18f;
        /// <summary>Tiempo (s) que un cliente sigue simulando lo que acaba de soltar antes de volver a seguir al servidor (evita que se congele un instante).</summary>
        public const float GhostSeconds = 0.35f;

        public ushort Id { get; private set; }
        public Rigidbody Rb { get; private set; }
        public Holdable Holdable { get; private set; }
        public Mode CurrentMode { get; private set; } = Mode.Simulated;

        /// <summary>Estado fisico original (lo que hay que devolver cuando alguien lo suelta).</summary>
        public bool OriginalKinematic { get; private set; }
        public bool OriginalGravity { get; private set; }
        private float originalDrag, originalAngularDrag;
        private CollisionDetectionMode originalDetection;

        // Seguimiento.
        private Vector3 targetPos;
        private Quaternion targetRot = Quaternion.identity;
        private bool hasTarget;

        // Ultimo enviado por el servidor (para no repetir lo que no cambio).
        public Vector3 LastSentPos;
        public Quaternion LastSentRot = Quaternion.identity;
        public bool LastSentActive = true;
        public bool EverSent;

        // Estado de agarre local (cliente).
        public bool LocalHeld;
        public bool SuppressRelease;
        public bool PendingRelease;
        public float GhostUntil;

        // Cuerpos creados en juego (rocas): el anfitrion los registra con un tipo y datos; los clientes arman una copia.
        public bool IsDynamic;
        /// <summary>El cuerpo tiene un lienzo registrado en el dibujo compartido (se quita al destruirse).</summary>
        public bool HasDynamicCanvas;
        /// <summary>No avisar al destruirse (cierre de la sesion: no es una baja del mundo).</summary>
        public bool SuppressDestroyNotify;
        public byte SpawnKind;
        public byte[] SpawnPayload;
        public float SpawnedAt;

        /// <summary>Fisica a la que vuelve el cuerpo cuando alguien lo suelta (para lo creado en juego no es la del momento de registrarlo: una roca se forma cinematica).</summary>
        public void SetOriginals(bool kinematic, bool gravity)
        {
            OriginalKinematic = kinematic;
            OriginalGravity = gravity;
        }

        public int HeldBy => Holdable != null ? Holdable.HeldByOwnerId : Holdable.NoOwner;

        public void Init(ushort id)
        {
            Id = id;
            Rb = GetComponent<Rigidbody>();
            Holdable = Holdable.GetOrAdd(gameObject);
            OriginalKinematic = Rb.isKinematic;
            OriginalGravity = Rb.useGravity;
            originalDrag = Rb.linearDamping;
            originalAngularDrag = Rb.angularDamping;
            originalDetection = Rb.collisionDetectionMode;
            Holdable.HeldChanged += OnHeldChanged;
        }

        private void OnDestroy()
        {
            if (Holdable != null) Holdable.HeldChanged -= OnHeldChanged;
            if (!SuppressDestroyNotify && NetWorld.Active != null) NetWorld.Active.OnBodyDestroyed(this);
        }

        private void OnHeldChanged(int owner)
        {
            NetWorld world = NetWorld.Active;
            if (world != null) world.OnLocalHeldChanged(this, owner);
        }

        /// <summary>Pasa a seguir las poses del servidor (cinematico). Quita lo que moveria el cuerpo por su cuenta.</summary>
        public void BecomeFollower()
        {
            if (Rb == null) return;
            DropLocalDrivers();
            if (!Rb.isKinematic) { Rb.linearVelocity = Vector3.zero; Rb.angularVelocity = Vector3.zero; }
            Rb.isKinematic = true;
            CurrentMode = Mode.Follower;
            hasTarget = false;
        }

        /// <summary>Vuelve a su fisica original (la simula esta maquina).</summary>
        public void BecomeSimulated()
        {
            if (Rb == null) return;
            Rb.isKinematic = OriginalKinematic;
            Rb.useGravity = OriginalGravity;
            Rb.linearDamping = originalDrag;
            Rb.angularDamping = originalAngularDrag;
            Rb.collisionDetectionMode = originalDetection;
            CurrentMode = Mode.Simulated;
            hasTarget = false;
        }

        /// <summary>Solo marca el modo (el objeto ya dejo su fisica lista, p. ej. un lienzo que se pone a caer o a flotar por su cuenta).</summary>
        public void MarkSimulated()
        {
            CurrentMode = Mode.Simulated;
            hasTarget = false;
        }

        /// <summary>Servidor: lo sostiene un cliente. El cuerpo queda cinematico y sigue las poses que ese cliente manda.</summary>
        public void BecomeRemoteDriven()
        {
            if (Rb == null) return;
            if (!Rb.isKinematic) { Rb.linearVelocity = Vector3.zero; Rb.angularVelocity = Vector3.zero; }
            Rb.isKinematic = true;
            CurrentMode = Mode.RemoteDriven;
            targetPos = Rb.position;
            targetRot = Rb.rotation;
            hasTarget = true;
        }

        /// <summary>Lo que se mueve solo en el objeto (flotar de un lienzo, fijado en el aire) no puede competir con el servidor.</summary>
        private void DropLocalDrivers()
        {
            if (TryGetComponent(out PinnedBody pin)) Destroy(pin);
            if (TryGetComponent(out PhysicalCanvas canvas)) canvas.NetNeutralize();
        }

        /// <summary>Nueva pose objetivo. snap = saltar de golpe (estado completo / reinicio).</summary>
        public void SetTarget(Vector3 pos, Quaternion rot, bool snap)
        {
            targetPos = pos;
            targetRot = rot;
            hasTarget = true;
            if (snap && Rb != null) { Rb.position = pos; Rb.rotation = rot; transform.SetPositionAndRotation(pos, rot); }
        }

        private void FixedUpdate()
        {
            if (Rb == null) return;
            switch (CurrentMode)
            {
                case Mode.Follower:
                    if (!hasTarget) break;
                    MoveToward(NetBodyMath.FollowAlpha(FollowRate, Time.fixedDeltaTime));
                    break;
                case Mode.RemoteDriven:
                    if (hasTarget) MoveToward(0.6f);
                    break;
                case Mode.Simulated:
                    // Cliente: lo que ya no sostiene vuelve a seguir al servidor tras un instante de simulacion propia.
                    NetWorld world = NetWorld.Active;
                    if (world != null && !world.IsServer && !LocalHeld && Time.time >= GhostUntil) BecomeFollower();
                    break;
            }
        }

        private void MoveToward(float alpha)
        {
            Vector3 pos = Rb.position;
            if ((targetPos - pos).sqrMagnitude > NetBodyMath.SnapDistance * NetBodyMath.SnapDistance)
            {
                Rb.position = targetPos; Rb.rotation = targetRot;
                return;
            }
            Rb.MovePosition(Vector3.Lerp(pos, targetPos, alpha));
            Rb.MoveRotation(Quaternion.Slerp(Rb.rotation, targetRot, alpha));
        }
    }
}
