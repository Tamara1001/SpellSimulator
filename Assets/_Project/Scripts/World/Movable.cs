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
        private float maxMass = 600f;
        [SerializeField, Tooltip("Fuerza del resorte hacia la mano (1/s). Mas alto = sigue mas pegado.")]
        private float follow = 14f;
        [SerializeField, Tooltip("Velocidad maxima de arrastre (m/s).")]
        private float maxCarrySpeed = 14f;

        private Rigidbody rb;
        private Collider[] colliders;
        private Transform target;
        private bool wasGravity;
        private float wasDrag, wasAngularDrag;
        private Renderer[] renderers;
        // Carga: distancia extra hacia adelante de la mano y resorte/velocidad ajustados por masa.
        private float carryOffsetForward, carryFollow, carrySpeed;
        private string prompt;

        public HandType PreferredHand => HandType.Both;
        public bool CanLift => rb != null && rb.mass <= maxMass;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>();
            renderers = GetComponentsInChildren<Renderer>();
        }

        public void SetName(string newName)
        {
            objectName = newName;
            prompt = null;
        }

        // Se arma una vez: el interactor lo pide en cada frame (sin GC).
        public string GetInteractionPrompt() => prompt ??= rb.mass <= maxMass ? $"Mover {objectName}" : $"{objectName} (demasiado pesado)";

        public void OnInteract(Transform holdPoint)
        {
            if (!CanLift) return;
            target = holdPoint;
            // Los grandes se sostienen mas lejos (su centro queda delante de la mano) y responden mas blando.
            carryOffsetForward = Mathf.Max(0f, ComputeRadius() - 0.35f);
            carryFollow = follow / (1f + rb.mass / 150f);
            carrySpeed = maxCarrySpeed / (1f + rb.mass / 300f);
            wasGravity = rb.useGravity;
            wasDrag = rb.linearDamping;
            wasAngularDrag = rb.angularDamping;
            rb.useGravity = false;
            rb.angularDamping = 4f;
            IgnorePlayer(true);
        }

        /// <summary>Radio (m) de la caja que envuelve todos los renderers del objeto. Se calcula al agarrar, no por frame.</summary>
        private float ComputeRadius()
        {
            if (renderers == null) return 0f;
            bool any = false;
            Bounds bounds = default;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any ? bounds.extents.magnitude : 0f;
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
            Vector3 point = target.position + target.forward * carryOffsetForward;
            Vector3 toTarget = point - rb.position;
            rb.linearVelocity = Vector3.ClampMagnitude(toTarget * carryFollow, carrySpeed);
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
