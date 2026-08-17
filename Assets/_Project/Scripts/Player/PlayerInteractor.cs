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
        [SerializeField, Tooltip("[VR] Transform del controlador derecho. Desde acá sale el rayo de interacción en VR.")]
        private Transform vrRaycastOrigin;

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

        private void Awake()
        {
            inputReader = GetComponent<PlayerInputReader>();
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
            Transform rayOrigin = (PlatformDetector.IsVR && vrRaycastOrigin != null)
                ? vrRaycastOrigin
                : cameraTransform;

            Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactLayer))
            {
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
            if (currentUsable == null) return;

            bool isUsing = inputReader.IsUsingTool;

            if (isUsing && !wasUsingTool)
            {
                currentUsable.OnUseStart();
            }
            else if (isUsing && wasUsingTool)
            {
                currentUsable.OnUseHold();
            }
            else if (!isUsing && wasUsingTool)
            {
                currentUsable.OnUseEnd();
            }

            wasUsingTool = isUsing;

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
