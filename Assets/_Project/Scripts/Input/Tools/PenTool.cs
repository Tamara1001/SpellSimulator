using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Core;

namespace SpellSimulator.Input.Tools
{
    public class PenTool : IDrawingTool
    {
        private DrawingSession currentSession;
        private Stroke currentStroke;

        public void Initialize(DrawingSession session)
        {
            currentSession = session;
        }

        public void OnEquip() { }
        public void OnUnequip() 
        { 
            currentStroke = null; // Clean up if unequipped while drawing
        }

        public void OnPointerDown(Vector2 uv, DrawingCanvas canvas)
        {
            currentStroke = new Stroke();
            currentStroke.AddPoint(uv);
            currentSession.AddStroke(currentStroke);
            
            if (canvas != null) canvas.DrawAt(uv, true);
        }

        public void OnPointerDrag(Vector2 uv, DrawingCanvas canvas)
        {
            if (currentStroke == null) return;

            // Solo agregar el punto si nos movimos lo suficiente
            if (currentStroke.Points.Count == 0 || Vector2.Distance(currentStroke.Points[currentStroke.Points.Count - 1], uv) > 0.005f)
            {
                currentStroke.AddPoint(uv);
                if (canvas != null) canvas.DrawAt(uv, false);
            }
        }

        public void OnPointerUp()
        {
            currentStroke = null;
        }
    }
}
