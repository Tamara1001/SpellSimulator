using UnityEngine;
using SpellSimulator.Interfaces;

namespace SpellSimulator.World
{
    /// <summary>
    /// Objeto del mundo que se puede mover para hacer experimentos (cajas, muñecos chicos, pesas, troncos). Se agarra con
    /// E y se lleva con un resorte hacia la mano: choca con el mundo (no lo atraviesa) y no empuja al jugador. La rueda
    /// del mouse lo acerca o aleja y Q lo arroja. Los objetos muy pesados no se levantan.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Movable : MonoBehaviour, IInteractable, IThrowable
    {
        [SerializeField] private string objectName = "objeto";
        [SerializeField, Tooltip("Masa maxima (kg) que se puede levantar.")]
        private float maxMass = 40f;
        [SerializeField, Tooltip("Fuerza del resorte hacia la mano (1/s). Mas alto = sigue mas pegado.")]
        private float follow = 14f;
        [SerializeField, Tooltip("Velocidad maxima de arrastre (m/s).")]
        private float maxCarrySpeed = 14f;

        private Rigidbody rb;
        private Collider[] colliders;
        private Transform target;
        private bool wasGravity;
        private float wasDrag, wasAngularDrag;
        private string prompt;

        public HandType PreferredHand => HandType.Both;
        public bool CanLift => rb != null && rb.mass <= maxMass;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>();
        }

        public void SetName(string newName)
        {
            objectName = newName;
            prompt = null;
        }

        // Se arma una vez: el interactor lo pide en cada frame (sin GC).
        public string GetInteractionPrompt() => prompt ??= rb.mass <= maxMass ? $"Mover {objectName}" : $"{objectName} (muy pesado)";

        public void OnInteract(Transform holdPoint)
        {
            if (!CanLift) return;
            target = holdPoint;
            wasGravity = rb.useGravity;
            wasDrag = rb.linearDamping;
            wasAngularDrag = rb.angularDamping;
            rb.useGravity = false;
            rb.angularDamping = 4f;
            IgnorePlayer(true);
        }

        public void OnDrop()
        {
            if (target == null) return;
            target = null;
            rb.useGravity = wasGravity;
            rb.linearDamping = wasDrag;
            rb.angularDamping = wasAngularDrag;
            IgnorePlayer(false);
        }

        public void Throw(Vector3 velocity) => rb.linearVelocity = velocity;

        private void FixedUpdate()
        {
            if (target == null) return;
            // Resorte hacia la mano; el giro se frena para que no gire como una hélice.
            Vector3 toTarget = target.position - rb.position;
            rb.linearVelocity = Vector3.ClampMagnitude(toTarget * follow, maxCarrySpeed);
            rb.angularVelocity *= 0.85f;
        }

        // El cuerpo del jugador no choca con lo que lleva (si no, se empujaria a si mismo).
        private void IgnorePlayer(bool ignore)
        {
            var player = FindFirstObjectByType<Player.PlayerMovement>();
            if (player == null) return;
            foreach (var theirs in player.GetComponentsInChildren<Collider>())
                foreach (var mine in colliders)
                    if (mine != null && theirs != null) Physics.IgnoreCollision(mine, theirs, ignore);
        }
    }
}
