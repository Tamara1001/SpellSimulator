using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.Core
{
    [Serializable]
    public class Stroke
    {
        public List<Vector2> Points = new List<Vector2>();

        public void AddPoint(Vector2 point)
        {
            Points.Add(point);
        }
    }

    [Serializable]
    public class DrawingSession
    {
        public List<Stroke> Strokes = new List<Stroke>();

        public void AddStroke(Stroke stroke)
        {
            Strokes.Add(stroke);
        }

        public void Clear()
        {
            Strokes.Clear();
        }

        public bool IsEmpty => Strokes.Count == 0;

        /// <summary>
        /// Obtiene los limites matematicos de todos los puntos dibujados.
        /// Retorna true si hay puntos validos.
        /// </summary>
        public bool GetBounds(out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);

            bool hasPoints = false;

            foreach (var stroke in Strokes)
            {
                foreach (var point in stroke.Points)
                {
                    if (point.x < min.x) min.x = point.x;
                    if (point.y < min.y) min.y = point.y;
                    if (point.x > max.x) max.x = point.x;
                    if (point.y > max.y) max.y = point.y;
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
    }
}
