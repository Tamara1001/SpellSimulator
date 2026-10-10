using UnityEngine;

namespace SpellSimulator.World
{
    /// <summary>Mantiene un cuerpo con la cara de arriba hacia arriba (el barquito del tutorial de agua no se da vuelta): par restaurador amortiguado.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class KeepUpright : MonoBehaviour
    {
        [SerializeField, Tooltip("Fuerza del par que lo endereza.")] private float strength = 14f;
        [SerializeField, Tooltip("Amortiguacion del giro.")] private float damping = 4f;

        private Rigidbody rb;

        private void Awake() => rb = GetComponent<Rigidbody>();

        private void FixedUpdate()
        {
            if (rb.isKinematic) return;
            Vector3 axis = Vector3.Cross(transform.up, Vector3.up);
            rb.AddTorque(axis * strength - rb.angularVelocity * damping, ForceMode.Acceleration);
        }
    }
}
