using UnityEngine;

namespace SpellSimulator.World
{
    /// <summary>
    /// Devuelve el objeto a su pose inicial si cae fuera del mundo o si se pide un reinicio (modo Libre).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Respawnable : MonoBehaviour
    {
        [SerializeField, Tooltip("Altura (m) por debajo de la cual el objeto vuelve a su lugar.")]
        private float killHeight = -20f;

        private Rigidbody rb;
        private Vector3 startPosition;
        private Quaternion startRotation;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            startPosition = transform.position;
            startRotation = transform.rotation;
        }

        private void FixedUpdate()
        {
            if (rb.position.y < killHeight) Respawn();
        }

        public void Respawn()
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = startPosition;
            rb.rotation = startRotation;
            transform.SetPositionAndRotation(startPosition, startRotation);
        }

        /// <summary>Reinicia todos los objetos reposicionables de la escena.</summary>
        public static void RespawnAll()
        {
            foreach (var r in FindObjectsByType<Respawnable>(FindObjectsSortMode.None)) r.Respawn();
        }
    }
}
