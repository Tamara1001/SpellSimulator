using UnityEngine;
using SpellSimulator.Audio;

namespace SpellSimulator.World
{
    /// <summary>
    /// Luz de un farol del tutorial (solo malla emisiva, sin luces reales: presupuesto de Quest). Al encenderse suena un campanilla corta
    /// y la llama hace un pulso (crece y se asienta). Sin allocations: solo escala el transform un par de decimas de segundo.
    /// </summary>
    public class TutorialLampGlow : MonoBehaviour
    {
        public const string CueId = "seal_prepared";
        private const float Duration = 0.75f, PeakScale = 2.0f, Volume = 0.9f;

        /// <summary>Pulso 0..1 -> multiplicador de escala: arranca en el pico y baja con curva suave hasta 1 (lo fija un test).</summary>
        public static float PulseScale(float t01)
        {
            float k = 1f - Mathf.Clamp01(t01);
            return 1f + (PeakScale - 1f) * k * k;
        }

        private Vector3 baseScale = Vector3.one;
        private bool captured, playing;
        private float t;

        private void OnEnable()
        {
            if (!captured) { baseScale = transform.localScale; captured = true; }
            t = 0f;
            playing = true;
            transform.localScale = baseScale * PulseScale(0f);
            AudioDirector director = AudioDirector.Instance;
            if (director != null) director.Play(CueId, transform.position, Volume);
        }

        private void OnDisable()
        {
            playing = false;
            if (captured) transform.localScale = baseScale;
        }

        private void Update()
        {
            if (!playing) return;
            t += Time.deltaTime;
            bool end = t >= Duration;
            transform.localScale = baseScale * PulseScale(end ? 1f : t / Duration);
            if (end) playing = false;
        }
    }
}
