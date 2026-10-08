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
    public class PhysicalTool : MonoBehaviour, IInteractable, IUsable, IInkUser, IHudInfo, IWidthSelectable
    {
        public enum ToolKind { Pen, Eraser, Rag }

        [Header("Definicion")]
        [SerializeField, Tooltip("Datos de la herramienta. Si esta asignado, pisa los campos de abajo.")]
        private ToolDefinition definition;

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
        [SerializeField, Tooltip("Ancho del trazo en metros (0 = el del lienzo).")]
        private float strokeWidth;

        [Header("Grosor (teclas 5 a 0)")]
        [SerializeField, Tooltip("Multiplicadores del ancho base de la pluma o el pincel para las teclas 5, 6, 7, 8, 9 y 0.")]
        private float[] widthPresets = { 0.04f, 0.09f, 0.2f, 0.45f, 1f, 2.2f };
        [SerializeField] private string[] widthNames = { "finisimo", "muy fino", "fino", "medio", "normal", "grueso" };

        [Header("VR")]
        [SerializeField, Tooltip("[VR] Dibujar solo cuando la punta toca el papel (en vez del laser). Experimental.")]
        private bool vrContactDrawing;
        [SerializeField, Tooltip("[VR] Distancia (m) desde la punta que cuenta como contacto.")]
        private float vrContactReach = 0.04f;
        [SerializeField, Tooltip("[VR] Radio (m) alrededor de la punta para mojarla en un tintero.")]
        private float vrDipRadius = 0.04f;

        [SerializeField, Tooltip("Parte del modelo que se tiñe segun la tinta que queda (cristal de la pluma). Si falta, todo el cuerpo.")]
        private Renderer inkIndicator;

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
        private Vector3 lastRayOrigin;
        private Camera cachedCamera;
        private bool held;
        private PlayerInputReader input;
        private float baseStrokeWidth;
        private int widthIndex = 4;

        /// <summary>(herramienta, usando) al empezar y terminar de dibujar/borrar/limpiar sobre un lienzo.</summary>
        public static event System.Action<PhysicalTool, bool> UseChanged;
        /// <summary>(herramienta) al recargar tinta.</summary>
        public static event System.Action<PhysicalTool> Refilled;
        /// <summary>(herramienta) al agarrarla.</summary>
        public static event System.Action<PhysicalTool> PickedUp;

        public HandType PreferredHand => HandType.Right;

        /// <summary>
        /// Inclinacion en PC para que parezca sostenida: pluma y pincel (punta en -Y) con la punta hacia adelante y un poco
        /// abajo; goma y trapo (punta en +Y) con la punta hacia adelante.
        /// </summary>
        private float PcHoldPitch => kind == ToolKind.Pen ? -72f : 72f;
        public ToolKind Kind => kind;
        public bool UsesInk => kind == ToolKind.Pen;

        // ---- Interfaz (HUD de abajo)
        public string HudName => toolName;
        public Color HudColor => kind == ToolKind.Pen ? fullInkColor : kind == ToolKind.Eraser ? new Color(0.93f, 0.62f, 0.66f) : new Color(0.92f, 0.87f, 0.74f);
        public string HudHints => kind == ToolKind.Pen ? "Clic: dibujar | Clic der.: deshacer | Rueda: grosor | R: tinta | Q: arrojar"
            : kind == ToolKind.Eraser ? "Clic: borrar | Clic der.: deshacer | Rueda: tamaño | Q: arrojar" : "Clic: limpiar el lienzo | Q: arrojar";
        public float HudGauge => kind == ToolKind.Pen ? ink.Fraction : -1f;
        public int WidthIndex => widthIndex;
        public int WidthCount => kind == ToolKind.Rag ? 0 : widthPresets.Length;
        public float WidthSample(int index) => widthPresets[Mathf.Clamp(index, 0, widthPresets.Length - 1)] / widthPresets[widthPresets.Length - 1];
        public string WidthLabel(int index) => index >= 0 && index < widthNames.Length ? widthNames[index] : (index + 1).ToString();
        public InkReservoir Ink => ink;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
            bodyRenderer = inkIndicator != null ? inkIndicator : GetComponentInChildren<Renderer>();
            block = new MaterialPropertyBlock();
            if (raycastOrigin == null) raycastOrigin = transform;
            ApplyDefinition();
            baseStrokeWidth = strokeWidth;

            float stabilizer = PlatformDetector.IsVR ? vrStabilizerSpeed : pcStabilizerSpeed;
            switch (kind)
            {
                case ToolKind.Pen: tool = new PenTool(stabilizer, strokeWidth); break;
                case ToolKind.Eraser: tool = new EraserTool(definition != null ? definition.eraseRadius : 0.025f); break;
                default: tool = new RagTool(definition != null ? definition.wipeLengthToClear : 1.2f); break;
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
                    Refilled?.Invoke(this);
                    Haptics.Pulse(HandType.Right, 0.3f, 0.08f);
                }
                break;
            }
        }

        /// <summary>Rueda del mouse: grosor de la pluma o el radio de la goma (no cambia mientras se dibuja).</summary>
        public void CycleWidth(int delta)
        {
            if (kind == ToolKind.Rag || currentCanvas != null) return;
            int count = widthPresets.Length;
            int next = Mathf.Clamp(widthIndex + delta, 0, count - 1);
            if (next == widthIndex) return;
            widthIndex = next;
            float stabilizer = PlatformDetector.IsVR ? vrStabilizerSpeed : pcStabilizerSpeed;
            if (kind == ToolKind.Pen) tool = new PenTool(stabilizer, baseStrokeWidth * widthPresets[widthIndex]);
            else tool = new EraserTool((definition != null ? definition.eraseRadius : 0.025f) * widthPresets[widthIndex]);
            Hud.Notify("width", (kind == ToolKind.Pen ? "Grosor: " : "Tamaño de la goma: ") + WidthLabel(widthIndex), 2f);
        }

        private void ApplyDefinition()
        {
            if (definition == null) return;
            toolName = definition.displayName;
            kind = definition.kind;
            strokeWidth = definition.strokeWidth;
            pcStabilizerSpeed = definition.pcStabilizerSpeed;
            vrStabilizerSpeed = definition.vrStabilizerSpeed;
            ink = new InkReservoir(definition.inkCapacity, definition.inkCapacity);
            inkPerMeter = definition.inkPerMeter;
            fullInkColor = definition.fullInkColor;
            emptyInkColor = definition.emptyInkColor;
        }

        // --- IInteractable ---

        private string prompt;

        // Se arma una vez: el interactor lo pide en cada frame (sin GC).
        public string GetInteractionPrompt() => prompt ??= $"Agarrar {toolName}";

        public void OnInteract(Transform holdPoint)
        {
            held = true;
            PickedUp?.Invoke(this);
            rb.isKinematic = true;
            col.enabled = false;
            transform.SetParent(holdPoint);
            transform.localPosition = Vector3.zero;
            transform.localRotation = PlatformDetector.IsVR ? Quaternion.identity : Quaternion.Euler(PcHoldPitch, 0f, 0f);
        }

        public void OnDrop()
        {
            OnUseEnd();
            held = false;
            transform.SetParent(null);
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            col.enabled = true;
        }

        // --- IUsable ---

        public void OnUseStart()
        {
            if (!TryShootLaser(out Vector2 uv, out DrawingCanvas canvas, allowInscribe: kind == ToolKind.Pen)) return;
            if (UsesInk && ink.IsEmpty)
            {
                ReportNoInk();
                return;
            }

            currentCanvas = canvas;
            lastUv = uv;
            if (kind == ToolKind.Pen && lastRayOrigin != Vector3.zero) canvas.NoteInkViewer(lastRayOrigin);
            tool.OnPointerDown(uv, canvas);
            UseChanged?.Invoke(this, true);
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
            UseChanged?.Invoke(this, false);

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
            if (amount > 0f) { Refilled?.Invoke(this); if (ink.Fraction > 0.05f) Hud.Clear("ink"); }
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
            Hud.Notify("ink", $"Sin tinta ({toolName}): recarga con R junto a un tintero", 0f); // permanente hasta recargar
            Haptics.Pulse(HandType.Right, 0.5f, 0.15f);
        }

        private void RefreshInkVisual()
        {
            if (!UsesInk || bodyRenderer == null) return;
            bodyRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, Color.Lerp(emptyInkColor, fullInkColor, ink.Fraction));
            bodyRenderer.SetPropertyBlock(block);
        }

        private bool TryShootLaser(out Vector2 uv, out DrawingCanvas canvas, bool allowInscribe = false)
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
                ray = FocusMode.GetRay(cachedCamera);
                distance = pcMaxDrawDistance;
            }

            lastRayOrigin = ray.origin;
            // Los lienzos sostenidos son triggers: hay que pedir que el rayo los detecte.
            if (Physics.Raycast(ray, out RaycastHit hit, distance, canvasLayer, QueryTriggerInteraction.Collide) &&
                hit.collider.TryGetComponent(out canvas))
            {
                uv = canvas.GetUVFromRay(ray, hit.point);
                return true;
            }
            canvas = null;

            // Sin lienzo a la vista: si se mira una superficie del entorno (piso, caja, pared), al empezar un trazo la
            // lapicera deja ahi una calcomania de tinta.
            if (!allowInscribe) return false;
            // Un boton (del grimorio) delante no deja dibujar en lo que tiene detras.
            if (Physics.Raycast(ray, out RaycastHit front, distance, ~0, QueryTriggerInteraction.Collide) &&
                front.collider.GetComponentInParent<IPressable>() != null) return false;
            if (!Physics.Raycast(ray, out hit, distance, ~0, QueryTriggerInteraction.Ignore)) return false;
            var surface = hit.collider.GetComponentInParent<Inscribable>();
            if (surface == null) return false;
            canvas = surface.CreateDecal(hit, ray.direction);
            if (canvas == null) return false;
            uv = canvas.GetUVFromRay(ray, hit.point);
            return true;
        }
    }
}
