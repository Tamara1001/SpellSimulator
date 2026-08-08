using UnityEngine;
using UnityEngine.InputSystem;
using SpellSimulator.Canvas;

namespace SpellSimulator.Input
{
    /// <summary>
    /// Módulo 2: Controlador de Entrada (Prototipo PC).
    /// Dispara Raycasts desde la cámara principal usando la posición del mouse
    /// para interactuar con el DrawingCanvas.
    /// </summary>
    public class MouseInputHandler : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField, Tooltip("Cámara desde la que se emitirá el Raycast. Si está vacía, usará Camera.main.")]
        private Camera mainCamera;
        
        [SerializeField, Tooltip("Capa física donde reside el lienzo (para optimizar el Raycast).")]
        private LayerMask canvasLayerMask = Physics.DefaultRaycastLayers;

        [Header("Settings")]
        [SerializeField, Tooltip("Distancia máxima del Raycast.")]
        private float maxRaycastDistance = 100f;

        [Header("Inference")]
        [SerializeField, Tooltip("Motor de inferencia para procesar los trazos.")]
        private Inference.SentisInferenceEngine inferenceEngine;

        // Referencias cacheadas para evitar GC Allocations en el Update
        private DrawingCanvas currentCanvas;
        private Collider lastColliderHit;

        private void Start()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (Mouse.current == null) return;

            // Clic inicial (nuevo trazo)
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                HandlePointerDown();
            }
            // Arrastre continuo (continuar trazo)
            else if (Mouse.current.leftButton.isPressed)
            {
                HandlePointerDrag();
            }
            // Soltar clic (fin del trazo)
            else if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                HandlePointerUp();
            }
        }

        private void HandlePointerDown()
        {
            if (TryGetCanvasUV(out Vector2 uv, out DrawingCanvas canvasHit))
            {
                currentCanvas = canvasHit;
                currentCanvas.DrawAt(uv, true);
            }
        }

        private void HandlePointerDrag()
        {
            if (currentCanvas == null) return;

            if (TryGetCanvasUV(out Vector2 uv, out DrawingCanvas canvasHit))
            {
                if (canvasHit == currentCanvas)
                {
                    currentCanvas.DrawAt(uv, false);
                }
                else
                {
                    // Salió del canvas original
                    currentCanvas = null;
                }
            }
            else
            {
                // Ignorar si el raycast no golpea nada durante el arrastre (movimientos rápidos)
                // para no perder la referencia del lienzo y abortar la inferencia.
            }
        }

        private void HandlePointerUp()
        {
            // Limpiar la caché de colisiones para permitir nuevos trazos en el mismo lienzo
            lastColliderHit = null;

            if (currentCanvas != null)
            {
                var canvasToProcess = currentCanvas;
                currentCanvas = null;
                
                if (inferenceEngine != null)
                {
                    RenderTexture canvasTex = canvasToProcess.GetRenderTexture();
                    inferenceEngine.ProcessCanvas(canvasTex);
                }
                else
                {
                    Debug.LogWarning("MouseInputHandler: InferenceEngine no está asignado.");
                }
            }
        }

        /// <summary>
        /// Emite un Raycast y devuelve la coordenada UV si golpea un DrawingCanvas.
        /// Optimizado para evitar GetComponent en cada frame si golpeamos el mismo colisionador.
        /// </summary>
        private bool TryGetCanvasUV(out Vector2 uv, out DrawingCanvas canvasHit)
        {
            uv = Vector2.zero;
            canvasHit = null;

            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, canvasLayerMask))
            {
                // Optimización: Solo hacer GetComponent si cambiamos de colisionador
                if (hit.collider != lastColliderHit)
                {
                    lastColliderHit = hit.collider;
                    currentCanvas = hit.collider.GetComponent<DrawingCanvas>();
                }
                
                if (currentCanvas != null)
                {
                    canvasHit = currentCanvas;
                    uv = hit.textureCoord;
                    return true;
                }
            }
            else
            {
                lastColliderHit = null;
            }

            return false;
        }
    }
}
