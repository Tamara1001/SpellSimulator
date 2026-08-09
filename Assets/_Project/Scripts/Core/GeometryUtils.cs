using UnityEngine;

namespace SpellSimulator.Core
{
    public static class GeometryUtils
    {
        /// <summary>
        /// Calcula el angulo en grados desde un punto hacia otro.
        /// </summary>
        public static float GetAngleDegrees(Vector2 from, Vector2 to)
        {
            Vector2 dir = to - from;
            return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Rota un punto alrededor de un pivote un determinado numero de grados.
        /// </summary>
        public static Vector2 RotatePointAroundPivot(Vector2 point, Vector2 pivot, float anglesDegrees)
        {
            float radians = anglesDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            
            float x = point.x - pivot.x;
            float y = point.y - pivot.y;
            
            float newX = x * cos - y * sin;
            float newY = x * sin + y * cos;
            
            return new Vector2(newX + pivot.x, newY + pivot.y);
        }

        /// <summary>
        /// Devuelve una copia de la sesion de dibujo con todos sus puntos rotados.
        /// Util para normalizar los Signos antes de enviarlos a la red neuronal.
        /// </summary>
        public static DrawingSession RotateSession(DrawingSession original, Vector2 pivot, float angleDegrees)
        {
            DrawingSession rotated = new DrawingSession();
            foreach (var stroke in original.Strokes)
            {
                Stroke rotatedStroke = new Stroke();
                foreach (var point in stroke.Points)
                {
                    rotatedStroke.AddPoint(RotatePointAroundPivot(point, pivot, angleDegrees));
                }
                rotated.AddStroke(rotatedStroke);
            }
            return rotated;
        }
        
        /// <summary>
        /// Calcula el centro geometrico exacto (centro del bounding box) de una sesion.
        /// </summary>
        public static Vector2 GetSessionCenter(DrawingSession session)
        {
            if (session.GetBounds(out Vector2 min, out Vector2 max))
            {
                return min + (max - min) / 2f;
            }
            return Vector2.zero;
        }
    }
}
