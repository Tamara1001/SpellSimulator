using UnityEngine;
using SpellSimulator.Canvas;

namespace SpellSimulator.Input.Tools
{
    /// <summary>Trapo: frotarlo sobre el lienzo lo limpia entero cuando el recorrido supera un umbral.</summary>
    public class RagTool : IDrawingTool
    {
        private readonly float wipeLengthToClear;
        private float wiped;
        private Vector2 last;

        /// <summary>True si el ultimo uso limpio el lienzo (lo consulta PhysicalTool para actualizar el sello).</summary>
        public bool ClearedThisUse { get; private set; }

        /// <param name="wipeLengthToClear">Recorrido en UV (ida y vuelta suma) para limpiar.</param>
        public RagTool(float wipeLengthToClear = 1.2f)
        {
            this.wipeLengthToClear = wipeLengthToClear;
        }

        public void OnPointerDown(Vector2 uv, DrawingCanvas canvas)
        {
            wiped = 0f;
            last = uv;
            ClearedThisUse = false;
        }

        public void OnPointerDrag(Vector2 uv, DrawingCanvas canvas)
        {
            if (ClearedThisUse) return;
            wiped += Vector2.Distance(last, uv);
            last = uv;
            if (wiped < wipeLengthToClear || canvas.Session.IsEmpty) return;

            canvas.Clear();
            ClearedThisUse = true;
        }

        public void OnPointerUp() { }
    }
}
