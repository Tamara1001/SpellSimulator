using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.Learning
{
    /// <summary>
    /// Matematica pura del trazo animado del grimorio: suaviza la plantilla (quita la escalera de la cuantizacion) y la
    /// remuestrea por longitud de arco, asi todos los glifos se dibujan a la misma velocidad.
    /// </summary>
    public static class GrimoireStrokeMath
    {
        /// <summary>Separacion entre puntos remuestreados (m): bastante menor que el ancho de la linea.</summary>
        public const float StepMeters = 0.0012f;
        /// <summary>Radio de la ventana de suavizado (en puntos de la plantilla).</summary>
        public const int SmoothRadius = 3;
        /// <summary>Duracion total de la animacion de un glifo (s), sin importar cuantos trazos o que tan largo sea.</summary>
        public const float DrawSeconds = 2.5f;

        /// <summary>Promedio movil simetrico; los extremos quedan fijos. Escribe en dst (se limpia antes).</summary>
        public static void Smooth(List<Vector2> src, int radius, List<Vector2> dst)
        {
            dst.Clear();
            int n = src.Count;
            for (int i = 0; i < n; i++)
            {
                int r = Mathf.Min(radius, Mathf.Min(i, n - 1 - i));
                Vector2 sum = Vector2.zero;
                for (int k = i - r; k <= i + r; k++) sum += src[k];
                dst.Add(sum / (2 * r + 1));
            }
        }

        /// <summary>Agrega a dst puntos equiespaciados (~step) sobre la polilinea src, conservando ambos extremos.</summary>
        public static void Resample(List<Vector2> src, float step, List<Vector2> dst)
        {
            int n = src.Count;
            if (n == 0) return;
            float total = 0f;
            for (int i = 1; i < n; i++) total += Vector2.Distance(src[i - 1], src[i]);
            if (n == 1 || total < 1e-6f) { dst.Add(src[0]); return; }

            int count = Mathf.Max(2, Mathf.CeilToInt(total / Mathf.Max(1e-5f, step)) + 1);
            int seg = 1;
            float segStart = 0f;
            float segLen = Vector2.Distance(src[0], src[1]);
            for (int k = 0; k < count; k++)
            {
                float d = total * k / (count - 1);
                while (seg < n - 1 && d > segStart + segLen)
                {
                    segStart += segLen;
                    seg++;
                    segLen = Vector2.Distance(src[seg - 1], src[seg]);
                }
                float t = segLen > 1e-9f ? Mathf.Clamp01((d - segStart) / segLen) : 0f;
                dst.Add(Vector2.Lerp(src[seg - 1], src[seg], t));
            }
        }
    }
}
