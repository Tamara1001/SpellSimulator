using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Core;

namespace SpellSimulator.Input.Tools
{
    /// <summary>
    /// Lapiz: agrega puntos al trazo actual con un estabilizador low-pass (suaviza el temblor de la mano en VR).
    /// </summary>
    public class PenTool : IDrawingTool
    {
        /// <summary>Distancia UV minima entre puntos guardados.</summary>
        public const float MinPointSpacing = 0.005f;

        // Mayor = sigue mas rapido a la mano (menos lag, mas temblor).
        private readonly float stabilizerSpeed;
        private Stroke currentStroke;
        private Vector2 smoothedUV;

        public PenTool(float stabilizerSpeed = 8f)
        {
            this.stabilizerSpeed = stabilizerSpeed;
        }

        public void OnPointerDown(Vector2 uv, DrawingCanvas canvas)
        {
            currentStroke = new Stroke();
            currentStroke.AddPoint(uv);
            canvas.Session.AddStroke(currentStroke);
            smoothedUV = uv;
            canvas.DrawAt(uv, true);
        }

        public void OnPointerDrag(Vector2 uv, DrawingCanvas canvas)
        {
            if (currentStroke == null) return;

            // Suavizado exponencial independiente del framerate.
            float t = 1f - Mathf.Exp(-stabilizerSpeed * Time.deltaTime);
            smoothedUV = Vector2.Lerp(smoothedUV, uv, t);

            Vector2 last = currentStroke.Points[currentStroke.Points.Count - 1];
            if (Vector2.Distance(last, smoothedUV) > MinPointSpacing)
            {
                currentStroke.AddPoint(smoothedUV);
                canvas.DrawAt(smoothedUV, false);
            }
        }

        public void OnPointerUp()
        {
            currentStroke = null;
        }
    }
}
