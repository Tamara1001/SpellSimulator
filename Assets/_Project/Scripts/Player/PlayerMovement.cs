using UnityEngine;

namespace SpellSimulator.Player
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveForce = 50f;
        [SerializeField, Tooltip("Velocidad al caminar (m/s).")] private float walkSpeed = 4.2f;
        [SerializeField, Tooltip("Velocidad al correr (Shift).")] private float runSpeed = 7.4f;
        [SerializeField, Tooltip("Velocidad agachado.")] private float crouchSpeed = 2f;
        [SerializeField, Range(0f, 1f)] private float friction = 0.175f;
        [SerializeField] private float jumpForce = 5f;
        
        [Header("Physics Settings")]
        [SerializeField] private float customGravity = -15f;
        [SerializeField] private LayerMask groundLayer = Physics.DefaultRaycastLayers;
        [SerializeField, Tooltip("Altura total del jugador para calcular cuando toca el suelo")] 
        private float playerHeight = 2f;

        [Header("Escalones y pendientes")]
        [SerializeField, Tooltip("Altura maxima (m) que se sube sola al caminar contra un borde (umbral, escalon, roca chica).")]
        private float maxStepHeight = 0.35f;
        [SerializeField, Tooltip("Radio del cuerpo para detectar escalones (m).")]
        private float bodyRadius = 0.3f;

        [Header("Agacharse")]
        [SerializeField, Tooltip("Altura del cuerpo agachado (m).")] private float crouchHeight = 1.1f;
        [SerializeField, Tooltip("Cuanto baja la camara agachado (m).")] private float crouchCameraDrop = 0.5f;
        [SerializeField, Tooltip("Rapidez de la transicion (1/s).")] private float crouchBlend = 12f;

        [Header("Tumbarse")]
        [SerializeField, Tooltip("Altura del cuerpo tumbado (m). Se clampa a 2 radios de la capsula.")] private float proneHeight = 0.65f;
        [SerializeField, Tooltip("Cuanto baja la camara tumbado respecto a de pie (m).")] private float proneCameraDrop = 1.35f;
        [SerializeField, Tooltip("Velocidad al arrastrarse tumbado (m/s).")] private float proneSpeed = 0.9f;

        private Rigidbody rb;
        private PlayerInputReader inputReader;
        private CapsuleCollider body;
        private Transform cameraRoot;
        private float standHeight, standCameraY;
        private float stanceT; // 0 = de pie, 1 = agachado, 2 = tumbado
        private bool crouching;
        private bool prone;

        // VR: movimiento mas suave para no marear (aceleracion y frenada graduales, velocidad moderada, sin correr).
        private const float VrWalkSpeed = 2.6f;
        private const float VrCrouchSpeed = 1.2f;
        private const float VrMoveForce = 14f;
        private const float VrFriction = 0.45f;
        private bool vrTuned;

        private bool isGrounded;
        /// <summary>Agachado o tumbado (lo lee el tutorial de la terraza).</summary>
        public bool IsLow => stanceT > 0.5f;
        /// <summary>Con los pies en el piso (lo lee el tutorial de la terraza).</summary>
        public bool IsGrounded => isGrounded;
        private Vector3 groundNormal = Vector3.up;
        private bool hasInput;
        private bool readyToJump = true;

        private void Awake()
        {
            // Contexto de jugador para PlayerRegistry (F0 de MULTIPLAYER_PLAN).
            if (!TryGetComponent<LocalPlayerContext>(out _)) gameObject.AddComponent<LocalPlayerContext>();
            rb = GetComponent<Rigidbody>();
            inputReader = GetComponent<PlayerInputReader>();

            // Configuracion vital del Rigidbody para FPS
            rb.freezeRotation = true; // Evita que caigamos como un bolo
            rb.useGravity = false;    // Usaremos gravedad matematica personalizada
            rb.mass = 1f;            
            rb.linearDamping = 0f;

            // El cuerpo real es la capsula activa (la del objeto raiz puede estar apagada).
            foreach (var capsule in GetComponentsInChildren<CapsuleCollider>())
                if (capsule.enabled && !capsule.isTrigger) { body = capsule; break; }
            if (body != null) standHeight = body.height;
            var cam = Camera.main;
            if (cam != null && !PlatformDetector.IsVR && cam.transform.parent != null)
            {
                cameraRoot = cam.transform.parent;
                standCameraY = cameraRoot.localPosition.y;
            }
        }

        private void Update()
        {
            // VR: el cuerpo se mueve en FixedUpdate; sin interpolacion la cabeza se ve a saltos (50 Hz contra 72-90 Hz del visor) y marea.
            if (!vrTuned && PlatformDetector.IsVR)
            {
                vrTuned = true;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                // Altura: el origen del visor va a los pies del cuerpo (suelo) y la cabeza queda a la altura real del jugador.
                var xrOrigin = GetComponentInChildren<Unity.XR.CoreUtils.XROrigin>(true);
                if (xrOrigin != null)
                {
                    xrOrigin.RequestedTrackingOriginMode = Unity.XR.CoreUtils.XROrigin.TrackingOriginMode.Floor;
                    xrOrigin.transform.localPosition = new Vector3(0f, -standHeight * 0.5f, 0f);
                    Transform offset = xrOrigin.CameraFloorOffsetObject != null ? xrOrigin.CameraFloorOffsetObject.transform : null;
                    if (offset != null)
                    {
                        offset.localPosition = Vector3.zero;
                        cameraRoot = offset;
                        standCameraY = 0f;
                    }
                }
                // Sin friccion contra paredes y esquinas: se desliza en vez de quedar pegado (el frenado ya es propio).
                if (body != null)
                    body.sharedMaterial = new PhysicsMaterial("VrBody") { dynamicFriction = 0f, staticFriction = 0f, frictionCombine = PhysicsMaterialCombine.Minimum };
            }
            CheckGrounded();
            HandleStance();
            HandleJump();
        }

        /// <summary>
        /// Postura (PC): de pie (0), agachado (1) o tumbado (2) con un unico valor continuo stanceT.
        /// C mantenida (~0.35 s) alterna tumbado; agacharse solo se para si hay lugar arriba.
        /// </summary>
        private void HandleStance()
        {
            if (body == null) return;
            // VR: agacharse baja la altura de la capsula y el pivote de la camara (la altura real de la cabeza la pone el visor encima).
            if (PlatformDetector.IsVR && cameraRoot == null)
            {
                var vrCam = Camera.main;
                if (vrCam != null && vrCam.transform.parent != null)
                {
                    cameraRoot = vrCam.transform.parent;
                    standCameraY = cameraRoot.localPosition.y;
                }
            }

            // C mantenida: si esta tumbado se levanta solo si hay altura libre hasta la altura de pie; si no, queda tumbado.
            if (inputReader.IsTogglingProne)
            {
                if (!prone) prone = true;
                else if (HasHeadroom(standHeight)) prone = false;
            }

            float target;
            if (prone) target = 2f; // tumbado: ignora Ctrl y no se levanta por agacharse
            else
            {
                bool physical = PhysicalCrouch();
                // VR: si te agachaste de verdad la camara ya bajo sola; con el boton se baja bastante mas que en PC.
                crouchByBody = physical && !inputReader.IsCrouching;
                if (inputReader.IsCrouching || physical) crouching = true;
                else if (crouching && HasHeadroom(standHeight)) crouching = false;
                target = crouching ? 1f : 0f;
            }

            float previousT = stanceT;
            stanceT = Mathf.MoveTowards(stanceT, target, crouchBlend * Time.deltaTime);
            if (stanceT == previousT) return; // quieto: no se toca el collider ni la camara
            float height = StanceHeight(stanceT);
            body.height = height;
            // Los pies quedan en el mismo lugar: el centro se desplaza la mitad de lo que cambia la altura.
            body.center = new Vector3(0f, (height - standHeight) * 0.5f, 0f);
            if (cameraRoot != null)
            {
                var p = cameraRoot.localPosition;
                p.y = standCameraY - StanceCameraDrop(stanceT);
                cameraRoot.localPosition = p;
            }
        }

        private const float VrPhysicalCrouchDrop = 0.35f;
        private float vrHeadBaseline = -1f;
        private Transform vrHead;

        /// <summary>
        /// VR: agacharse de verdad. La altura de la cabeza seguida por el visor (local a la raiz de la camara) se compara con la mas alta
        /// que se vio (se recalibra despacio): si baja mas de 35 cm, el cuerpo se agacha tambien.
        /// </summary>
        private bool PhysicalCrouch()
        {
            if (!PlatformDetector.IsVR) return false;
            if (vrHead == null) { var cam = Camera.main; if (cam == null) return false; vrHead = cam.transform; }
            float y = vrHead.localPosition.y;
            if (vrHeadBaseline < 0f || y > vrHeadBaseline) vrHeadBaseline = y;
            else vrHeadBaseline = Mathf.MoveTowards(vrHeadBaseline, y, 0.02f * Time.deltaTime);
            return y < vrHeadBaseline - VrPhysicalCrouchDrop;
        }

        // La capsula no puede ser mas baja que su diametro.
        private float ProneHeight => Mathf.Max(proneHeight, body.radius * 2f);

        /// <summary>Altura de la capsula para la postura t, interpolada por tramos (de pie-agachado, agachado-tumbado).</summary>
        private float StanceHeight(float t)
        {
            return t <= 1f ? Mathf.Lerp(standHeight, crouchHeight, t) : Mathf.Lerp(crouchHeight, ProneHeight, t - 1f);
        }

        /// <summary>Cuanto baja la camara para la postura t, interpolado por tramos.</summary>
        private float StanceCameraDrop(float t)
        {
            float drop = t <= 1f ? crouchCameraDrop * t : Mathf.Lerp(crouchCameraDrop, proneCameraDrop, t - 1f);
            if (PlatformDetector.IsVR) drop *= crouchByBody ? 0f : VrCrouchDropFactor;
            return drop;
        }

        private bool crouchByBody;
        private const float VrCrouchDropFactor = 1.9f;

        /// <summary>SphereCast hacia arriba desde la cabeza actual: hay lugar si nada bloquea hasta targetHeight.</summary>
        private bool HasHeadroom(float targetHeight)
        {
            float radius = body.radius * 0.9f;
            float currentHeight = body.height;
            // Los pies estan fijos en -standHeight/2 (ver HandleStance): la esfera arranca bajo la cabeza actual.
            Vector3 from = transform.position + Vector3.up * (-standHeight * 0.5f + currentHeight - radius);
            float room = targetHeight - currentHeight + 0.05f;
            if (room <= 0f) return true;
            return !Physics.SphereCast(from, radius, Vector3.up, out _, room, groundLayer, QueryTriggerInteraction.Ignore);
        }

        private float CurrentMaxSpeed
        {
            get
            {
                if (PlatformDetector.IsVR) return crouching ? VrCrouchSpeed : VrWalkSpeed;
                return prone ? proneSpeed : crouching ? crouchSpeed : inputReader.IsRunning ? runSpeed : walkSpeed;
            }
        }

        private void FixedUpdate()
        {
            HandleMovement();
            ApplyCustomGravity();
        }

        private void CheckGrounded()
        {
            // Raycast desde el centro hacia abajo. La mitad de la altura + 0.1f de margen.
            isGrounded = Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, (playerHeight * 0.5f) + 0.1f, groundLayer, QueryTriggerInteraction.Ignore);
            groundNormal = isGrounded ? hit.normal : Vector3.up;
        }

        private void HandleMovement()
        {
            // Direccion
            Vector2 input = inputReader.MoveValue;
            bool vr = PlatformDetector.IsVR;
            float rawMagnitude = input.magnitude;
            // VR: respuesta cuadratica del stick (fino al empezar a empujar) y fuerza / frenada propias, mas suaves.
            if (vr) input *= rawMagnitude;
            float force = vr ? VrMoveForce : moveForce;
            float brake = vr ? VrFriction : friction;

            // Usar la camara principal como referencia para saber hacia donde estamos mirando
            Transform refTransform = Camera.main != null ? Camera.main.transform : transform;
            
            Vector3 forward = refTransform.forward;
            Vector3 right = refTransform.right;
            
            // Ignorar la inclinacion vertical (Pitch) para no volar ni hundirnos
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 moveDir = right * input.x + forward * input.y;
            // PC: siempre a fuerza plena. VR: la fuerza sigue al stick (empujar poco = andar despacio).
            if (vr) moveDir = Vector3.ClampMagnitude(moveDir, 1f);
            else moveDir.Normalize();
            hasInput = rawMagnitude >= 0.1f;
            if (hasInput) TryStepUp(moveDir);

            // Velocidad actual plana (ignorando la Y de gravedad)
            Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

            // Counter movement (Friccion custom) para frenar rapido cuando soltamos las teclas
            if (rawMagnitude < 0.1f)
            {
                rb.AddForce(-flatVel * force * brake, ForceMode.Acceleration);
            }

            // Aplicar fuerza de movimiento
            if (isGrounded || float.IsFinite(stepTargetFeetY))
            {
                rb.AddForce(moveDir * force, ForceMode.Acceleration);
            }
            else
            {
                // En el aire tenemos mucho menos control (20%)
                rb.AddForce(moveDir * force * 0.2f, ForceMode.Acceleration);
            }

            // Limitar velocidad maxima para no acelerar infinitamente
            flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            float maxSpeed = CurrentMaxSpeed;
            if (flatVel.magnitude > maxSpeed)
            {
                Vector3 limitedVel = flatVel.normalized * maxSpeed;
                rb.linearVelocity = new Vector3(limitedVel.x, rb.linearVelocity.y, limitedVel.z);
            }
        }

        private void HandleJump()
        {
            if (prone)
            {
                // Tumbado no se salta: apretar salto solo intenta levantarse (si hay lugar).
                if (inputReader.IsJumping && HasHeadroom(standHeight)) prone = false;
                return;
            }
            if (inputReader.IsJumping && isGrounded && readyToJump && !crouching)
            {
                readyToJump = false;
                
                // Resetear velocidad Y antes de saltar para que caidas previas no lo anulen
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
                
                Invoke(nameof(ResetJump), 0.25f);
            }
        }

        private void ResetJump()
        {
            readyToJump = true;
        }

        /// <summary>
        /// Sube bordes bajos (umbral de la torre, inicio de una rampa, roca chica) sin saltar: si a la altura de los pies
        /// hay una pared casi vertical y a la altura maxima de escalon no, se sube el cuerpo a la cima del borde.
        /// </summary>
        private const float VrStepHeight = 0.5f, VrClimbSpeed = 2.2f;
        private float stepTargetFeetY = float.NegativeInfinity;
        private float stepClimbUntil;

        private void TryStepUp(Vector3 moveDir)
        {
            bool vr = PlatformDetector.IsVR;
            // VR: sube el borde deslizandose hacia arriba (no de un salto, que marea) y mira tambien de costado para no
            // quedar trabado en esquinas y umbrales.
            if (vr && float.IsFinite(stepTargetFeetY))
            {
                float feetY = rb.position.y - playerHeight * 0.5f;
                if (feetY >= stepTargetFeetY - 0.02f || Time.time > stepClimbUntil) stepTargetFeetY = float.NegativeInfinity;
                else
                {
                    rb.linearVelocity = new Vector3(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, VrClimbSpeed), rb.linearVelocity.z);
                    return;
                }
            }
            if (!isGrounded) return;
            if (TryStep(moveDir, vr)) return;
            if (!vr) return;
            Quaternion left = Quaternion.AngleAxis(-40f, Vector3.up), right = Quaternion.AngleAxis(40f, Vector3.up);
            if (!TryStep(left * moveDir, true)) TryStep(right * moveDir, true);
        }

        private bool TryStep(Vector3 dir, bool vr)
        {
            float stepMax = vr ? VrStepHeight : maxStepHeight;
            float reach = bodyRadius + 0.2f;
            Vector3 feet = transform.position + Vector3.down * (playerHeight * 0.5f - 0.04f);
            if (!Physics.Raycast(feet, dir, out RaycastHit low, reach, groundLayer, QueryTriggerInteraction.Ignore)) return false;
            if (Vector3.Angle(low.normal, Vector3.up) < 50f) return false; // es una pendiente, no un escalon
            Vector3 top = feet + Vector3.up * stepMax;
            if (Physics.Raycast(top, dir, reach, groundLayer, QueryTriggerInteraction.Ignore)) return false; // muy alto
            if (!Physics.Raycast(top + dir * reach, Vector3.down, out RaycastHit landing, stepMax, groundLayer, QueryTriggerInteraction.Ignore)) return false;
            float rise = landing.point.y - (feet.y - 0.04f);
            if (rise <= 0.01f || rise > stepMax) return false;
            if (vr)
            {
                stepTargetFeetY = landing.point.y;
                stepClimbUntil = Time.time + 0.6f;
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, VrClimbSpeed), rb.linearVelocity.z);
            }
            else rb.position += Vector3.up * (rise + 0.02f);
            return true;
        }

        private void ApplyCustomGravity()
        {
            // Quieto sobre una rampa: la gravedad empuja contra el piso (no a lo largo) para no resbalar.
            Vector3 gravity = Vector3.up * customGravity;
            if (isGrounded && !hasInput) gravity = -groundNormal * Mathf.Abs(customGravity);
            // Corriente de aire ascendente (hechizo de viento hacia arriba): planea con poca gravedad y sube suave hasta la velocidad de la columna.
            float updraft = Output.WindColumn.UpdraftAt(rb.position);
            if (updraft > 0f)
            {
                gravity = Vector3.up * (customGravity * 0.2f);
                float missing = updraft - rb.linearVelocity.y;
                if (missing > 0f) gravity += Vector3.up * Mathf.Min(missing * 8f, 30f);
            }
            rb.AddForce(gravity, ForceMode.Acceleration);
        }
    }
}
