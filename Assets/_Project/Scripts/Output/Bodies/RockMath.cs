using UnityEngine;

namespace SpellSimulator.Output
{
    /// <summary>Numeros del hechizo de piedra (logica pura, con test EditMode). Ver PLAN_OCTAVA_TANDA seccion 3.</summary>
    public static class RockMath
    {
        public const float MinRadius = 0.08f, MaxRadius = 1.2f;
        public const float MinFormSeconds = 0.6f, MaxFormSeconds = 1.6f;
        public const float Density = 1800f, MinMass = 1f, MaxMass = 600f;
        /// <summary>Segundos de vida de una roca antes de empezar a achicarse.</summary>
        public const float LifeSeconds = 120f;
        public const float ShrinkSeconds = 1.5f;
        public const int MaxRocks = 10;

        /// <summary>Radio de la roca: 0.9 x anillo, mas grande con tinta poderosa (raiz cubica: crece el volumen, no el diametro).</summary>
        public static float Radius(float ringRadius, float inkPower)
        {
            float ink = Mathf.Max(inkPower, 0.0001f);
            return Mathf.Clamp(ringRadius * 0.9f * Mathf.Pow(ink, 1f / 3f), MinRadius, MaxRadius);
        }

        /// <summary>Cuanto tarda en formarse: las grandes tardan mas.</summary>
        public static float FormSeconds(float rockRadius)
        {
            float t = Mathf.InverseLerp(MinRadius, MaxRadius, rockRadius);
            return Mathf.Lerp(MinFormSeconds, MaxFormSeconds, t);
        }

        /// <summary>Masa (kg): densidad x volumen de esfera, acotada.</summary>
        public static float Mass(float rockRadius)
        {
            float volume = 4f / 3f * Mathf.PI * rockRadius * rockRadius * rockRadius;
            return Mathf.Clamp(Density * volume, MinMass, MaxMass);
        }
    }
}
