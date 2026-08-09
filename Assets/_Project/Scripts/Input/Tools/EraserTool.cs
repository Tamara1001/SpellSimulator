using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Core;

namespace SpellSimulator.Input.Tools
{
    public class EraserTool : IDrawingTool
    {
        private DrawingSession currentSession;
        private float eraseRadius = 0.05f; // Radio de interseccion para borrar

        public void Initialize(DrawingSession session)
        {
            currentSession = session;
        }

        public void OnEquip() { }
        public void OnUnequip() { }

        public void OnPointerDown(Vector2 uv, DrawingCanvas canvas)
        {
            EraseAt(uv, canvas);
        }

        public void OnPointerDrag(Vector2 uv, DrawingCanvas canvas)
        {
            EraseAt(uv, canvas);
        }

        public void OnPointerUp() { }

        private void EraseAt(Vector2 uv, DrawingCanvas canvas)
        {
            if (currentSession == null || currentSession.IsEmpty) return;

            bool erasedSomething = false;
            
            // Iteramos al reves para poder eliminar elementos de forma segura
            for (int i = currentSession.Strokes.Count - 1; i >= 0; i--)
            {
                Stroke stroke = currentSession.Strokes[i];
                foreach (Vector2 point in stroke.Points)
                {
                    if (Vector2.Distance(point, uv) <= eraseRadius)
                    {
                        currentSession.Strokes.RemoveAt(i);
                        erasedSomething = true;
                        break; // Dejamos de revisar este trazo porque ya lo eliminamos
                    }
                }
            }

            // Si tocamos algun trazo, forzamos al lienzo a repintarse al instante
            if (erasedSomething && canvas != null)
            {
                canvas.RedrawSession(currentSession);
            }
        }
    }
}
