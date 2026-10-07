using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.Core
{
    /// <summary>
    /// Geometria 2D pura sobre trazos. Convencion de angulos: grados matematicos con Y hacia arriba
    /// (0 = derecha, 90 = arriba, 270 = abajo), igual que utils/geometry.ts de la referencia.
    /// </summary>
    public static class GeometryUtils
    {
        public static float NormalizeAngleDeg(float degrees)
        {
            float wrapped = degrees % 360f;
            return wrapped < 0f ? wrapped + 360f : wrapped;
        }

        public static float AngleDeg(Vector2 direction)
        {
            return NormalizeAngleDeg(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        /// <summary>Angulo en grados desde un punto hacia otro.</summary>
        public static float GetAngleDegrees(Vector2 from, Vector2 to)
        {
            return AngleDeg(to - from);
        }

        public static Vector2 VectorFromAngleDeg(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        public static Vector2 RotatePointAroundPivot(Vector2 point, Vector2 pivot, float angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            Vector2 d = point - pivot;
            return new Vector2(d.x * cos - d.y * sin, d.x * sin + d.y * cos) + pivot;
        }

        public static bool GetBounds(IReadOnlyList<Stroke> strokes, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            bool hasPoints = false;

            foreach (var stroke in strokes)
            {
                foreach (var p in stroke.Points)
                {
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                    hasPoints = true;
                }
            }

            if (!hasPoints)
            {
                min = Vector2.zero;
                max = Vector2.zero;
            }
            return hasPoints;
        }

        /// <summary>Centro del bounding box de una sesion.</summary>
        public static Vector2 GetSessionCenter(DrawingSession session)
        {
            return session.GetBounds(out Vector2 min, out Vector2 max) ? (min + max) * 0.5f : Vector2.zero;
        }

        public static float StrokeLength(Stroke stroke)
        {
            float length = 0f;
            for (int i = 1; i < stroke.Points.Count; i++)
                length += Vector2.Distance(stroke.Points[i - 1], stroke.Points[i]);
            return length;
        }

        public static Vector2 Centroid(IReadOnlyList<Stroke> strokes)
        {
            Vector2 sum = Vector2.zero;
            int count = 0;
            foreach (var stroke in strokes)
            {
                foreach (var p in stroke.Points)
                {
                    sum += p;
                    count++;
                }
            }
            return count > 0 ? sum / count : Vector2.zero;
        }

        /// <summary>
        /// Eje dominante (no dirigido) de la nube de puntos via PCA, en [0, 360).
        /// </summary>
        public static float DominantAxisOrientationDeg(IReadOnlyList<Stroke> strokes)
        {
            Vector2 center = Centroid(strokes);
            float xx = 0f, xy = 0f, yy = 0f;
            int count = 0;
            foreach (var stroke in strokes)
            {
                foreach (var p in stroke.Points)
                {
                    Vector2 d = p - center;
                    xx += d.x * d.x;
                    xy += d.x * d.y;
                    yy += d.y * d.y;
                    count++;
                }
            }
            if (count < 2) return 0f;
            return NormalizeAngleDeg(0.5f * Mathf.Atan2(2f * xy, xx - yy) * Mathf.Rad2Deg);
        }

        /// <summary>
        /// Direccion de dibujo: desde el primer punto del primer trazo al ultimo punto del ultimo.
        /// </summary>
        public static float DirectedStrokeAngleDeg(IReadOnlyList<Stroke> strokes)
        {
            Stroke first = null, last = null;
            foreach (var s in strokes)
            {
                if (s.Points.Count < 2) continue;
                if (first == null) first = s;
                last = s;
            }
            if (first == null) return 0f;
            return AngleDeg(last.Points[last.Points.Count - 1] - first.Points[0]);
        }
    }
}
