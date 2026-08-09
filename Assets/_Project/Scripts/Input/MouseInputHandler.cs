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

        private DrawingSession currentSession = new DrawingSession();
        private Stroke currentStroke;
        private DrawingCanvas lastUsedCanvas;

        private void Start()
        {
            if (mainCamera == null) mainCamera = Camera.main;
        }

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
            if (TryGetCanvasUV(out Vector2 uv, out DrawingCanvas canvasHit))
            {
                lastUsedCanvas = canvasHit;
                currentStroke = new Stroke();
                currentStroke.AddPoint(uv);
                currentSession.AddStroke(currentStroke);
                
                // Visual feedback opcional en el lienzo
                if (canvasHit != null) canvasHit.DrawAt(uv, true);
            }
        }

        private void HandlePointerDrag()
        {
            if (currentStroke == null) return;

            if (TryGetCanvasUV(out Vector2 uv, out DrawingCanvas canvasHit))
            {
                // Solo agregar el punto si nos movimos lo suficiente para no inundar la lista
                if (currentStroke.Points.Count == 0 || Vector2.Distance(currentStroke.Points[currentStroke.Points.Count - 1], uv) > 0.005f)
                {
                    currentStroke.AddPoint(uv);
                    if (canvasHit != null) canvasHit.DrawAt(uv, false);
                }
            }
        }

        private void HandlePointerUp()
        {
            currentStroke = null;
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
