using UnityEngine;
using SpellSimulator.DebugTools;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Player
{
    /// <summary>
    /// Mirar / agarrar / soltar / usar objetos. Mano derecha = herramientas, izquierda = lienzos.
    /// En PC el rayo sale de la camara; en VR del controlador derecho.
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField, Tooltip("Punto de agarre de la mano derecha (herramientas).")]
        private Transform rightHoldPoint;
        [SerializeField, Tooltip("Punto de agarre de la mano izquierda (lienzos).")]
        private Transform leftHoldPoint;

        [Header("VR References")]
        [SerializeField, Tooltip("[VR] Controlador derecho: origen del rayo y punto de agarre.")]
        private Transform vrRightController;
        [SerializeField, Tooltip("[VR] Controlador izquierdo: punto de agarre de lienzos.")]
        private Transform vrLeftController;
        [SerializeField, Tooltip("[VR] Rotacion (grados, sobre el eje X del controlador) para alinear el rayo con la mano.")]
        private float vrRayPitchOffset = 90f;
        [SerializeField, Tooltip("[VR] Material del puntero laser. Si falta se usa Sprites/Default.")]
        private Material vrPointerMaterial;

        [Header("Settings")]
        [SerializeField] private float interactRange = 3f;
        [SerializeField, Tooltip("[PC] Distancia minima y maxima (m) a la que se sostiene lo agarrado (rueda del mouse).")]
        private Vector2 holdDistanceRange = new Vector2(0.45f, 1.8f);
        [SerializeField, Tooltip("Velocidad (m/s) con que sale lo que se arroja (Q).")]
        private float throwSpeed = 7f;
        [SerializeField] private LayerMask interactLayer = Physics.DefaultRaycastLayers;

        private PlayerInputReader inputReader;
        private InkSupply inkSupply;
        private static readonly RaycastHit[] hoverHits = new RaycastHit[24];

        private IInteractable currentHovered;
        private IPressable hoveredPressable;
        private IFlippable hoveredFlippable;
        private Rigidbody body;
        private float holdDistance = 1f;

        /// <summary>Texto de ayuda de lo que se esta mirando ("Agarrar Lapiz", "Pagina siguiente"...). Vacio si nada.</summary>
        public string HoverPrompt { get; private set; } = "";
        private IInkSource hoveredInkSource;
        private GameObject heldRight;
        private GameObject heldLeft;
        private IUsable rightUsable;
        private IUsable leftUsable;
        private bool wasUsingTool;
        private LineRenderer vrPointerLine;

        /// <summary>El usable que responde al gatillo/click: la mano derecha manda.</summary>
        private IUsable ActiveUsable => rightUsable ?? leftUsable;

        private void Awake()
        {
            inputReader = GetComponent<PlayerInputReader>();
            inkSupply = GetComponent<InkSupply>();
            body = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            if (!PlatformDetector.IsVR)
            {
                if (leftHoldPoint != null) holdDistance = leftHoldPoint.localPosition.z;
                return;
            }

            if (vrRightController != null) rightHoldPoint = vrRightController;
            if (vrLeftController != null) leftHoldPoint = vrLeftController;
            if (vrRightController != null) vrPointerLine = CreatePointer(vrRightController.gameObject);
        }

        private LineRenderer CreatePointer(GameObject host)
        {
            var line = host.AddComponent<LineRenderer>();
            line.startWidth = 0.005f;
            line.endWidth = 0.005f;
            line.material = vrPointerMaterial != null ? vrPointerMaterial : new Material(Shader.Find("Sprites/Default"));
            line.startColor = Color.white;
            line.endColor = new Color(1f, 1f, 1f, 0f);
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void Update()
        {
            // Lo que se tenia en la mano y ya no esta (libro cerrado, objeto destruido) se suelta solo.
            if (heldLeft != null && !heldLeft.activeInHierarchy) Drop(HandType.Left);
            if (heldRight != null && !heldRight.activeInHierarchy) Drop(HandType.Right);
            HandleHover();
            HandleInteraction();
            HandlePin();
            HandleThrow();
            HandleFlip();
            HandleHoldDistance();
            HandleUsage();
            if (inputReader.IsReloading) Reload();
            if (inputReader.IsTogglingFocus) ToggleFocus();
        }

        private void HandleHover()
        {
            bool vrRay = PlatformDetector.IsVR && vrRightController != null;
            Transform origin = vrRay ? vrRightController : cameraTransform;
            if (origin == null) return;

            Vector3 direction = vrRay ? Quaternion.AngleAxis(vrRayPitchOffset, origin.right) * origin.forward : origin.forward;
            Vector3 end = origin.position + direction * interactRange;

            currentHovered = null;
            hoveredPressable = null;
            hoveredInkSource = null;
            hoveredFlippable = null;
            if (PickHit(origin.position, direction, out RaycastHit hit))
            {
                end = hit.point;
                hoveredInkSource = hit.collider.GetComponentInParent<IInkSource>();
                hoveredPressable = hit.collider.GetComponentInParent<IPressable>();
                hoveredFlippable = hit.collider.GetComponentInParent<IFlippable>();
                var interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null) currentHovered = interactable;
            }
            HoverPrompt = hoveredPressable != null ? hoveredPressable.GetPressPrompt()
                : currentHovered != null ? currentHovered.GetInteractionPrompt() : "";

            if (vrPointerLine != null)
            {
                vrPointerLine.SetPosition(0, origin.position);
                vrPointerLine.SetPosition(1, end);
            }
        }

        /// <summary>
        /// Primer golpe util del rayo: ignora el propio cuerpo, lo que se lleva en la mano (salvo botones, como las
        /// paginas de un libro en mano) y los volumenes disparadores que no se pueden usar (agua, campos magicos).
        /// </summary>
        private bool PickHit(Vector3 origin, Vector3 direction, out RaycastHit best)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, hoverHits, interactRange, interactLayer, QueryTriggerInteraction.Collide);
            best = default;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hoverHits[i];
                if (hit.distance >= bestDistance) continue;
                Collider c = hit.collider;
                if (c.transform.IsChildOf(transform)) continue;
                bool pressable = c.GetComponentInParent<IPressable>() != null;
                bool held = IsHeld(c.transform);
                if (held && !pressable) continue;
                if (c.isTrigger && !pressable && c.GetComponentInParent<IInteractable>() == null &&
                    c.GetComponentInParent<IInkSource>() == null && c.GetComponentInParent<IFlippable>() == null) continue;
                best = hit;
                bestDistance = hit.distance;
            }
            return bestDistance < float.MaxValue;
        }

        private bool IsHeld(Transform t)
        {
            return (heldRight != null && t.IsChildOf(heldRight.transform)) || (heldLeft != null && t.IsChildOf(heldLeft.transform));
        }

        private void HandleThrow()
        {
            if (!inputReader.IsThrowing) return;
            HandType hand = heldRight != null ? HandType.Right : HandType.Left;
            GameObject obj = hand == HandType.Right ? heldRight : heldLeft;
            if (obj == null) return;
            Drop(hand);
            Transform view = PlatformDetector.IsVR && vrRightController != null ? vrRightController : cameraTransform;
            Vector3 velocity = view.forward * throwSpeed + Vector3.up * 1.2f + (body != null ? body.linearVelocity : Vector3.zero);
            if (obj.TryGetComponent(out IThrowable throwable)) throwable.Throw(velocity);
            else if (obj.TryGetComponent(out Rigidbody rb) && !rb.isKinematic) rb.linearVelocity = velocity;
        }

        private void HandleFlip()
        {
            if (!inputReader.IsFlipping) return;
            IFlippable target = null;
            if (heldLeft != null) heldLeft.TryGetComponent(out target);
            if (target == null && heldRight != null) heldRight.TryGetComponent(out target);
            (target ?? hoveredFlippable)?.Flip();
        }

        /// <summary>[PC] La rueda acerca o aleja lo que se sostiene (no si el grimorio esta abierto: ahi pasa paginas).</summary>
        private void HandleHoldDistance()
        {
            if (PlatformDetector.IsVR || inputReader.Scroll == 0f || (heldLeft == null && heldRight == null)) return;
            var book = Learning.Grimoire.Instance;
            if (book != null && book.IsOpen) return;
            holdDistance = Mathf.Clamp(holdDistance + Mathf.Sign(inputReader.Scroll) * 0.1f, holdDistanceRange.x, holdDistanceRange.y);
            ApplyHoldDistance(leftHoldPoint);
            ApplyHoldDistance(rightHoldPoint);
        }

        private void ApplyHoldDistance(Transform point)
        {
            if (point == null) return;
            var p = point.localPosition;
            p.z = holdDistance;
            point.localPosition = p;
        }

        /// <summary>G: fija lo que se sostiene. Prioridad: mano izquierda (lienzos) y luego derecha.</summary>
        private void HandlePin()
        {
            if (!inputReader.IsPinning) return;
            if (!TryPin(HandType.Left)) TryPin(HandType.Right);
        }

        private bool TryPin(HandType hand)
        {
            GameObject obj = hand == HandType.Left ? heldLeft : heldRight;
            if (obj == null || !obj.TryGetComponent(out IPinnable pinnable)) return false;
            Drop(hand);
            pinnable.Pin();
            return true;
        }

        private void HandleInteraction()
        {
            if (!inputReader.IsInteracting) return;

            if (hoveredPressable != null) hoveredPressable.Press();
            else if (currentHovered != null) Grab(currentHovered);
            else if (heldRight != null) Drop(HandType.Right);
            else if (heldLeft != null) Drop(HandType.Left);
        }

        private void HandleUsage()
        {
            IUsable usable = ActiveUsable;
            bool using_ = inputReader.IsUsingTool;

            if (usable != null)
            {
                if (using_ && !wasUsingTool) usable.OnUseStart();
                else if (using_) usable.OnUseHold();
                else if (wasUsingTool) usable.OnUseEnd();

                if (inputReader.IsSecondaryUsingTool) usable.OnSecondaryUseStart();
            }
            wasUsingTool = using_;
        }

        /// <summary>
        /// Recargar tinta de la herramienta activa. Fuente: el tintero que se mira, uno en la mano, o la reserva personal.
        /// </summary>
        private void Reload()
        {
            if (!(ActiveUsable is IInkUser user) || !user.UsesInk || user.Ink.Missing <= 0f) return;

            IInkSource source = hoveredInkSource ?? HeldInkSource(heldLeft) ?? HeldInkSource(heldRight) ?? inkSupply;
            if (source == null) return;

            float amount = user.RefillFrom(source);
            // Si se recargo de un tintero, tambien se repone la reserva personal.
            if (inkSupply != null && !ReferenceEquals(source, inkSupply)) amount += inkSupply.Stock(source);
            if (amount > 0f) Haptics.Pulse(HandType.Right, 0.3f, 0.08f);
            DiagnosticBus.OnDiagnosticsUpdated?.Invoke(
                $"<b>[Tinta]</b> Lapiz {user.Ink.Fraction:P0}" + (inkSupply != null ? $" | reserva {inkSupply.Reserve.Level:F0}" : ""));
        }

        /// <summary>V: fija el lienzo de la mano delante de la camara para dibujar sin sostener la mira.</summary>
        private void ToggleFocus()
        {
            // Con el grimorio abierto, V lo fija a el (lo maneja el propio libro).
            var book = Learning.Grimoire.Instance;
            if (book != null && book.IsOpen) return;
            if (PlatformDetector.IsVR || heldLeft == null || !heldLeft.TryGetComponent(out IFocusable focusable)) return;
            focusable.SetFocus(cameraTransform, !focusable.Focused);
        }

        private static IInkSource HeldInkSource(GameObject held)
        {
            return held != null && held.TryGetComponent(out IInkSource source) ? source : null;
        }

        private void Grab(IInteractable interactable)
        {
            if (!(interactable is MonoBehaviour mb)) return;

            // "Both" va a la izquierda solo si la derecha esta ocupada y la izquierda libre; si ambas estan ocupadas, la derecha.
            bool left = interactable.PreferredHand == HandType.Left ||
                        (interactable.PreferredHand == HandType.Both && heldRight != null && heldLeft == null);

            // Intercambio: si la mano destino esta ocupada, lo que tiene se suelta primero.
            if (left ? heldLeft != null : heldRight != null) Drop(left ? HandType.Left : HandType.Right);

            mb.TryGetComponent(out IUsable usable);

            if (left)
            {
                heldLeft = mb.gameObject;
                leftUsable = usable;
            }
            else
            {
                heldRight = mb.gameObject;
                rightUsable = usable;
            }

            interactable.OnInteract(left ? leftHoldPoint : rightHoldPoint);
            currentHovered = null;
        }

        private void Drop(HandType hand)
        {
            GameObject obj = hand == HandType.Left ? heldLeft : heldRight;
            IUsable usable = hand == HandType.Left ? leftUsable : rightUsable;
            if (usable != null && usable == ActiveUsable && wasUsingTool) usable.OnUseEnd();

            if (obj != null && obj.TryGetComponent(out IInteractable interactable)) interactable.OnDrop();

            if (hand == HandType.Left)
            {
                heldLeft = null;
                leftUsable = null;
            }
            else
            {
                heldRight = null;
                rightUsable = null;
            }
            wasUsingTool = false;
        }
    }
}
