using UnityEngine;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Matematica pura de los controles manipulables del laboratorio (perilla, deslizador, selector con muescas).
    /// Los valores normalizados van en [0,1].
    /// </summary>
    public static class LabControlMath
    {
        /// <summary>Valor tras arrastrar: startValue + dragDelta / fullRangeDrag * (max - min), recortado a [min, max]. fullRangeDrag (> 0) es lo que hay que arrastrar para recorrer todo el rango.</summary>
        public static float DeltaToValue(float startValue, float dragDelta, float min, float max, float fullRangeDrag)
        {
            if (fullRangeDrag <= 1e-6f) return Mathf.Clamp(startValue, min, max);
            return Mathf.Clamp(startValue + dragDelta / fullRangeDrag * (max - min), min, max);
        }

        /// <summary>Angulo continuo (sin saltos de 360°): suma a previousUnwrapped el menor giro hacia newAngleDeg.</summary>
        public static float UnwrapAngle(float previousUnwrapped, float newAngleDeg) =>
            previousUnwrapped + Mathf.DeltaAngle(previousUnwrapped, newAngleDeg);

        /// <summary>Indice de la muesca mas cercana a value01 (0..detentCount-1).</summary>
        public static int DetentIndex(float value01, int detentCount)
        {
            if (detentCount <= 1) return 0;
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(value01) * (detentCount - 1)), 0, detentCount - 1);
        }

        /// <summary>Valor normalizado pegado a la muesca mas cercana (detentCount muescas repartidas de 0 a 1).</summary>
        public static float SnapToDetent(float value01, int detentCount)
        {
            if (detentCount <= 1) return 0f;
            return DetentIndex(value01, detentCount) / (float)(detentCount - 1);
        }

        /// <summary>True si pasar de prev01 a curr01 cambia de muesca: dispara haptica y clic.</summary>
        public static bool CrossedDetent(float prev01, float curr01, int detentCount) =>
            DetentIndex(prev01, detentCount) != DetentIndex(curr01, detentCount);
    }
}
