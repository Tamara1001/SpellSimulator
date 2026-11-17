using UnityEngine;
using SpellSimulator.Player;
using SpellSimulator.World.Map;

namespace SpellSimulator.World
{
    /// <summary>
    /// Tela pesada que tapa la bajada del piso 1 (torre-tutorial, Docs/MAPA_FLUJO_PLAN.md, §17). Es un cuerpo fisico atado a su lugar
    /// con un resorte: el viento (dibujado en un papel, en la pared o en el piso) o un objeto lanzado la apartan y entonces se suelta,
    /// vuela y desaparece, dejando el paso libre. El jugador no la mueve: para el es un muro. <see cref="TowerTutorial.Begin"/> la rearma.
    /// Reglas en <see cref="ClothRules"/>. Sin allocations por frame.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public class TutorialCloth : MonoBehaviour
    {
        [SerializeField, Tooltip("Resorte que la devuelve a su lugar (1/s2) y freno del movimiento (1/s).")] private Vector2 spring = new Vector2(8f, 2.5f);
        [SerializeField, Tooltip("Segundos que vuela suelta antes de achicarse y desaparecer.")] private float flyTime = 1.4f;

        private const float PlayerHold = 0.2f, Shrink = 0.35f;

        private Rigidbody body;
        private BoxCollider box;
        private Vector3 home, scale;
        private Quaternion homeRotation;
        private float playerTouch = -10f, impulse, openedAt;
        private bool opened;

        public bool IsOpen => opened;
        /// <summary>Cuantas veces se soltó (para el smoke).</summary>
        public int Releases { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            box = GetComponent<BoxCollider>();
            home = transform.position;
            homeRotation = transform.rotation;
            scale = transform.localScale;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation;
        }

        /// <summary>Vuelve a colgar la tela (rearme del tutorial).</summary>
        public void Rearm()
        {
            gameObject.SetActive(true);
            opened = false;
            impulse = 0f;
            playerTouch = -10f;
            transform.SetPositionAndRotation(home, homeRotation);
            transform.localScale = scale;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.useGravity = false;
            box.enabled = true;
        }

        private void OnCollisionStay(Collision c)
        {
            if (c.collider.GetComponentInParent<PlayerMovement>() != null) playerTouch = Time.time;
        }

        private void OnCollisionEnter(Collision c)
        {
            if (opened || c.rigidbody == null) return;
            if (c.collider.GetComponentInParent<PlayerMovement>() != null) { playerTouch = Time.time; return; }
            impulse = Mathf.Max(impulse, c.impulse.magnitude);
        }

        private void FixedUpdate()
        {
            if (opened)
            {
                float t = Time.time - openedAt;
                if (t > flyTime)
                {
                    float k = 1f - (t - flyTime) / Shrink;
                    transform.localScale = scale * Mathf.Max(k, 0f);
                    if (k <= 0f) gameObject.SetActive(false);
                }
                return;
            }

            bool touching = Time.time - playerTouch < PlayerHold;
            if (touching)
            {
                // El jugador empuja una tela pesada: no cede.
                body.position = home;
                body.linearVelocity = Vector3.zero;
                impulse = 0f;
                return;
            }

            body.AddForce((home - body.position) * spring.x - body.linearVelocity * spring.y, ForceMode.Acceleration);
            float displacement = (body.position - home).magnitude;
            if (ClothRules.ShouldOpen(displacement, body.linearVelocity.magnitude, impulse, false)) Open();
            impulse = 0f;
        }

        /// <summary>Suelta la tela (lo usa el smoke para no depender del viento).</summary>
        public void ForceOpen()
        {
            if (!opened) Open();
        }

        private void Open()
        {
            opened = true;
            openedAt = Time.time;
            Releases++;
            box.enabled = false;
            Debug.Log("[Tutorial] la tela pesada se soltó", this);
        }
    }
}
