using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.World
{
    /// <summary>
    /// Logica pura de los mini tutoriales de calco del piso 1 (sigilo de tierra, columna, circulo): decide si lo dibujado se parece a la plantilla
    /// fantasma. No usa el modelo de reconocimiento (que necesita GPU): compara FORMAS. Ambos conjuntos de trazos se llevan a un cuadrado
    /// unidad (centrados, por el lado mayor, sin deformar), se remuestrean por longitud de arco y se mide la distancia media al punto mas cercano
    /// en los dos sentidos (chamfer): asi no importan el orden de los trazos, su sentido, el tamaño ni donde se haya dibujado, pero si que
    /// falte una parte o sobre una linea.
    /// </summary>
    public static class TutorialTraceRules
    {
        /// <summary>Puntos con los que se muestrea cada conjunto de trazos.</summary>
        public const int Samples = 64;
        /// <summary>Error medio maximo (fraccion del lado mayor) para dar por buena la copia. Una mano temblorosa pasa; otra forma no.</summary>
        public const float MaxError = 0.14f;
        /// <summary>Un punto de la plantilla queda «sin cubrir» si lo dibujado pasa a mas de esto (fraccion del lado mayor); y no puede haber mas de <see cref="MaxUncovered"/> de ellos (falta un trazo).</summary>
        public const float CoverTolerance = 0.14f, MaxUncovered = 0.3f;
        /// <summary>Lado mayor minimo (en espacio isotropico del lienzo, el lado mayor del papel = 1) para que un garabato diminuto no cuente.</summary>
        public const float MinExtent = 0.18f;

        /// <summary>Circulo cerrado (cuadrado unidad, centro 0,5 y radio 0,5), un solo trazo.</summary>
        public static List<List<Vector2>> Circle(int points = 48)
        {
            var ring = new List<Vector2>(points + 1);
            for (int i = 0; i <= points; i++)
            {
                float a = i / (float)points * Mathf.PI * 2f;
                ring.Add(new Vector2(0.5f + 0.5f * Mathf.Cos(a), 0.5f + 0.5f * Mathf.Sin(a)));
            }
            return new List<List<Vector2>> { ring };
        }

        /// <summary>Lleva los trazos a un cuadrado de lado <paramref name="size"/> centrado en (0,5; 0,5), por el lado mayor y sin deformar. Devuelve una copia.</summary>
        public static List<List<Vector2>> FitToSquare(IReadOnlyList<IReadOnlyList<Vector2>> strokes, float size)
        {
            var result = new List<List<Vector2>>();
            if (!Bounds(strokes, out Vector2 min, out Vector2 max)) return result;
            Vector2 center = (min + max) * 0.5f;
            float scale = size / Mathf.Max(Mathf.Max(max.x - min.x, max.y - min.y), 1e-5f);
            for (int s = 0; s < strokes.Count; s++)
            {
                var pts = new List<Vector2>(strokes[s].Count);
                for (int i = 0; i < strokes[s].Count; i++) pts.Add((strokes[s][i] - center) * scale + new Vector2(0.5f, 0.5f));
                result.Add(pts);
            }
            return result;
        }

        /// <summary>Lado mayor (en las mismas unidades que los puntos) del conjunto de trazos; 0 si no hay.</summary>
        public static float Extent(IReadOnlyList<IReadOnlyList<Vector2>> strokes) =>
            Bounds(strokes, out Vector2 min, out Vector2 max) ? Mathf.Max(max.x - min.x, max.y - min.y) : 0f;

        /// <summary>
        /// ¿Lo dibujado (puntos en espacio isotropico del lienzo) copia la plantilla? <paramref name="error"/> es el error medio (0 = identico).
        /// Falso si no hay trazos, si lo dibujado es diminuto o si el error pasa de <see cref="MaxError"/>.
        /// </summary>
        public static bool Matches(IReadOnlyList<IReadOnlyList<Vector2>> drawn, IReadOnlyList<IReadOnlyList<Vector2>> template, out float error)
        {
            error = float.MaxValue;
            if (drawn == null || template == null || drawn.Count == 0 || template.Count == 0) return false;
            if (Extent(drawn) < MinExtent) return false;
            var a = Sample(Normalize(drawn), Samples);
            var b = Sample(Normalize(template), Samples);
            if (a.Count == 0 || b.Count == 0) return false;
            error = 0.5f * (MeanNearest(a, b, out _) + MeanNearest(b, a, out float uncovered));
            return error <= MaxError && uncovered <= MaxUncovered;
        }

        // ------------------------------------------------------------------ internos

        private static bool Bounds(IReadOnlyList<IReadOnlyList<Vector2>> strokes, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            bool any = false;
            if (strokes == null) return false;
            for (int s = 0; s < strokes.Count; s++)
                for (int i = 0; i < strokes[s].Count; i++)
                {
                    min = Vector2.Min(min, strokes[s][i]);
                    max = Vector2.Max(max, strokes[s][i]);
                    any = true;
                }
            return any;
        }

        /// <summary>Centra y escala por el lado mayor a 1.</summary>
        private static List<List<Vector2>> Normalize(IReadOnlyList<IReadOnlyList<Vector2>> strokes) => FitToSquare(strokes, 1f);

        /// <summary>Remuestrea todos los trazos juntos a <paramref name="count"/> puntos repartidos por longitud de arco.</summary>
        private static List<Vector2> Sample(List<List<Vector2>> strokes, int count)
        {
            var points = new List<Vector2>(count);
            float total = 0f;
            foreach (var s in strokes) total += Length(s);
            if (total < 1e-5f)
            {
                // Todo puntos sueltos (un toque): se devuelven tal cual.
                foreach (var s in strokes) points.AddRange(s);
                return points;
            }
            float step = total / count;
            foreach (var s in strokes)
            {
                if (s.Count == 1) { points.Add(s[0]); continue; }
                float carry = 0f;
                for (int i = 1; i < s.Count; i++)
                {
                    Vector2 p0 = s[i - 1], p1 = s[i];
                    float seg = Vector2.Distance(p0, p1);
                    if (seg < 1e-7f) continue;
                    float at = carry;
                    while (at < seg)
                    {
                        points.Add(Vector2.Lerp(p0, p1, at / seg));
                        at += step;
                    }
                    carry = at - seg;
                }
                points.Add(s[s.Count - 1]);
            }
            return points;
        }

        private static float Length(List<Vector2> s)
        {
            float l = 0f;
            for (int i = 1; i < s.Count; i++) l += Vector2.Distance(s[i - 1], s[i]);
            return l;
        }

        /// <summary>Distancia media de cada punto de <paramref name="from"/> al punto mas cercano de <paramref name="to"/>, y que parte de ellos queda a mas de <see cref="CoverTolerance"/>.</summary>
        private static float MeanNearest(List<Vector2> from, List<Vector2> to, out float farShare)
        {
            float sum = 0f;
            int far = 0;
            for (int i = 0; i < from.Count; i++)
            {
                float best = float.MaxValue;
                for (int j = 0; j < to.Count; j++)
                {
                    float d = (from[i] - to[j]).sqrMagnitude;
                    if (d < best) best = d;
                }
                float dist = Mathf.Sqrt(best);
                sum += dist;
                if (dist > CoverTolerance) far++;
            }
            farShare = far / (float)from.Count;
            return sum / from.Count;
        }
    }
}
