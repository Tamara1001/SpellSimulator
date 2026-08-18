using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Core;

namespace SpellSimulator.Input.Tools
{
    public class PenTool : IDrawingTool
    {
        private DrawingSession currentSession;
        private Stroke currentStroke;
        
        // --- Estabilizador VR ---
        private Vector2 smoothedUV;
        private bool isFirstPoint = true;
        // Velocidad del estabilizador (menor = más suave pero más "lag" percibido, mayor = más rápido pero más tembloroso)
        private float stabilizerSpeed = 8f; 

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
            
            // Inicializar el estabilizador en el punto de impacto
            smoothedUV = uv;
            isFirstPoint = true;
            
            if (canvas != null) canvas.DrawAt(uv, true);
        }

        public void OnPointerDrag(Vector2 uv, DrawingCanvas canvas)
        {
            if (currentStroke == null) return;

            // 1. Estabilizador (Low-Pass Filter)
            if (isFirstPoint)
            {
                smoothedUV = uv;
                isFirstPoint = false;
            }
            else
            {
                // En VR, movemos la tinta suavemente hacia la mano real en base al tiempo
                smoothedUV = Vector2.Lerp(smoothedUV, uv, Time.deltaTime * stabilizerSpeed);
            }

            // 2. Solo agregar el punto a la geometría si nos movimos lo suficiente desde el último punto suavizado
            if (currentStroke.Points.Count == 0 || Vector2.Distance(currentStroke.Points[currentStroke.Points.Count - 1], smoothedUV) > 0.005f)
            {
                currentStroke.AddPoint(smoothedUV);
                if (canvas != null) canvas.DrawAt(smoothedUV, false);
            }
        }

        public void OnPointerUp()
        {
            currentStroke = null;
        }
    }
}
