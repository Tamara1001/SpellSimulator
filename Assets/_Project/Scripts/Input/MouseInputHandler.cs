using UnityEngine;
using UnityEngine.InputSystem;
using SpellSimulator.Canvas;
using SpellSimulator.Core;

namespace SpellSimulator.Input
{
    /// <summary>
    /// Recolecta el input del raton y lo convierte en trazos vectoriales.
    /// </summary>
    public class MouseInputHandler : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private LayerMask canvasLayerMask = Physics.DefaultRaycastLayers;
        [SerializeField] private float maxRaycastDistance = 100f;

        [Header("Inference")]
        [SerializeField] private Inference.SentisInferenceEngine inferenceEngine;
        [SerializeField] private DrawingCanvas fallbackCanvas;

        [Header("Settings")]
        [SerializeField, Range(0.001f, 0.05f), Tooltip("Tolerancia de Douglas-Peucker. Mayor = mas simplificado.")] 
        private float simplificationTolerance = 0.005f;
        [SerializeField, Range(1, 10), Tooltip("Resolucion del Catmull-Rom. Mayor = curvas mas suaves.")] 
        private int splineResolution = 3;

        public enum ToolType { Pen, Eraser }

        private DrawingSession currentSession = new DrawingSession();
        private DrawingCanvas lastUsedCanvas;
        
        private System.Collections.Generic.Dictionary<ToolType, Input.Tools.IDrawingTool> tools;
        private Input.Tools.IDrawingTool activeTool;

        private void Start()
        {
            if (mainCamera == null) mainCamera = Camera.main;

            tools = new System.Collections.Generic.Dictionary<ToolType, Input.Tools.IDrawingTool>
            {
                { ToolType.Pen, new Input.Tools.PenTool() },
                { ToolType.Eraser, new Input.Tools.EraserTool() }
            };

            foreach (var tool in tools.Values)
            {
                tool.Initialize(currentSession);
            }

            EquipTool(ToolType.Pen);
        }

        public void EquipTool(ToolType type)
        {
            if (activeTool != null)
                activeTool.OnUnequip();

            if (tools.TryGetValue(type, out Input.Tools.IDrawingTool tool))
            {
                activeTool = tool;
                activeTool.OnEquip();
            }
        }

        // Metodos para facil acceso desde Unity UI Buttons
        public void EquipPen() => EquipTool(ToolType.Pen);
        public void EquipEraser() => EquipTool(ToolType.Eraser);

        private void Update()
        {
            if (Mouse.current == null) return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
                HandlePointerDown();
            else if (Mouse.current.leftButton.isPressed)
                HandlePointerDrag();
            else if (Mouse.current.leftButton.wasReleasedThisFrame)
                HandlePointerUp();
        }

        private void HandlePointerDown()
        {
            if (activeTool == null) return;

            if (TryGetCanvasUV(out Vector2 uv, out DrawingCanvas canvasHit))
            {
                lastUsedCanvas = canvasHit;
                activeTool.OnPointerDown(uv, canvasHit);
            }
        }

        private void HandlePointerDrag()
        {
            if (activeTool == null) return;

            if (TryGetCanvasUV(out Vector2 uv, out DrawingCanvas canvasHit))
            {
                activeTool.OnPointerDrag(uv, canvasHit);
            }
        }

        private void HandlePointerUp()
        {
            if (activeTool != null)
            {
                activeTool.OnPointerUp();
            }
        }

        public void TriggerCast()
        {
            if (currentSession.IsEmpty)
            {
                Debug.LogWarning("[Input] TriggerCast: La sesion de dibujo esta vacia.");
                return;
            }

            if (inferenceEngine != null)
            {
                DrawingSession cleanedSession = StrokeCleaner.CleanSession(currentSession, simplificationTolerance, splineResolution);
                inferenceEngine.ProcessVectorSession(cleanedSession);
            }
            else
                Debug.LogWarning("[Input] InferenceEngine no asignado.");

            // Eliminado el auto-clear para permitir edicion post-casteo.
            // Para limpiar, usar el boton en la UI que llame a ClearCanvas().
        }

        public void ClearCanvas()
        {
            currentSession.Clear();
            if (lastUsedCanvas != null) lastUsedCanvas.ClearCanvas();
            else if (fallbackCanvas != null) fallbackCanvas.ClearCanvas();
        }

        public void Undo()
        {
            if (currentSession.UndoLastStroke())
            {
                if (lastUsedCanvas != null) lastUsedCanvas.RedrawSession(currentSession);
                else if (fallbackCanvas != null) fallbackCanvas.RedrawSession(currentSession);
            }
        }

        private bool TryGetCanvasUV(out Vector2 uv, out DrawingCanvas canvasHit)
        {
            uv = Vector2.zero;
            canvasHit = null;

            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, canvasLayerMask))
            {
                canvasHit = hit.collider.GetComponent<DrawingCanvas>();
                if (canvasHit == null) canvasHit = fallbackCanvas;

                if (canvasHit != null)
                {
                    uv = hit.textureCoord;
                    return true;
                }
            }

            return false;
        }
    }
}
