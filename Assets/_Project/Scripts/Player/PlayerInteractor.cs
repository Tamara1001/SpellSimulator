using UnityEngine;
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
        [SerializeField] private LayerMask interactLayer = Physics.DefaultRaycastLayers;

        private PlayerInputReader inputReader;
        private IInteractable currentHovered;
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
        }

        private void Start()
        {
            if (!PlatformDetector.IsVR) return;

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
            HandleHover();
            HandleInteraction();
            HandleUsage();
        }

        private void HandleHover()
        {
            bool vrRay = PlatformDetector.IsVR && vrRightController != null;
            Transform origin = vrRay ? vrRightController : cameraTransform;
            if (origin == null) return;

            Vector3 direction = vrRay ? Quaternion.AngleAxis(vrRayPitchOffset, origin.right) * origin.forward : origin.forward;
            Vector3 end = origin.position + direction * interactRange;

            currentHovered = null;
            if (Physics.Raycast(origin.position, direction, out RaycastHit hit, interactRange, interactLayer))
            {
                end = hit.point;
                var interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null && HasFreeHandFor(interactable)) currentHovered = interactable;
            }

            if (vrPointerLine != null)
            {
                vrPointerLine.SetPosition(0, origin.position);
                vrPointerLine.SetPosition(1, end);
            }
        }

        private bool HasFreeHandFor(IInteractable interactable)
        {
            switch (interactable.PreferredHand)
            {
                case HandType.Right: return heldRight == null;
                case HandType.Left: return heldLeft == null;
                default: return heldRight == null || heldLeft == null;
            }
        }

        private void HandleInteraction()
        {
            if (!inputReader.IsInteracting) return;

            if (currentHovered != null) Grab(currentHovered);
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

        private void Grab(IInteractable interactable)
        {
            if (!(interactable is MonoBehaviour mb)) return;

            // "Both" va a la mano que este libre, prefiriendo la derecha.
            bool left = interactable.PreferredHand == HandType.Left ||
                        (interactable.PreferredHand == HandType.Both && heldRight != null);
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
