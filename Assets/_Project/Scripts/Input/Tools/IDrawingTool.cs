using UnityEngine;
using SpellSimulator.Canvas;

namespace SpellSimulator.Input.Tools
{
    /// <summary>
    /// Herramienta logica que edita la sesion de un lienzo a partir de coordenadas UV.
    /// No sabe de donde viene la UV (mouse, rayo VR, contacto).
    /// </summary>
    public interface IDrawingTool
    {
        void OnPointerDown(Vector2 uv, DrawingCanvas canvas);
        void OnPointerDrag(Vector2 uv, DrawingCanvas canvas);
        void OnPointerUp();
    }
}
