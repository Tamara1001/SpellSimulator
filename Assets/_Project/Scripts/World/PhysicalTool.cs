using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Core;
using SpellSimulator.DebugTools;
using SpellSimulator.Input.Tools;
using SpellSimulator.Interfaces;
using SpellSimulator.Player;

namespace SpellSimulator.World
{
    /// <summary>
    /// Herramienta fisica (lapiz, goma, trapo) que el jugador agarra y usa. Proyecta un rayo sobre un DrawingCanvas:
    /// en PC desde la camara (mira), en VR desde la punta (laser o contacto).
    /// Uso primario = dibujar/borrar/limpiar. Secundario = deshacer. El hechizo se lanza solo al cerrar el anillo.
    /// El lapiz gasta tinta de conjuro y se recarga en tinteros (R en PC, mojando la punta en VR).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class PhysicalTool : MonoBehaviour, IInteractable, IUsable, IInkUser
    {
        public enum ToolKind { Pen, Eraser, Rag }

        [Header("Drawing Settings")]
        [SerializeField] private string toolName = "Lapiz";
        [SerializeField] private ToolKind kind = ToolKind.Pen;
        [SerializeField] private LayerMask canvasLayer;
        [SerializeField] private float pcMaxDrawDistance = 5f;
        [SerializeField] private float vrMaxDrawDistance = 10f;
        [SerializeField, Tooltip("Transform desde donde sale el rayo en VR (la punta de la herramienta).")]
        private Transform raycastOrigin;
        [SerializeField, Tooltip("Velocidad del estabilizador del lapiz en PC. Alto = sin retraso (el mouse no tiembla).")]
        private float pcStabilizerSpeed = 40f;
        [SerializeField, Tooltip("Velocidad del estabilizador en VR. Bajo = mas suave contra el temblor, mas retraso.")]
        private float vrStabilizerSpeed = 14f;

        [Header("VR")]
        [SerializeField, Tooltip("[VR] Dibujar solo cuando la punta toca el papel (en vez del laser). Experimental.")]
        private bool vrContactDrawing;
        [SerializeField, Tooltip("[VR] Distancia (m) desde la punta que cuenta como contacto.")]
        private float vrContactReach = 0.04f;
        [SerializeField, Tooltip("[VR] Radio (m) alrededor de la punta para mojarla en un tintero.")]
        private float vrDipRadius = 0.04f;

        [Header("Tinta (solo lapiz)")]
        [SerializeField] private InkReservoir ink = new InkReservoir(100f, 100f);
        [SerializeField, Tooltip("Tinta gastada por metro de trazo sobre el papel.")]
        private float inkPerMeter = 8f;
        [SerializeField, Tooltip("Color del lapiz lleno / vacio (indicador diegetico de tinta).")]
        private Color fullInkColor = new Color(0.12f, 0.08f, 0.06f);
        [SerializeField] private Color emptyInkColor = new Color(0.75f, 0.72f, 0.68f);

        [Header("Casting")]
        [SerializeField, Tooltip("Servicio que evalua el sello al terminar cada trazo y lanza el hechizo.")]
        private SpellCaster spellCaster;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Collider[] dipHits = new Collider[8];

        private Rigidbody rb;
        private Collider col;
        private Renderer bodyRenderer;
        private MaterialPropertyBlock block;
        private IDrawingTool tool;
        private DrawingCanvas currentCanvas;
        private Vector2 lastUv;
        private Camera cachedCamera;
        private bool held;

        public HandType PreferredHand => HandType.Right;
        public ToolKind Kind => kind;
        public bool UsesInk => kind == ToolKind.Pen;
        public InkReservoir Ink => ink;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
            bodyRenderer = GetComponentInChildren<Renderer>();
            block = new MaterialPropertyBlock();
            if (raycastOrigin == null) raycastOrigin = transform;

            float stabilizer = PlatformDetector.IsVR ? vrStabilizerSpeed : pcStabilizerSpeed;
            switch (kind)
            {
                case ToolKind.Pen: tool = new PenTool(stabilizer); break;
                case ToolKind.Eraser: tool = new EraserTool(); break;
                default: tool = new RagTool(); break;
            }
            RefreshInkVisual();
        }

        private void Update()
        {
            // VR: mojar la punta dentro de un tintero recarga.
            if (!held || !UsesInk || !PlatformDetector.IsVR || ink.Missing <= 0f) return;
            int count = Physics.OverlapSphereNonAlloc(raycastOrigin.position, vrDipRadius, dipHits, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var well = dipHits[i].GetComponentInParent<Inkwell>();
                if (well == null) continue;
                if (well.Refill(ink) > 0f)
                {
                    RefreshInkVisual();
                    Haptics.Pulse(HandType.Right, 0.3f, 0.08f);
                }
                break;
            }
        }

        // --- IInteractable ---

        public string GetInteractionPrompt() => $"Agarrar {toolName}";

        public void OnInteract(Transform holdPoint)
        {
            held = true;
            rb.isKinematic = true;
            col.enabled = false;
            transform.SetParent(holdPoint);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        public void OnDrop()
        {
            OnUseEnd();
            held = false;
            transform.SetParent(null);
            rb.isKinematic = false;
            col.enabled = true;
        }

        // --- IUsable ---

        public void OnUseStart()
        {
            if (!TryShootLaser(out Vector2 uv, out DrawingCanvas canvas)) return;
            if (UsesInk && ink.IsEmpty)
            {
                ReportNoInk();
                return;
            }

            currentCanvas = canvas;
            lastUv = uv;
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

            if (UsesInk && !SpendInk(uv, canvas))
            {
                OnUseEnd();
                ReportNoInk();
                return;
            }

            tool.OnPointerDrag(uv, currentCanvas);
            lastUv = uv;
            if (PlatformDetector.IsVR && UsesInk) Haptics.Pulse(HandType.Right, Haptics.StrokeAmplitude, 0.02f);
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

        /// <summary>Lo llama el jugador al recargar (R) con una fuente de tinta.</summary>
        public float RefillFrom(IInkSource source)
        {
            if (!UsesInk) return 0f;
            float amount = source.Refill(ink);
            RefreshInkVisual();
            return amount;
        }

        private bool SpendInk(Vector2 uv, DrawingCanvas canvas)
        {
            float meters = Vector2.Scale(uv - lastUv, canvas.WorldSize).magnitude;
            bool ok = ink.TryConsume(meters * inkPerMeter);
            RefreshInkVisual();
            return ok;
        }

        private void ReportNoInk()
        {
            DiagnosticBus.OnDiagnosticsUpdated?.Invoke("<b>[Tinta]</b> El lapiz esta seco. Recarga en un tintero (R en PC).");
            Haptics.Pulse(HandType.Right, 0.5f, 0.15f);
        }

        private void RefreshInkVisual()
        {
            if (!UsesInk || bodyRenderer == null) return;
            bodyRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, Color.Lerp(emptyInkColor, fullInkColor, ink.Fraction));
            bodyRenderer.SetPropertyBlock(block);
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
                distance = vrContactDrawing ? vrContactReach : vrMaxDrawDistance;
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
