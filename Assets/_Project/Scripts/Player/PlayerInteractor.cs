using UnityEngine;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Player
{
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField, Tooltip("El punto vacio en la mano donde se pegaran los objetos")] 
        private Transform holdPoint; 

        [Header("Settings")]
        [SerializeField] private float interactRange = 3f;
        [SerializeField] private LayerMask interactLayer = Physics.DefaultRaycastLayers;

        private PlayerInputReader inputReader;
        private IInteractable currentHovered;
        private GameObject heldObject;
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
            if (heldObject != null)
            {
                // Si tenemos algo agarrado, dejamos de buscar cosas en el piso
                currentHovered = null;
                return;
            }

            // Raycast desde el centro de la pantalla
            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactLayer))
            {
                IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null)
                {
                    currentHovered = interactable;
                    // En el futuro, aqui mostrariamos el UI "Press Click to Grab Pen"
                    return;
                }
            }
            
            currentHovered = null;
        }

        private void HandleInteraction()
        {
            if (inputReader.IsInteracting)
            {
                if (heldObject != null)
                {
                    DropCurrentObject();
                }
                else if (currentHovered != null)
                {
                    GrabObject(currentHovered);
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
                heldObject = mb.gameObject;
                currentUsable = heldObject.GetComponent<IUsable>();
                
                // Le avisamos al objeto fisico que fue agarrado y a donde debe ir
                interactable.OnInteract(holdPoint);
                currentHovered = null;
            }
        }

        private void DropCurrentObject()
        {
            if (heldObject != null)
            {
                IInteractable interactable = heldObject.GetComponent<IInteractable>();
                interactable?.OnDrop();
                
                heldObject = null;
                currentUsable = null;
                wasUsingTool = false;
            }
        }
    }
}
