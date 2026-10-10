using UnityEngine;

namespace SpellSimulator.World
{
    /// <summary>
    /// Revoloteo de lo muy liviano (hojas, plumas, papeles sueltos): mientras se mueve con algo de velocidad recibe giros y empujoncitos laterales
    /// suaves y, al caer, planea (resta parte de la gravedad). Sin allocations; cuesta casi nada y solo actua con el cuerpo en movimiento.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Flutter : MonoBehaviour
    {
        [SerializeField, Tooltip("Velocidad (m/s) a partir de la cual revolotea.")] private float minSpeed = 0.6f;
        [SerializeField, Tooltip("Fuerza del revoloteo (m/s2).")] private float swirl = 5f;
        [SerializeField, Tooltip("Parte de la gravedad que se anula al caer (planeo, 0..1).")] private float glide = 0.6f;

        private Rigidbody rb;
        private float phase;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            phase = Mathf.Abs(transform.position.x * 3.1f + transform.position.z * 1.7f);
        }

        private void FixedUpdate()
        {
            if (rb.isKinematic || rb.IsSleeping()) return;
            Vector3 v = rb.linearVelocity;
            float speed = v.magnitude;
            if (speed < minSpeed) return;
            float k = Mathf.Clamp01(speed / 6f);
            float t = Time.time * 3.2f + phase;
            Vector3 side = Vector3.Cross(v / speed, Vector3.up);
            rb.AddForce((side * Mathf.Sin(t) + Vector3.up * Mathf.Sin(t * 1.7f) * 0.5f) * (swirl * k), ForceMode.Acceleration);
            rb.AddTorque(new Vector3(Mathf.Sin(t * 1.3f), Mathf.Cos(t * 0.9f), Mathf.Sin(t * 1.1f)) * (swirl * 0.8f * k), ForceMode.Acceleration);
            if (rb.useGravity && v.y < 0f) rb.AddForce(-Physics.gravity * glide, ForceMode.Acceleration);
        }
    }
}
