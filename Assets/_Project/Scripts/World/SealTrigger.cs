using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;

namespace SpellSimulator.World
{
    /// <summary>
    /// Disparador de un sello inscripto en un objeto (GAME_DESIGN, sellos en objetos). Con este componente el anillo
    /// cerrado deja el sello ARMADO en vez de lanzarlo; el disparador lo activa:
    /// - Impact: un golpe fuerte (piedra arrojada);
    /// - Pressure: algo pesado apoyado encima (placa de presion).
    /// Va en el mismo objeto que el DrawingCanvas (o un padre suyo).
    /// </summary>
    public class SealTrigger : MonoBehaviour, Interfaces.ISealTrigger
    {
        public enum Mode { Impact, Pressure }

        [SerializeField] private Mode mode = Mode.Impact;
        [SerializeField] private SpellCaster caster;
        [SerializeField, Tooltip("Impact: velocidad relativa minima del golpe (m/s).")]
        private float impactSpeed = 3f;
        [SerializeField, Tooltip("Pressure: masa minima apoyada encima (kg).")]
        private float pressureMass = 1.5f;
        [SerializeField, Tooltip("Pressure: segundos que debe sostenerse el peso.")]
        private float pressureSeconds = 0.25f;
        [SerializeField, Tooltip("Pressure: la normal del contacto debe apuntar hacia arriba al menos asi (dot con up).")]
        private float minUpDot = 0.6f;

        [Header("Sello permanente (piedra de rayar)")]
        [SerializeField, Tooltip("Usos totales antes de gastarse. 1 = un solo lanzamiento (hay que volver a dibujar).")]
        private int maxUses = 1;
        [SerializeField, Tooltip("Segundos de enfriamiento antes de volver a armarse.")]
        private float cooldownSeconds = 3f;

        private DrawingCanvas canvas;
        private int uses;
        private float rearmAt = -1f;
        private float held;
        private float lastLoad;

        public Mode TriggerMode => mode;
        public int UsesLeft => Mathf.Max(0, maxUses - uses);

        public void SetPermanent(int uses, float cooldown)
        {
            maxUses = Mathf.Max(1, uses);
            cooldownSeconds = cooldown;
        }

        private void Fire()
        {
            bool launched = caster.Trigger(canvas);
            Debug.Log($"[SealTrigger] {name} dispara: {(launched ? "hechizo lanzado" : "no se lanzo (fase " + caster.GetPhase(canvas) + ")")}", this);
            if (!launched) return;
            uses++;
            if (uses < maxUses) rearmAt = Time.time + cooldownSeconds;
        }

        public void Configure(Mode newMode, SpellCaster spellCaster, float threshold)
        {
            mode = newMode;
            caster = spellCaster;
            if (mode == Mode.Impact) impactSpeed = threshold; else pressureMass = threshold;
        }

        private void Awake() => canvas = GetComponentInChildren<DrawingCanvas>();

        /// <summary>Un sello nuevo (el lienzo se borro) empieza con todos sus usos.</summary>
        private void OnEnable() => SealBreaker.SealBroken += OnBroken;
        private void OnDisable() => SealBreaker.SealBroken -= OnBroken;
        private void OnBroken(DrawingCanvas broken)
        {
            if (broken == canvas) { uses = 0; rearmAt = -1f; }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (mode != Mode.Impact || canvas == null || caster == null) return;
            float speed = collision.relativeVelocity.magnitude;
            // Diagnostico (prueba a mano): cada golpe relevante con lo que lo decidio.
            if (speed >= 1f)
                Debug.Log($"[SealTrigger] {name} golpea {collision.collider.name} a {speed:F1} m/s (umbral {impactSpeed:F1}), fase={caster.GetPhase(canvas)}, usos {uses}/{maxUses}", this);
            if (speed < impactSpeed) return;
            Fire();
        }

        private void OnCollisionStay(Collision collision)
        {
            if (mode != Mode.Pressure || canvas == null || caster == null) return;
            if (caster.GetPhase(canvas) != SealPhase.Armed) return;
            var body = collision.rigidbody;
            if (body == null || body.mass < pressureMass) return;
            // La carga cuenta solo si esta apoyada encima (el contacto empuja la placa hacia abajo).
            if (collision.contactCount == 0) return;
            if (Vector3.Dot(collision.GetContact(0).normal, Vector3.up) < minUpDot && Vector3.Dot(collision.GetContact(0).normal, Vector3.down) < minUpDot) return;
            lastLoad = Time.time;
        }

        private void Update()
        {
            if (rearmAt < 0f || Time.time < rearmAt || caster == null) return;
            rearmAt = -1f;
            caster.Rearm(canvas);
        }

        private void FixedUpdate()
        {
            if (mode != Mode.Pressure || canvas == null || caster == null) return;
            bool loaded = Time.time - lastLoad < Time.fixedDeltaTime * 2.5f;
            held = loaded ? held + Time.fixedDeltaTime : 0f;
            if (held >= pressureSeconds)
            {
                held = 0f;
                Fire();
            }
        }
    }
}
