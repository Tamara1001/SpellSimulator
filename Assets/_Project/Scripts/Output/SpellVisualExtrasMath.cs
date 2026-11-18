using UnityEngine;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Logica pura de los extras visuales del hechizo (region, tinta resistente, estabilidad). Sin MonoBehaviour: testeable en EditMode.
    /// </summary>
    public static class SpellVisualExtrasMath
    {
        /// <summary>Por debajo de esta estabilidad el sello empieza a temblar y chisporrotear.</summary>
        public const float StableThreshold = 0.85f;

        /// <summary>Radio del cerco de la region, en radios de sello (anillo de fuego, perimetro de luz, domo de cristal).</summary>
        public const float RegionRadiusFactor = 3.2f;

        /// <summary>0 = trazo estable; 1 = trazo muy inestable. Alimenta ruido de particulas, chispas y parpadeo de luz.</summary>
        public static float Jitter(float stability)
        {
            if (stability >= StableThreshold) return 0f;
            return Mathf.Clamp01((StableThreshold - Mathf.Max(0f, stability)) / StableThreshold);
        }

        /// <summary>Multiplicador de la luz del hechizo: 1 si es estable; baja hasta 1 - 0.6*jitter segun el ruido (0..1).</summary>
        public static float Flicker(float jitter, float noise01)
        {
            return 1f - Mathf.Clamp01(jitter) * 0.6f * Mathf.Clamp01(noise01);
        }

        /// <summary>Chispas por rafaga del chisporroteo (crece con la inestabilidad).</summary>
        public static int CrackleCount(float jitter)
        {
            return jitter <= 0f ? 0 : Mathf.RoundToInt(6f + 40f * Mathf.Clamp01(jitter));
        }

        /// <summary>Radio del cerco de la region para un sello de escala <paramref name="scale"/>.</summary>
        public static float RegionRadius(float scale) => Mathf.Max(0.05f, scale) * RegionRadiusFactor;
    }
}
