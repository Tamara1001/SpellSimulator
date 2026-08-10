using UnityEngine;
using SpellSimulator.Interfaces;
using SpellSimulator.Input.Tools;
using SpellSimulator.Canvas;
using SpellSimulator.Core;

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

        [Header("Settings")]
        [SerializeField] private string toolName = "Lápiz";
        [SerializeField] private ToolKind kind = ToolKind.Pen;
        [SerializeField] private Transform raycastOrigin; // La punta del lapiz
        [SerializeField] private LayerMask canvasLayer;
        
        [Header("PC vs VR Mode")]
        [SerializeField, Tooltip("Si es True, el trazo sale de la punta del lapiz (Requiere tocar el lienzo). Si es False, el trazo sale del centro de la pantalla (Láser FPS).")]
        private bool isVRMode = false;
        [SerializeField, Tooltip("Distancia maxima en VR Mode. Valores bajos (0.25) obligan al contacto fisico real.")] 
        private float vrMaxDrawDistance = 0.25f;
        [SerializeField, Tooltip("Distancia maxima en PC Mode. Láser a distancia.")] 
        private float pcMaxDrawDistance = 10f;

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

            if (isVRMode)
            {
                // En VR, sale de la punta de la varita
                ray = new Ray(raycastOrigin.position, raycastOrigin.forward);
                currentDistance = vrMaxDrawDistance;
            }
            else
            {
                // En PC, sale del centro de la pantalla (mira del jugador) para que sea 100% preciso e intuitivo
                Transform cam = Camera.main.transform;
                ray = new Ray(cam.position, cam.forward);
                currentDistance = pcMaxDrawDistance;
            }

            if (Physics.Raycast(ray, out RaycastHit hit, currentDistance, canvasLayer))
            {
                hitCanvas = hit.collider.GetComponent<DrawingCanvas>();
                if (hitCanvas != null)
                {
                    uv = hit.textureCoord;
                    return true;
                }
            }
            return false;
        }
    }
}
