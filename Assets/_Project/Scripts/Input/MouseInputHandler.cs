using UnityEngine;
using UnityEngine.InputSystem;
using SpellSimulator.Canvas;

namespace SpellSimulator.Input
{
    /// <summary>
    /// Modulo 2: Controlador de Entrada.
    /// Acumula trazos del usuario en el canvas. La inferencia se dispara
    /// exclusivamente via TriggerCast(), que debe conectarse a un boton UI.
    /// </summary>
    public class MouseInputHandler : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private LayerMask canvasLayerMask = Physics.DefaultRaycastLayers;
        [SerializeField] private float maxRaycastDistance = 100f;

        [Header("Inference")]
        [SerializeField] private Inference.SentisInferenceEngine inferenceEngine;

        [Header("Settings")]
        [SerializeField, Tooltip("Limpia el canvas automaticamente despues de lanzar el hechizo.")]
        private bool clearCanvasAfterCast = true;

        private DrawingCanvas currentCanvas;
        private DrawingCanvas lastUsedCanvas;
        private Collider lastColliderHit;

        private void Start()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;
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
                currentCanvas  = canvasHit;
                lastUsedCanvas = canvasHit;
                currentCanvas.DrawAt(uv, true);
            }
        }

        private void HandlePointerDrag()
        {
            if (currentCanvas == null) return;

            if (TryGetCanvasUV(out Vector2 uv, out DrawingCanvas canvasHit))
            {
                if (canvasHit == currentCanvas)
                    currentCanvas.DrawAt(uv, false);
            }
        }

        private void HandlePointerUp()
        {
            lastColliderHit = null;
            currentCanvas   = null;
            // No disparamos inferencia aqui. El usuario usa el boton.
        }

        /// <summary>
        /// Lanzar el hechizo. Conectar al OnClick del boton UI en el Inspector.
        /// Si no hay tinta en el canvas, no hace nada.
        /// </summary>
        public void TriggerCast()
        {
            if (lastUsedCanvas == null || !lastUsedCanvas.HasInk)
            {
                Debug.Log("[Input] TriggerCast: no hay tinta en el canvas.");
                return;
            }

            if (inferenceEngine != null)
                inferenceEngine.ProcessCanvas(lastUsedCanvas.GetRenderTexture());
            else
                Debug.LogWarning("[Input] InferenceEngine no asignado.");

            if (clearCanvasAfterCast)
                lastUsedCanvas.ClearCanvas();

            lastUsedCanvas.ResetInkBounds();
        }

        private bool TryGetCanvasUV(out Vector2 uv, out DrawingCanvas canvasHit)
        {
            uv = Vector2.zero;
            canvasHit = null;

            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, canvasLayerMask))
            {
                if (hit.collider != lastColliderHit)
                {
                    lastColliderHit = hit.collider;
                    currentCanvas   = hit.collider.GetComponent<DrawingCanvas>();
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
