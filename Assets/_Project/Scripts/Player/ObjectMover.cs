using UnityEngine;
using SpellSimulator.Interfaces;
using SpellSimulator.World;

namespace SpellSimulator.Player
{
    /// <summary>
    /// Mover objetos a distancia estilo Garry's Mod (PC): el interactor (E sobre algo movible) llama a <see cref="TryBegin"/>
    /// y el objeto se sostiene con fisica delante de la camara. Rueda = distancia, Shift + rueda = girar sobre el eje de la
    /// mira, clic derecho + mouse = rotar el objeto (la vista queda quieta). Otra E lo suelta; el clic de la rueda lo deja
    /// flotando donde esta (<see cref="End"/>). En VR no hace nada.
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    public class ObjectMover : MonoBehaviour
    {
        private const float PickRange = 12f;
        private const float MinDistance = 0.8f;
        private const float MaxDistance = 7f;
        /// <summary>Un objeto grande se sostiene mas lejos: distancia minima = radio * HoldRadiusFactor + HoldMargin.</summary>
        private const float HoldRadiusFactor = 1.5f, HoldMargin = 0.7f;
        private const float DistanceStep = 0.35f;
        private const float RollStep = 15f;
        private const float RotateDegreesPerUnit = 0.6f;
        private const float FollowGain = 18f;
        private const float MaxLinearSpeed = 25f;
        private const float RotationGain = 12f;
        private const float MaxAngularSpeed = 20f;
        private const float GenericDamping = 3f;

        private static readonly RaycastHit[] pickHits = new RaycastHit[16];

        private PlayerInputReader inputReader;
        private Camera cam;
        private Rigidbody playerBody;

        private bool holding;
        private bool pinRequested;
        private Rigidbody held;
        private IRemoteGrabbable grabbable;
        // Marca de "quien lo sostiene" (dato, F0): se fija al empezar y se limpia en ClearHeld.
        private Holdable heldHoldable;
        private float holdDistance;
        private float minHoldDistance = MinDistance, maxHoldDistance = MaxDistance;
        private Quaternion relativeRotation;

        // Valores originales del objeto genérico (sin IRemoteGrabbable), para restaurarlos al soltar.
        private bool savedGravity, savedKinematic;
        private float savedLinearDamping, savedAngularDamping;

        // Colliders del objeto y del jugador que se ignoraron mientras se sostiene (se revierte al soltar).
        private Collider[] heldColliders;
        private Collider[] playerColliders;

        /// <summary>True mientras se sostiene un objeto a distancia.</summary>
        public bool IsHolding => holding;
        /// <summary>True si se sostiene y el clic derecho esta apretado: el mouse rota el objeto y no la vista.</summary>
        public bool IsRotating => holding && inputReader.IsRotateHeld;

        private void Awake()
        {
            inputReader = GetComponent<PlayerInputReader>();
            cam = Camera.main;
            playerBody = GetComponent<Rigidbody>();
        }

        private void OnDisable()
        {
            if (holding) ClearHeld();
        }

        private void Update()
        {
            if (PlatformDetector.IsVR || cam == null) return;

            if (!holding) return;

            // Objeto destruido o desactivado: se suelta sin callbacks.
            if (held == null || !held.gameObject.activeInHierarchy)
            {
                ClearHeld();
                return;
            }

            float scroll = inputReader.Scroll;
            if (scroll != 0f)
            {
                float step = Mathf.Sign(scroll);
                if (inputReader.IsRunning)
                    relativeRotation = Quaternion.AngleAxis(step * RollStep, Vector3.forward) * relativeRotation;
                else if (!FocusMode.CanvasView)
                    holdDistance = Mathf.Clamp(holdDistance + step * DistanceStep, minHoldDistance, maxHoldDistance);
            }

            if (inputReader.IsRotateHeld)
            {
                Vector2 look = inputReader.LookValue * RotateDegreesPerUnit;
                // Multiplicar por la izquierda rota alrededor de los ejes de la camara (la rotacion relativa vive en ese espacio).
                relativeRotation = Quaternion.AngleAxis(look.x, Vector3.up) * relativeRotation;
                relativeRotation = Quaternion.AngleAxis(-look.y, Vector3.right) * relativeRotation;
            }
        }

        private void FixedUpdate()
        {
            if (!holding || held == null || cam == null) return;
            // Vista completa (V) de lo que se sostiene: la camara se coloca frente al objeto, asi que seguirla lo haria huir
            // sin parar. Mientras dura, el objeto queda quieto donde esta.
            if (SurfaceFocus.IsActive)
            {
                if (!held.isKinematic) { held.linearVelocity = Vector3.zero; held.angularVelocity = Vector3.zero; }
                return;
            }
            Vector3 point = cam.transform.position + cam.transform.forward * holdDistance;
            if (held.isKinematic)
            {
                // Cinematicos conducidos (el grimorio): se llevan con MovePosition/MoveRotation. Los demas (lienzo fijo a la camara) no.
                if (grabbable != null && grabbable.DriveKinematic)
                {
                    held.MovePosition(point);
                    held.MoveRotation(cam.transform.rotation * relativeRotation);
                }
                return;
            }
            held.linearVelocity = Vector3.ClampMagnitude((point - held.position) * FollowGain, MaxLinearSpeed);

            Quaternion targetRotation = cam.transform.rotation * relativeRotation;
            Quaternion delta = targetRotation * Quaternion.Inverse(held.rotation);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            Vector3 angular = axis * (angle * Mathf.Deg2Rad * RotationGain);
            held.angularVelocity = Vector3.ClampMagnitude(angular, MaxAngularSpeed);
        }

        /// <summary>Da vuelta lo que se arrastra media vuelta sobre su propio eje vertical (F).</summary>
        public void Flip()
        {
            if (holding) relativeRotation = relativeRotation * Quaternion.Euler(0f, 180f, 0f);
        }

        /// <summary>Cuerpo que se esta moviendo (null si ninguno).</summary>
        public Rigidbody HeldBody => holding ? held : null;

        /// <summary>Empieza a mover el cuerpo a la distancia dada. Devuelve false si ya se esta moviendo algo.</summary>
        public bool TryBegin(Rigidbody rb, float distance)
        {
            if (holding || rb == null || PlatformDetector.IsVR || cam == null) return false;
            Begin(rb, distance);
            return true;
        }

        private static readonly System.Collections.Generic.List<Collider> boundsBuffer = new System.Collections.Generic.List<Collider>(16);

        /// <summary>Radio aproximado del cuerpo (mitad de la diagonal de sus colliders).</summary>
        private static float BoundsRadius(Rigidbody rb)
        {
            rb.GetComponentsInChildren(false, boundsBuffer);
            if (boundsBuffer.Count == 0) return 0.3f;
            Bounds b = boundsBuffer[0].bounds;
            for (int i = 1; i < boundsBuffer.Count; i++) b.Encapsulate(boundsBuffer[i].bounds);
            return b.extents.magnitude;
        }

        /// <summary>Suelta lo que se mueve: cae, o queda flotando donde esta si pin es true.</summary>
        public void End(bool pin)
        {
            if (!holding) return;
            pinRequested = pin;
            Release();
        }

        private void Begin(Rigidbody rb, float distance)
        {
            held = rb;
            holding = true;
            pinRequested = false;
            // Objetos grandes (rocas): por defecto quedan lejos y no se atraviesan al caminar; el tope tambien crece un poco con el tamano.
            float radius = BoundsRadius(rb);
            minHoldDistance = Mathf.Max(MinDistance, radius * HoldRadiusFactor + HoldMargin);
            maxHoldDistance = Mathf.Max(MaxDistance, minHoldDistance + 1.5f);
            holdDistance = Mathf.Clamp(Mathf.Max(distance, minHoldDistance), minHoldDistance, maxHoldDistance);
            relativeRotation = Quaternion.Inverse(cam.transform.rotation) * rb.rotation;

            grabbable = rb.GetComponentInParent<IRemoteGrabbable>();
            if (grabbable != null)
            {
                grabbable.OnRemoteGrab();
            }
            else
            {
                // Si estaba fijado en el aire, se desfija primero: asi savedKinematic guarda el estado original y no "fijo".
                PinnedBody.Unpin(rb);
                savedGravity = rb.useGravity;
                savedKinematic = rb.isKinematic;
                savedLinearDamping = rb.linearDamping;
                savedAngularDamping = rb.angularDamping;
                rb.useGravity = false;
                rb.isKinematic = false;
                rb.linearDamping = GenericDamping;
                rb.angularDamping = GenericDamping;
            }
            // Evita que atraviese el piso al chocar fuerte (ver KI de fisicas).
            if (!rb.isKinematic) rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            IgnorePlayerCollisions(true);

            heldHoldable = Holdable.GetOrAdd(rb.gameObject);
            heldHoldable.SetHeldBy(Holdable.LocalOwnerId);
        }

        /// <summary>Suelta el objeto: avisa a IRemoteGrabbable, o restaura los valores originales del generico.</summary>
        private void Release()
        {
            bool pin = pinRequested;
            Rigidbody rb = held;
            IRemoteGrabbable target = grabbable;
            ClearHeld();

            if (rb == null) return;
            if (target != null)
            {
                target.OnRemoteRelease(pin);
                return;
            }

            rb.useGravity = savedGravity;
            rb.linearDamping = savedLinearDamping;
            rb.angularDamping = savedAngularDamping;
            if (pin)
            {
                PinnedBody.Pin(rb, savedKinematic, savedGravity);
            }
            else
            {
                rb.isKinematic = savedKinematic;
            }
        }

        /// <summary>Limpia el estado de sujecion y revierte las colisiones ignoradas. No llama callbacks.</summary>
        private void ClearHeld()
        {
            IgnorePlayerCollisions(false);
            holding = false;
            pinRequested = false;
            held = null;
            grabbable = null;
            // Despues de limpiar: quien escuche HeldChanged ve el movedor ya libre.
            Holdable holdable = heldHoldable;
            heldHoldable = null;
            if (holdable != null) holdable.Release();
        }

        private void IgnorePlayerCollisions(bool ignore)
        {
            if (ignore)
            {
                heldColliders = held != null ? held.GetComponentsInChildren<Collider>() : null;
                playerColliders = GetComponentsInChildren<Collider>();
            }
            if (heldColliders == null || playerColliders == null) return;

            foreach (var mine in heldColliders)
            {
                if (mine == null) continue;
                foreach (var theirs in playerColliders)
                    if (theirs != null) Physics.IgnoreCollision(mine, theirs, ignore);
            }

            if (!ignore)
            {
                heldColliders = null;
                playerColliders = null;
            }
        }
    }
}
