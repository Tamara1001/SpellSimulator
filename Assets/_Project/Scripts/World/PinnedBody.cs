using UnityEngine;

namespace SpellSimulator.World
{
    /// <summary>
    /// Objeto generico (banco, mesa, caja...) dejado flotando en el aire con el clic de la rueda / T. Queda quieto con el mismo
    /// vaiven suave que los lienzos. Recuerda el estado fisico original para devolverlo al volver a agarrarlo (<see cref="Release"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public class PinnedBody : MonoBehaviour
    {
        // Mismos valores que PhysicalCanvas, pero con menos cabeceo: los muebles grandes se verian raros.
        private const float BobHeight = 0.03f, BobSpeed = 1.3f, SwayDegrees = 1.2f, DriftMeters = 0.015f;

        private Rigidbody rb;
        private bool originalKinematic, originalGravity;
        private Vector3 anchor;
        private Quaternion baseRotation;
        private float phase;
        private float pinnedAt;

        /// <summary>Segundos que una cosa dejada flotando con E se sostiene antes de caerse sola (regla general de las cosas en el aire).</summary>
        public const float FloatSeconds = 60f;

        /// <summary>Deja el cuerpo flotando donde esta. originalKinematic/originalGravity: lo que tenia antes de agarrarlo.</summary>
        public static void Pin(Rigidbody body, bool originalKinematic, bool originalGravity)
        {
            if (body == null) return;
            if (!body.TryGetComponent(out PinnedBody pin)) pin = body.gameObject.AddComponent<PinnedBody>();
            pin.rb = body;
            pin.originalKinematic = originalKinematic;
            pin.originalGravity = originalGravity;
            pin.anchor = body.position;
            pin.baseRotation = body.rotation;
            pin.phase = Random.value * 6.28f;
            pin.pinnedAt = Time.time;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.useGravity = false;
            body.isKinematic = true;
        }

        /// <summary>Si el cuerpo esta fijado, lo desfija (restaura fisica original) y devuelve true.</summary>
        public static bool Unpin(Rigidbody body)
        {
            if (body == null || !body.TryGetComponent(out PinnedBody pin)) return false;
            pin.Release();
            return true;
        }

        /// <summary>Restaura la fisica original y se quita del objeto.</summary>
        public void Release()
        {
            if (rb != null)
            {
                rb.isKinematic = originalKinematic;
                rb.useGravity = originalGravity;
            }
            Destroy(this);
        }

        private void Update()
        {
            if (rb == null || !rb.isKinematic) return;
            if (Time.time - pinnedAt >= FloatSeconds) { Release(); return; }
            float t = Time.time * BobSpeed + phase;
            // Tres ondas de periodos distintos: no se repite de forma obvia.
            Vector3 offset = new Vector3(Mathf.Sin(t * 0.61f) * DriftMeters, Mathf.Sin(t) * BobHeight, Mathf.Cos(t * 0.47f) * DriftMeters);
            Quaternion sway = Quaternion.Euler(Mathf.Sin(t * 0.83f) * SwayDegrees, Mathf.Sin(t * 0.53f + 1.3f) * SwayDegrees, Mathf.Sin(t * 0.71f + 2.1f) * SwayDegrees);
            transform.SetPositionAndRotation(anchor + offset, baseRotation * sway);
        }
    }
}
