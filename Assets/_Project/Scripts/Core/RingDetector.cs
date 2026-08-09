using UnityEngine;
using System.Collections.Generic;

namespace SpellSimulator.Core
{
    public class RingInfo
    {
        public bool Found;
        public Stroke Stroke;
        public Vector2 Center;
        public float Radius;
    }

    public static class RingDetector
    {
        /// <summary>
        /// Detecta si hay un anillo en la sesion de dibujo usando una heuristica rapida.
        /// Busca un trazo grande, que encierre area, y cuyos extremos se toquen o esten cerca.
        /// </summary>
        public static RingInfo Detect(DrawingSession session)
        {
            RingInfo bestRing = new RingInfo { Found = false };
            float bestScore = -1f;

            foreach (var stroke in session.Strokes)
            {
                if (stroke.Points.Count < 10) continue; // Demasiado corto para ser un anillo

                // 1. Calcular Bounding Box del trazo
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
                Vector2 max = new Vector2(float.MinValue, float.MinValue);
                foreach (var p in stroke.Points)
                {
                    if (p.x < min.x) min.x = p.x;
                    if (p.y < min.y) min.y = p.y;
                    if (p.x > max.x) max.x = p.x;
                    if (p.y > max.y) max.y = p.y;
                }

                float width = max.x - min.x;
                float height = max.y - min.y;
                float size = Mathf.Max(width, height);
                
                // Si el tamano es infimo, ignorar
                if (size < 0.05f) continue;

                // 2. Proximidad de cierre (distancia entre inicio y fin relativa al tamano total)
                Vector2 start = stroke.Points[0];
                Vector2 end = stroke.Points[stroke.Points.Count - 1];
                float closureDist = Vector2.Distance(start, end);
                float closureRatio = closureDist / size;

                // 3. Aspect Ratio (un anillo suele tender a cuadrado/circular, no hiper rectangular)
                float aspectRatio = Mathf.Min(width, height) / Mathf.Max(width, height);

                // Heuristica de puntaje: premiamos trazos grandes, bien cerrados y cuadrados.
                // Tolerancia: Cierre hasta un 40% del tamano del radio. AspectRatio > 50%.
                if (closureRatio < 0.4f && aspectRatio > 0.5f)
                {
                    float score = size * aspectRatio * (1.0f - closureRatio);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestRing.Found = true;
                        bestRing.Stroke = stroke;
                        bestRing.Center = min + new Vector2(width / 2f, height / 2f);
                        bestRing.Radius = size / 2f;
                    }
                }
            }

            return bestRing;
        }
    }
}
