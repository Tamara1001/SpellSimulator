using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Input.Tools;
using SpellSimulator.Interfaces;
using SpellSimulator.Player;

namespace SpellSimulator.World
{
    /// <summary>
    /// Herramienta fisica (lapiz, goma) que el jugador agarra y usa. Proyecta un rayo sobre un DrawingCanvas:
    /// en PC desde la camara (mira), en VR desde la punta de la herramienta.
    /// Uso primario = dibujar/borrar. Secundario = deshacer. El hechizo se lanza solo al cerrar el anillo.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class PhysicalTool : MonoBehaviour, IInteractable, IUsable
    {
        public enum ToolKind { Pen, Eraser }

        [Header("Drawing Settings")]
        [SerializeField] private string toolName = "Lapiz";
        [SerializeField] private ToolKind kind = ToolKind.Pen;
        [SerializeField] private LayerMask canvasLayer;
        [SerializeField] private float pcMaxDrawDistance = 5f;
        [SerializeField] private float vrMaxDrawDistance = 10f;
        [SerializeField, Tooltip("Transform desde donde sale el rayo en VR (la punta de la herramienta).")]
        private Transform raycastOrigin;

        [Header("Casting")]
        [SerializeField, Tooltip("Servicio que evalua el sello al terminar cada trazo y lanza el hechizo.")]
        private SpellCaster spellCaster;

        private Rigidbody rb;
        private Collider col;
        private IDrawingTool tool;
        private DrawingCanvas currentCanvas;
        private Camera cachedCamera;

        public HandType PreferredHand => HandType.Right;
        public ToolKind Kind => kind;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
            tool = kind == ToolKind.Pen ? new PenTool() : (IDrawingTool)new EraserTool();
            if (raycastOrigin == null) raycastOrigin = transform;
        }

        // --- IInteractable ---

        public string GetInteractionPrompt() => $"Agarrar {toolName}";

        public void OnInteract(Transform holdPoint)
        {
            rb.isKinematic = true;
            col.enabled = false;
            transform.SetParent(holdPoint);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        public void OnDrop()
        {
            OnUseEnd();
            transform.SetParent(null);
            rb.isKinematic = false;
            col.enabled = true;
        }

        // --- IUsable ---

        public void OnUseStart()
        {
            if (!TryShootLaser(out Vector2 uv, out DrawingCanvas canvas)) return;
            currentCanvas = canvas;
            tool.OnPointerDown(uv, canvas);
        }

        public void OnUseHold()
        {
            if (currentCanvas == null)
            {
                // Permite empezar a dibujar si el gatillo ya estaba apretado al llegar al lienzo.
                OnUseStart();
                return;
            }

            if (!TryShootLaser(out Vector2 uv, out DrawingCanvas canvas)) return;
            if (canvas != currentCanvas)
            {
                OnUseEnd();
                return;
            }
            tool.OnPointerDrag(uv, currentCanvas);
        }

        public void OnUseEnd()
        {
            if (currentCanvas == null) return;
            tool.OnPointerUp();
            DrawingCanvas finished = currentCanvas;
            currentCanvas = null;

            // Canon: el hechizo se lanza al cerrar el anillo. Solo la tinta puede cerrarlo; borrar solo actualiza la fase.
            if (spellCaster == null)
            {
                Debug.LogWarning($"[PhysicalTool] {name} no tiene SpellCaster asignado.", this);
                return;
            }
            if (kind == ToolKind.Pen) spellCaster.OnStrokeFinished(finished);
            else spellCaster.OnSealEdited(finished);
        }

        /// <summary>Secundario: deshacer el ultimo trazo del lienzo apuntado.</summary>
        public void OnSecondaryUseStart()
        {
            if (!TryShootLaser(out _, out DrawingCanvas canvas)) return;
            if (!canvas.Session.UndoLastStroke()) return;

            canvas.Redraw();
            if (spellCaster != null) spellCaster.OnSealEdited(canvas);
        }

        private bool TryShootLaser(out Vector2 uv, out DrawingCanvas canvas)
        {
            uv = Vector2.zero;
            canvas = null;

            Ray ray;
            float distance;
            if (PlatformDetector.IsVR)
            {
                ray = new Ray(raycastOrigin.position, raycastOrigin.forward);
                distance = vrMaxDrawDistance;
            }
            else
            {
                if (cachedCamera == null) cachedCamera = Camera.main;
                if (cachedCamera == null) return false;
                ray = new Ray(cachedCamera.transform.position, cachedCamera.transform.forward);
                distance = pcMaxDrawDistance;
            }

            // Los lienzos sostenidos son triggers: hay que pedir que el rayo los detecte.
            if (!Physics.Raycast(ray, out RaycastHit hit, distance, canvasLayer, QueryTriggerInteraction.Collide)) return false;
            if (!hit.collider.TryGetComponent(out canvas)) return false;
            uv = canvas.GetUVFromWorldPosition(hit.point);
            return true;
        }
    }
}
