using UnityEngine;
using SpellSimulator.Interfaces;
using SpellSimulator.Input.Tools;
using SpellSimulator.Canvas;
using SpellSimulator.Core;
using SpellSimulator.Player;

namespace SpellSimulator.World
{
    /// <summary>
    /// Representa un objeto fisico en el mundo 3D (Lapiz, Goma) que el jugador puede agarrar y usar.
    /// Utiliza un "puntero laser" invisible para proyectar el uso sobre un DrawingCanvas.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class PhysicalTool : MonoBehaviour, IInteractable, IUsable
    {
        public enum ToolKind { Pen, Eraser }

        [Header("Drawing Settings")]
        [SerializeField] private string toolName = "Lápiz";
        [SerializeField] private ToolKind kind = ToolKind.Pen;
        [SerializeField] private LayerMask canvasLayer;
        [SerializeField] private float pcMaxDrawDistance = 5f;
        [SerializeField] private float vrMaxDrawDistance = 10f; // Aumentado temporalmente para asegurar que el trazo llegue al lienzo
        
        [Tooltip("Transform desde donde sale el rayo en VR (la punta del lápiz)")]
        [SerializeField] private Transform raycastOrigin;

        private Rigidbody rb;
        private Collider col;
        private IDrawingTool internalTool;
        
        // Estado de agarre
        private Transform holdParent;
        
        // Estado de dibujo
        private DrawingCanvas currentCanvas;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
            
            // Inicializar la herramienta logica
            if (kind == ToolKind.Pen) internalTool = new PenTool();
            else internalTool = new EraserTool();

            if (raycastOrigin == null) raycastOrigin = transform;
        }

        // --- IInteractable ---

        public HandType PreferredHand => HandType.Right;

        public string GetInteractionPrompt()
        {
            return $"Agarrar {toolName}";
        }

        public void OnInteract(Transform holdPoint)
        {
            holdParent = holdPoint;
            
            // Desactivar fisicas
            rb.isKinematic = true;
            col.enabled = false;
            
            // Emparentar y resetear transform
            transform.SetParent(holdPoint);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        public void OnDrop()
        {
            holdParent = null;
            transform.SetParent(null);
            
            // Reactivar fisicas
            rb.isKinematic = false;
            col.enabled = true;
        }

        // --- IUsable ---

        public void OnUseStart()
        {
            if (TryShootLaser(out Vector2 uv, out DrawingCanvas hitCanvas))
            {
                currentCanvas = hitCanvas;
                
                // La herramienta física ahora usa la sesión de vectores del lienzo que golpeó
                internalTool.Initialize(currentCanvas.Session);
                internalTool.OnPointerDown(uv, currentCanvas);
            }
        }

        public void OnUseHold()
        {
            if (currentCanvas == null) return;

            if (TryShootLaser(out Vector2 uv, out DrawingCanvas hitCanvas))
            {
                // Si el rayo salio del canvas actual, abortamos el trazo
                if (hitCanvas != currentCanvas)
                {
                    OnUseEnd();
                    return;
                }

                internalTool.OnPointerDrag(uv, currentCanvas);
            }
        }

        public void OnUseEnd()
        {
            if (currentCanvas != null)
            {
                internalTool.OnPointerUp();
                currentCanvas = null;
            }
        }

        public void OnSecondaryUseStart()
        {
            // Disparamos el laser para ver a qué lienzo le estamos apuntando
            if (TryShootLaser(out Vector2 uv, out DrawingCanvas hitCanvas))
            {
                if (kind == ToolKind.Pen)
                {
                    // LAPIZ: Castear Hechizo
                    if (!hitCanvas.Session.IsEmpty)
                    {
                        var engine = Object.FindFirstObjectByType<Inference.SentisInferenceEngine>();
                        if (engine != null)
                        {
                            // Limpiar y castear
                            DrawingSession cleaned = StrokeCleaner.CleanSession(hitCanvas.Session, 0.005f, 3);
                            engine.ProcessVectorSession(cleaned);
                        }
                        else
                        {
                            Debug.LogWarning("[PhysicalTool] No se encontró un SentisInferenceEngine en la escena para castear.");
                        }
                    }
                }
                else if (kind == ToolKind.Eraser)
                {
                    // GOMA: Undo local en ese lienzo
                    if (hitCanvas.Session.UndoLastStroke())
                    {
                        hitCanvas.RedrawSession(hitCanvas.Session);
                    }
                }
            }
        }

        private bool TryShootLaser(out Vector2 uv, out DrawingCanvas hitCanvas)
        {
            uv = Vector2.zero;
            hitCanvas = null;

            Ray ray;
            float currentDistance;

            if (PlatformDetector.IsVR)
            {
                // En VR, sale de la punta de la varita. Si no se asigno en el inspector, usamos el transform del lapiz.
                Transform origin = raycastOrigin != null ? raycastOrigin : transform;
                ray = new Ray(origin.position, origin.forward);
                currentDistance = vrMaxDrawDistance;
            }
            else
            {
                // En PC, sale de la cámara
                Camera cam = Camera.main;
                if (cam == null) cam = FindAnyObjectByType<Camera>(); // Fallback seguro
                
                ray = new Ray(cam.transform.position, cam.transform.forward);
                currentDistance = pcMaxDrawDistance;
            }

            if (Physics.Raycast(ray, out RaycastHit hit, currentDistance, canvasLayer))
            {
                hitCanvas = hit.collider.GetComponent<DrawingCanvas>();
                if (hitCanvas != null)
                {
                    uv = hitCanvas.GetUVFromWorldPosition(hit.point);
                    return true;
                }
            }
            
            return false;
        }
    }
}
