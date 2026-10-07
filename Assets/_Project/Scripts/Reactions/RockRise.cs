using UnityEngine;

namespace SpellSimulator.Reactions
{
    /// <summary>Roca que sale del suelo en un instante y queda clavada (cinematica) hasta que algo la rompe.</summary>
    public class RockRise : MonoBehaviour
    {
        private const float RiseSeconds = 0.35f;
        private Vector3 target, start;
        private float t;

        public void Init(Vector3 basePoint, Quaternion rotation, float height)
        {
            Vector3 up = rotation * Vector3.up;
            target = basePoint + up * height * 0.3f;
            start = basePoint - up * height * 0.6f;
            transform.position = start;
        }

        private void Update()
        {
            t += Time.deltaTime / RiseSeconds;
            float k = 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));
            transform.position = Vector3.LerpUnclamped(start, target, k);
            if (t >= 1f) enabled = false;
        }
    }
}
