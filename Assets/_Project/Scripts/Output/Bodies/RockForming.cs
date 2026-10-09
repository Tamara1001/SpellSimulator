using UnityEngine;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Fase de formacion de la roca: flota sin gravedad (cinematica) con un temblor suave mientras crece de escala 0 a 1.
    /// Al terminar activa su collider, la gravedad y la fisica normal (cae al suelo) y se quita.
    /// </summary>
    public class RockForming : MonoBehaviour
    {
        private const float Tremor = 0.012f, Spin = 25f;

        private Rigidbody rb;
        private Collider col;
        private Vector3 basePos;
        private float radius, seconds, t, phase;
        private Vector3 spinAxis;

        public void Init(Rigidbody body, Collider collider, float rockRadius, float formSeconds)
        {
            rb = body;
            col = collider;
            radius = rockRadius;
            seconds = Mathf.Max(0.05f, formSeconds);
            basePos = transform.position;
            phase = Random.value * 6.28f;
            spinAxis = Random.onUnitSphere;
            transform.localScale = Vector3.zero;
        }

        private void Update()
        {
            t += Time.deltaTime / seconds;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            transform.localScale = Vector3.one * (radius * k);
            float w = Time.time * 23f + phase;
            Vector3 shake = new Vector3(Mathf.Sin(w), Mathf.Sin(w * 1.31f + 1f), Mathf.Cos(w * 0.87f)) * (Tremor * k);
            transform.SetPositionAndRotation(basePos + shake, Quaternion.AngleAxis(Spin * Time.deltaTime, spinAxis) * transform.rotation);
            if (t < 1f) return;

            transform.localScale = Vector3.one * radius;
            col.enabled = true;
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.WakeUp();
            Destroy(this);
        }
    }
}
