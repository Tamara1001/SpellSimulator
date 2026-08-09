using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.Core
{
    public static class StrokeCleaner
    {
        /// <summary>
        /// Procesa una sesion entera: primero simplifica y luego suaviza los trazos.
        /// Retorna una copia limpia de la sesion (no modifica la original).
        /// </summary>
        public static DrawingSession CleanSession(DrawingSession rawSession, float tolerance, int splineResolution)
        {
            var cleanedSession = new DrawingSession();

            foreach (var rawStroke in rawSession.Strokes)
            {
                if (rawStroke.Points.Count < 3)
                {
                    // Trazos muy cortos (ej. puntos) no se suavizan
                    cleanedSession.AddStroke(rawStroke);
                    continue;
                }

                // 1. Simplificacion (Douglas-Peucker)
                List<Vector2> simplified = SimplifyDouglasPeucker(rawStroke.Points, tolerance);

                // 2. Suavizado (Catmull-Rom Spline)
                // Si despues de simplificar quedan pocos puntos, igual podemos suavizarlos.
                List<Vector2> smoothed = SmoothCatmullRom(simplified, splineResolution);

                var newStroke = new Stroke { Points = smoothed };
                cleanedSession.AddStroke(newStroke);
            }

            return cleanedSession;
        }

        #region Douglas-Peucker

        private static List<Vector2> SimplifyDouglasPeucker(List<Vector2> points, float tolerance)
        {
            if (points == null || points.Count < 3)
                return points;

            int firstPoint = 0;
            int lastPoint = points.Count - 1;
            List<int> pointIndexesToKeep = new List<int> { firstPoint, lastPoint };

            while (points[firstPoint].Equals(points[lastPoint]))
            {
                lastPoint--;
                if (lastPoint == firstPoint)
                    return new List<Vector2> { points[firstPoint] };
            }

            DouglasPeuckerReduction(points, firstPoint, lastPoint, tolerance, ref pointIndexesToKeep);

            pointIndexesToKeep.Sort();

            List<Vector2> result = new List<Vector2>();
            foreach (int index in pointIndexesToKeep)
            {
                result.Add(points[index]);
            }
            return result;
        }

        private static void DouglasPeuckerReduction(List<Vector2> points, int firstPoint, int lastPoint, float tolerance, ref List<int> pointIndexesToKeep)
        {
            float maxDistance = 0;
            int indexFarthest = 0;

            for (int index = firstPoint + 1; index < lastPoint; index++)
            {
                float distance = PerpendicularDistance(points[firstPoint], points[lastPoint], points[index]);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    indexFarthest = index;
                }
            }

            if (maxDistance > tolerance && indexFarthest != 0)
            {
                pointIndexesToKeep.Add(indexFarthest);
                DouglasPeuckerReduction(points, firstPoint, indexFarthest, tolerance, ref pointIndexesToKeep);
                DouglasPeuckerReduction(points, indexFarthest, lastPoint, tolerance, ref pointIndexesToKeep);
            }
        }

        private static float PerpendicularDistance(Vector2 Point1, Vector2 Point2, Vector2 Point)
        {
            float area = Mathf.Abs(.5f * (Point1.x * Point2.y + Point2.x * Point.y + Point.x * Point1.y - Point2.x * Point1.y - Point.x * Point2.y - Point1.x * Point.y));
            float bottom = Mathf.Sqrt(Mathf.Pow(Point1.x - Point2.x, 2f) + Mathf.Pow(Point1.y - Point2.y, 2f));
            return area / bottom * 2f;
        }

        #endregion

        #region Catmull-Rom Spline

        private static List<Vector2> SmoothCatmullRom(List<Vector2> points, int resolution)
        {
            if (points == null || points.Count < 3)
                return points;

            List<Vector2> result = new List<Vector2>();

            // Agregamos un punto extra al inicio y al final (duplicamos) para que la curva incluya los extremos.
            List<Vector2> controlPoints = new List<Vector2>(points.Count + 2);
            controlPoints.Add(points[0]);
            controlPoints.AddRange(points);
            controlPoints.Add(points[points.Count - 1]);

            // Catmull-Rom procesa segmentos de 4 puntos
            for (int i = 1; i < controlPoints.Count - 2; i++)
            {
                Vector2 p0 = controlPoints[i - 1];
                Vector2 p1 = controlPoints[i];
                Vector2 p2 = controlPoints[i + 1];
                Vector2 p3 = controlPoints[i + 2];

                // El primer punto del segmento
                if (i == 1) result.Add(GetCatmullRomPosition(0f, p0, p1, p2, p3));

                for (int j = 1; j <= resolution; j++)
                {
                    float t = (float)j / resolution;
                    result.Add(GetCatmullRomPosition(t, p0, p1, p2, p3));
                }
            }

            return result;
        }

        private static Vector2 GetCatmullRomPosition(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            Vector2 a = 2f * p1;
            Vector2 b = p2 - p0;
            Vector2 c = 2f * p0 - 5f * p1 + 4f * p2 - p3;
            Vector2 d = -p0 + 3f * p1 - 3f * p2 + p3;

            return 0.5f * (a + (b * t) + (c * t * t) + (d * t * t * t));
        }

        #endregion
    }
}
