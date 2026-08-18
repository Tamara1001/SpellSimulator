using UnityEngine;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Player
{
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField, Tooltip("Punto de agarre para la mano derecha (Herramientas)")] 
        private Transform rightHoldPoint; 
        [SerializeField, Tooltip("Punto de agarre para la mano izquierda (Lienzos/Escudos)")] 
        private Transform leftHoldPoint;
        
        [Header("VR References")]
        [SerializeField, Tooltip("[VR] Transform del controlador derecho. De acá sale el rayo y se agarran herramientas.")]
        private Transform vrRightController;
        [SerializeField, Tooltip("[VR] Transform del controlador izquierdo. Acá se agarran lienzos.")]
        private Transform vrLeftController;

        [Header("Settings")]
        [SerializeField] private float interactRange = 3f;
        [SerializeField] private LayerMask interactLayer = Physics.DefaultRaycastLayers;

        private PlayerInputReader inputReader;
        private IInteractable currentHovered;
        
        // Manos
        private GameObject heldRightObject;
        private GameObject heldLeftObject;
        
        // La herramienta usable (Lapiz/Goma) que este en cualquier mano, usualmente la derecha
        private IUsable currentUsable;
        private bool wasUsingTool;
        
        // Puntero visual para VR
        private LineRenderer vrPointerLine;

        private void Awake()
        {
            inputReader = GetComponent<PlayerInputReader>();
        }

        private void Start()
        {
            if (PlatformDetector.IsVR)
            {
                // Reasignar los puntos de agarre a las manos reales de VR
                if (vrRightController != null) rightHoldPoint = vrRightController;
                if (vrLeftController != null) leftHoldPoint = vrLeftController;
                
                // Crear el rayo laser visual para VR automáticamente si no existe
                if (vrRightController != null)
                {
                    vrPointerLine = vrRightController.gameObject.AddComponent<LineRenderer>();
                    vrPointerLine.startWidth = 0.005f;
                    vrPointerLine.endWidth = 0.005f;
                    vrPointerLine.material = new Material(Shader.Find("Sprites/Default"));
                    vrPointerLine.startColor = Color.white;
                    vrPointerLine.endColor = new Color(1f, 1f, 1f, 0f); // Fade out
                    vrPointerLine.useWorldSpace = true;
                    
                    vrPointerLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    vrPointerLine.receiveShadows = false;
                }
            }
        }

        private void Update()
        {
            HandleHover();
            HandleInteraction();
            HandleUsage();
        }

        private void HandleHover()
        {
            // Si miramos a algo, vemos si lo podemos agarrar.
            // En VR el rayo sale del controlador derecho; en PC, de la camara (mira).
            Transform rayOrigin = (PlatformDetector.IsVR && vrRightController != null)
                ? vrRightController
                : cameraTransform;

            Vector3 rayDirection = rayOrigin.forward;
            if (PlatformDetector.IsVR && rayOrigin == vrRightController)
            {
                // El usuario solicitó rotarlo 90 grados por código sin tocar el Transform original
                rayDirection = Quaternion.AngleAxis(90f, rayOrigin.right) * rayOrigin.forward;
            }

            Ray ray = new Ray(rayOrigin.position, rayDirection);
            
            // Dibujar linea si estamos en VR
            if (vrPointerLine != null)
            {
                vrPointerLine.SetPosition(0, rayOrigin.position);
                vrPointerLine.SetPosition(1, rayOrigin.position + rayDirection * interactRange);
            }

            if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactLayer))
            {
                // Cortar la linea visual en el punto de impacto
                if (vrPointerLine != null) vrPointerLine.SetPosition(1, hit.point);
                
                IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null)
                {
                    // Solo podemos hacer hover si tenemos la mano correspondiente libre
                    bool canHold = (interactable.PreferredHand == HandType.Right && heldRightObject == null) ||
                                   (interactable.PreferredHand == HandType.Left && heldLeftObject == null) ||
                                   interactable.PreferredHand == HandType.Both;
                    
                    if (canHold)
                    {
                        currentHovered = interactable;
                        return;
                    }
                }
            }
            
            currentHovered = null;
        }

        private void HandleInteraction()
        {
            if (inputReader.IsInteracting)
            {
                // Prioridad: Si miramos algo agarrable y tenemos mano libre, lo agarramos.
                if (currentHovered != null)
                {
                    GrabObject(currentHovered);
                }
                // Si miramos al vacio, tiramos las cosas. (Primero la derecha, si esta vacia, tiramos la izquierda)
                else
                {
                    if (heldRightObject != null)
                        DropObject(heldRightObject, HandType.Right);
                    else if (heldLeftObject != null)
                        DropObject(heldLeftObject, HandType.Left);
                }
            }
        }

        private void HandleUsage()
        {
            if (currentUsable == null)
            {
                if (inputReader.IsUsingTool && !wasUsingTool)
                {
                    Debug.LogWarning("[PlayerInteractor] Intentaste usar herramienta pero currentUsable es NULL. ¿Agarraste el lápiz?");
                    wasUsingTool = true;
                }
                else if (!inputReader.IsUsingTool && wasUsingTool)
                {
                    wasUsingTool = false;
                }
                return;
            }

            if (inputReader.IsUsingTool && !wasUsingTool)
            {
                Debug.Log("[PlayerInteractor] Iniciando trazo (OnUseStart) con: " + currentUsable.GetType().Name);
                currentUsable.OnUseStart();
                wasUsingTool = true;
            }
            else if (inputReader.IsUsingTool && wasUsingTool)
            {
                currentUsable.OnUseHold();
            }
            else if (!inputReader.IsUsingTool && wasUsingTool)
            {
                Debug.Log("[PlayerInteractor] Finalizando trazo (OnUseEnd)");
                currentUsable.OnUseEnd();
                wasUsingTool = false;
            }

            // Uso Secundario (Cast/Undo)
            if (inputReader.IsSecondaryUsingTool)
            {
                currentUsable.OnSecondaryUseStart();
            }
        }

        private void GrabObject(IInteractable interactable)
        {
            MonoBehaviour mb = interactable as MonoBehaviour;
            if (mb != null)
            {
                Transform targetHoldPoint = (interactable.PreferredHand == HandType.Left) ? leftHoldPoint : rightHoldPoint;
                
                if (interactable.PreferredHand == HandType.Left)
                {
                    heldLeftObject = mb.gameObject;
                }
                else
                {
                    heldRightObject = mb.gameObject;
                }

                // Si es un IUsable (como el Lapiz), nos lo guardamos
                IUsable usable = mb.GetComponent<IUsable>();
                if (usable != null) currentUsable = usable;
                
                interactable.OnInteract(targetHoldPoint);
                currentHovered = null;
            }
        }

        private void DropObject(GameObject obj, HandType hand)
        {
            IInteractable interactable = obj.GetComponent<IInteractable>();
            interactable?.OnDrop();
            
            if (hand == HandType.Left)
                heldLeftObject = null;
            else
                heldRightObject = null;
            
            IUsable usable = obj.GetComponent<IUsable>();
            if (usable != null && currentUsable == usable)
            {
                currentUsable = null;
                wasUsingTool = false;
            }
        }
    }
}
