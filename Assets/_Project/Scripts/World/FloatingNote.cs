using UnityEngine;
using TMPro;

namespace SpellSimulator.World
{
    /// <summary>
    /// Nota flotante del tutorial: una tarjeta con dos o tres palabras que flota en su lugar con un vaiven suave. Es un cuerpo
    /// fisico sin gravedad atado a su punto con un resorte: el viento, un golpe o el jugador la empujan y vuelve sola.
    /// Sin allocations por frame.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class FloatingNote : MonoBehaviour
    {
        [SerializeField, Tooltip("Fuerza del resorte que la devuelve a su lugar (N/m).")] private float spring = 6f;
        [SerializeField, Tooltip("Freno del movimiento.")] private float damping = 2.2f;
        [SerializeField, Tooltip("Rapidez con la que recupera su orientacion (1/s).")] private float upright = 2.5f;
        [SerializeField, Tooltip("Alto del vaiven (m) y vueltas por segundo.")] private Vector2 bob = new Vector2(0.05f, 0.25f);

        private Rigidbody body;
        private Vector3 home;
        private Quaternion homeRotation;
        private float phase;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            home = transform.position;
            homeRotation = transform.rotation;
            TextMeshPro front = GetComponentInChildren<TextMeshPro>();
            if (front != null && transform.Find("LabelBack") == null)
            {
                TextMeshPro back = Instantiate(front, front.transform.parent);
                back.name = "LabelBack";
                back.transform.localPosition = new Vector3(0f, 0f, 0.014f);
                back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
            // Cada nota arranca en otro punto del vaiven.
            phase = (home.x * 1.7f + home.z * 2.3f) % 6.2831853f;
        }

        private void FixedUpdate()
        {
            float t = Time.time * bob.y * 6.2831853f + phase;
            Vector3 target = home + Vector3.up * (Mathf.Sin(t) * bob.x);
            body.AddForce((target - body.position) * spring - body.linearVelocity * damping, ForceMode.Acceleration);
            body.angularVelocity *= 1f - Mathf.Clamp01(damping * Time.fixedDeltaTime);
            body.MoveRotation(Quaternion.Slerp(body.rotation, homeRotation, upright * Time.fixedDeltaTime));
        }
    }
}
