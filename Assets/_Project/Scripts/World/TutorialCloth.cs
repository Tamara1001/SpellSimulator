using UnityEngine;
using SpellSimulator.Player;
using SpellSimulator.World.Map;

namespace SpellSimulator.World
{
    /// <summary>
    /// Tela pesada que tapa la bajada del piso 1 (torre-tutorial, Docs/MAPA_FLUJO_PLAN.md, §17). Es un cuerpo fisico atado a su lugar
    /// con un resorte: el viento (dibujado en un papel, en la pared o en el piso) o un objeto lanzado la apartan y entonces se suelta,
    /// queda en otra posicion (la tela flota hacia el techo, el pestillo cae al piso, el contrapeso se queda levantado) y deja de bloquear al jugador. El jugador no la mueve: para el es un muro. <see cref="TowerTutorial.Begin"/> la rearma.
    /// Reglas en <see cref="ClothRules"/>. Sin allocations por frame.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public class TutorialCloth : MonoBehaviour
    {
        [SerializeField, Tooltip("Resorte que la devuelve a su lugar (1/s2) y freno del movimiento (1/s).")] private Vector2 spring = new Vector2(8f, 2.5f);
        [SerializeField, Tooltip("Aceleracion (m/s2) hacia arriba de la tela ya suelta: flota y sube hasta topar con el techo.")] private float buoyancy = 1.6f;

        [SerializeField, Tooltip("Contrapeso: solo se suelta al subir (ClothRules.LiftDistance), no por empuje lateral.")] private bool liftOnly;

        private const float PlayerHold = 0.2f;

        private bool drops;
        private Collider[] ignoredPlayer;

        private Rigidbody body;
        private BoxCollider box;
        private Vector3 home, scale;
        private Quaternion homeRotation;
        private float playerTouch = -10f, impulse, openedAt;
        private bool opened;

        public bool IsOpen => opened;

        /// <summary>Lo llama el generador del mapa: contrapeso que solo se suelta al subir.</summary>
        public void SetLiftOnly(bool value) => liftOnly = value;

        /// <summary>Lo llama el generador del mapa: al soltarse cae al piso (el pestillo) en vez de flotar.</summary>
        public void SetDrops(bool value) => drops = value;
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
            body.position = home;
            body.rotation = homeRotation;
            transform.localScale = scale;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.linearDamping = 0f;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.useGravity = false;
            box.enabled = true;
            SetIgnorePlayer(false);
            Physics.SyncTransforms();
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
                // Suelta: sigue siendo un cuerpo fisico. La tela flota (sube hasta el techo); el pestillo cae; el contrapeso queda donde lo dejo la columna.
                if (!drops && !liftOnly) body.AddForce(Vector3.up * buoyancy - body.linearVelocity * 0.8f, ForceMode.Acceleration);
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
            bool release = liftOnly ? ClothRules.ShouldRelease(body.position.y - home.y)
                : ClothRules.ShouldOpen(displacement, body.linearVelocity.magnitude, impulse, false);
            if (release) Open();
            impulse = 0f;
        }

        /// <summary>El cuerpo suelto ya no choca con el jugador (puede cruzarlo), pero sigue chocando con el resto del mundo.</summary>
        private void SetIgnorePlayer(bool ignore)
        {
            if (ignore)
            {
                var player = FindFirstObjectByType<PlayerMovement>();
                ignoredPlayer = player != null ? player.GetComponentsInChildren<Collider>() : null;
            }
            if (ignoredPlayer == null) return;
            for (int i = 0; i < ignoredPlayer.Length; i++)
                if (ignoredPlayer[i] != null) Physics.IgnoreCollision(box, ignoredPlayer[i], ignore);
            if (!ignore) ignoredPlayer = null;
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
            SetIgnorePlayer(true);
            if (drops)
            {
                body.constraints = RigidbodyConstraints.None;
                body.useGravity = true;
                body.linearDamping = 0.3f;
            }
            else if (liftOnly) body.linearDamping = 3f;
            else body.linearDamping = 0.5f;
            Debug.Log("[Tutorial] la tela pesada se soltó", this);
        }
    }
}
