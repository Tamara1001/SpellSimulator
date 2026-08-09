using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Core;

namespace SpellSimulator.Input.Tools
{
    /// <summary>
    /// Interfaz base para todas las herramientas de edicion del simulador.
    /// </summary>
    public interface IDrawingTool
    {
        void Initialize(DrawingSession session);
        void OnEquip();
        void OnUnequip();
        void OnPointerDown(Vector2 uv, DrawingCanvas canvas);
        void OnPointerDrag(Vector2 uv, DrawingCanvas canvas);
        void OnPointerUp();
    }
}
