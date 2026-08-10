using UnityEngine;
using SpellSimulator.Interfaces;

namespace SpellSimulator.World
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class PhysicalCanvas : MonoBehaviour, IInteractable
    {
        [Header("Settings")]
        [SerializeField] private string objectName = "Lienzo Mágico";
        [SerializeField, Tooltip("Corrección de rotación al sostenerlo en la mano izquierda")]
        private Vector3 holdRotationOffset = Vector3.zero;

        private Rigidbody rb;
        private Collider col;
        private Transform holdParent;

        public HandType PreferredHand => HandType.Left;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
        }

        public string GetInteractionPrompt()
        {
            return $"Agarrar {objectName}";
        }

        public void OnInteract(Transform holdPoint)
        {
            holdParent = holdPoint;
            
            rb.isKinematic = true;
            
            // Para que no "empuje" al jugador al estar pegado a su cuerpo,
            // lo volvemos un Trigger. El Raycast para dibujar SÍ detecta Triggers, 
            // así que podrás seguir dibujando sin que te tire del mapa.
            col.isTrigger = true; 
            
            transform.SetParent(holdPoint);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.Euler(holdRotationOffset);
        }

        public void OnDrop()
        {
            holdParent = null;
            transform.SetParent(null);
            
            rb.isKinematic = false;
            col.isTrigger = false;
        }
    }
}
