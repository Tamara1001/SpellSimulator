using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.Core
{
    /// <summary>
    /// Limpieza de trazos: simplificacion Douglas-Peucker seguida de suavizado Catmull-Rom.
    /// Nunca modifica la sesion original.
    /// </summary>
    public static class StrokeCleaner
    {
        public const float DefaultTolerance = 0.005f;
        public const int DefaultSplineResolution = 3;

        public static DrawingSession CleanSession(DrawingSession rawSession, float tolerance, int splineResolution)
        {
            var cleaned = new DrawingSession();
            foreach (var raw in rawSession.Strokes)
            {
                if (raw.Points.Count < 3)
                {
                    cleaned.AddStroke(new Stroke(new List<Vector2>(raw.Points)));
                    continue;
                }

                List<Vector2> simplified = SimplifyDouglasPeucker(raw.Points, tolerance);
                cleaned.AddStroke(new Stroke(SmoothCatmullRom(simplified, splineResolution)));
            }
            return cleaned;
        }

        #region Douglas-Peucker

        private static List<Vector2> SimplifyDouglasPeucker(List<Vector2> points, float tolerance)
        {
            int first = 0;
            int last = points.Count - 1;

            // Un trazo cerrado (inicio == fin) degenera la recta de referencia; se recorta el final.
            while (last > first && points[first] == points[last]) last--;
            if (last == first) return new List<Vector2> { points[first] };

            var keep = new List<int> { first, last };
            Reduce(points, first, last, tolerance, keep);
            keep.Sort();

            var result = new List<Vector2>(keep.Count);
            foreach (int index in keep) result.Add(points[index]);
            return result;
        }

        private static void Reduce(List<Vector2> points, int first, int last, float tolerance, List<int> keep)
        {
            float maxDistance = 0f;
            int farthest = -1;

            for (int i = first + 1; i < last; i++)
            {
                float distance = PerpendicularDistance(points[first], points[last], points[i]);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    farthest = i;
                }
            }

            if (farthest < 0 || maxDistance <= tolerance) return;

            keep.Add(farthest);
            Reduce(points, first, farthest, tolerance, keep);
            Reduce(points, farthest, last, tolerance, keep);
        }

        private static float PerpendicularDistance(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 ab = b - a;
            float length = ab.magnitude;
            // Segmentos degenerados (a == b) aparecen en trazos que se cruzan; medir al punto.
            if (length < 1e-6f) return Vector2.Distance(a, p);
            return Mathf.Abs(ab.x * (a.y - p.y) - ab.y * (a.x - p.x)) / length;
        }

        #endregion

        #region Catmull-Rom

        private static List<Vector2> SmoothCatmullRom(List<Vector2> points, int resolution)
        {
            if (points.Count < 3 || resolution < 1) return points;

            var result = new List<Vector2>((points.Count - 1) * resolution + 1) { points[0] };
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 p0 = points[Mathf.Max(i - 1, 0)];
                Vector2 p1 = points[i];
                Vector2 p2 = points[i + 1];
                Vector2 p3 = points[Mathf.Min(i + 2, points.Count - 1)];

                for (int j = 1; j <= resolution; j++)
                    result.Add(CatmullRom((float)j / resolution, p0, p1, p2, p3));
            }
            return result;
        }

        private static Vector2 CatmullRom(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            Vector2 a = 2f * p1;
            Vector2 b = p2 - p0;
            Vector2 c = 2f * p0 - 5f * p1 + 4f * p2 - p3;
            Vector2 d = -p0 + 3f * p1 - 3f * p2 + p3;
            return 0.5f * (a + b * t + c * (t * t) + d * (t * t * t));
        }

        #endregion
    }
}
