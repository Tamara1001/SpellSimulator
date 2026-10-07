using UnityEngine;
using SpellSimulator.Canvas;

namespace SpellSimulator.Input.Tools
{
    /// <summary>Goma: elimina trazos enteros que pasen cerca del puntero.</summary>
    public class EraserTool : IDrawingTool
    {
        // Radio en metros sobre la superficie: igual en lienzos de cualquier tamanio o proporcion.
        private readonly float eraseRadiusMeters;

        public EraserTool(float eraseRadiusMeters = 0.025f)
        {
            this.eraseRadiusMeters = eraseRadiusMeters;
        }

        public void OnPointerDown(Vector2 uv, DrawingCanvas canvas) => EraseAt(uv, canvas);
        public void OnPointerDrag(Vector2 uv, DrawingCanvas canvas) => EraseAt(uv, canvas);
        public void OnPointerUp() { }

        private void EraseAt(Vector2 uv, DrawingCanvas canvas)
        {
            var strokes = canvas.Session.Strokes;
            if (strokes.Count == 0) return;

            Vector2 size = canvas.WorldSize;
            float radiusSq = eraseRadiusMeters * eraseRadiusMeters;
            bool erased = false;

            for (int i = strokes.Count - 1; i >= 0; i--)
            {
                foreach (Vector2 point in strokes[i].Points)
                {
                    Vector2 delta = Vector2.Scale(point - uv, size);
                    if (delta.sqrMagnitude <= radiusSq)
                    {
                        strokes.RemoveAt(i);
                        erased = true;
                        break;
                    }
                }
            }

            if (erased) canvas.Redraw();
        }
    }
}
