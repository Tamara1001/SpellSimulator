using UnityEngine;
using SpellSimulator.Interfaces;
using SpellSimulator.Reactions;

namespace SpellSimulator.World
{
    /// <summary>
    /// Puerta de la torre (E2): E (mirandola) la abre o la cierra con un giro suave de la bisagra (~0,5 s). Arranca abierta,
    /// contra el muro, sin tapar el paso ni el atril; cerrada bloquea la entrada. Reiniciar el mundo la deja abierta.
    /// Rigidbody cinematico: al girar empuja al jugador en vez de atravesarlo. Sin allocations por frame.
    /// Funciona igual en PC y VR (IPressable: E o el gatillo sobre la puerta).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TowerDoor : MonoBehaviour, IPressable, IResettable
    {
        private const string OpenPrompt = "Abrir puerta";
        private const string ClosePrompt = "Cerrar puerta";

        [SerializeField, Tooltip("Giro (grados, Y local respecto del padre) con la puerta cerrada.")]
        private float closedYaw;
        [SerializeField, Tooltip("Giro (grados, Y local respecto del padre) con la puerta abierta.")]
        private float openYaw = -150f;
        [SerializeField, Min(0.05f), Tooltip("Duracion del giro (s).")]
        private float swingTime = 0.5f;
        [SerializeField, Tooltip("Arranca abierta.")]
        private bool startOpen = true;

        private Rigidbody rb;
        private bool open;
        /// <summary>0 = cerrada, 1 = abierta.</summary>
        private float amount;
        private bool moving;

        /// <summary>Estado pedido (la animacion puede no haber terminado).</summary>
        public bool IsOpen => open;

        /// <summary>True mientras gira.</summary>
        public bool IsMoving => moving;

        /// <summary>Giro actual (grados, Y local).</summary>
        public float CurrentYaw => Mathf.LerpAngle(closedYaw, openYaw, Ease(amount));

        /// <summary>Configura los angulos (lo usa el generador del mapa).</summary>
        public void Configure(float closed, float opened, float seconds)
        {
            closedYaw = closed;
            openYaw = opened;
            swingTime = Mathf.Max(0.05f, seconds);
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            SetOpen(startOpen, true);
        }

        private void OnEnable() => MagicWorld.WorldReset += ResetToInitial;
        private void OnDisable() => MagicWorld.WorldReset -= ResetToInitial;

        public string GetPressPrompt() => open ? ClosePrompt : OpenPrompt;

        public void Press() => SetOpen(!open, false);

        /// <summary>Reinicio del mundo: abierta y quieta.</summary>
        public void ResetToInitial() => SetOpen(startOpen, true);

        /// <summary>Abre o cierra; immediate salta al final sin animar.</summary>
        public void SetOpen(bool value, bool immediate)
        {
            open = value;
            if (immediate)
            {
                amount = open ? 1f : 0f;
                moving = false;
                Apply(false);
                return;
            }
            moving = true;
        }

        private void FixedUpdate()
        {
            if (!moving) return;
            float target = open ? 1f : 0f;
            amount = Mathf.MoveTowards(amount, target, Time.fixedDeltaTime / swingTime);
            if (Mathf.Approximately(amount, target))
            {
                amount = target;
                moving = false;
            }
            Apply(true);
        }

        private void Apply(bool physics)
        {
            Quaternion parent = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
            Quaternion rotation = parent * Quaternion.Euler(0f, CurrentYaw, 0f);
            if (physics && rb != null) rb.MoveRotation(rotation);
            else
            {
                transform.rotation = rotation;
                if (rb != null) rb.rotation = rotation;
            }
        }

        /// <summary>Suavizado (empieza y termina despacio).</summary>
        private static float Ease(float t) => t * t * (3f - 2f * t);
    }
}
