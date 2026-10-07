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

        [Header("Papel suelto")]
        [SerializeField, Tooltip("Aceleracion angular (rad/s2) que acuesta la hoja cuando esta suelta: un papel no queda de canto.")]
        private float settleTorque = 40f;
        [SerializeField, Tooltip("Frenado del giro al acostarse (1/s).")]
        private float settleDamping = 6f;
        [SerializeField, Tooltip("Frenado del aire mientras cae suelta (planea un poco).")]
        private float airDrag = 1.2f;

        private Rigidbody rb;
        private Collider col;
        private Transform holdParent;

        public HandType PreferredHand => HandType.Left;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
        }

        /// <summary>
        /// Una hoja suelta se acuesta sola: gira hasta quedar con la cara hacia arriba o hacia abajo (la mas cercana).
        /// Un papel real se dobla y cae plano; la caja fina del collider, en cambio, quedaba parada de canto.
        /// </summary>
        private void FixedUpdate()
        {
            if (holdParent != null || rb.isKinematic) return;
            Vector3 normal = transform.forward;
            float facing = Vector3.Dot(normal, Vector3.up);
            Vector3 target = facing >= 0f ? Vector3.up : Vector3.down;
            Vector3 axis = Vector3.Cross(normal, target);
            if (axis.sqrMagnitude < 1e-5f) return;
            rb.AddTorque(axis * settleTorque - rb.angularVelocity * settleDamping * (1f - Mathf.Abs(facing)), ForceMode.Acceleration);
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
            // Una hoja soltada cae (antes quedaba suspendida en el aire, de pie) y se acuesta en FixedUpdate.
            rb.useGravity = true;
            rb.linearDamping = airDrag;
        }
    }
}
