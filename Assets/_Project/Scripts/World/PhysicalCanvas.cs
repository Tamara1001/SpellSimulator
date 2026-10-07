using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Interfaces;
using SpellSimulator.Player;

namespace SpellSimulator.World
{
    /// <summary>
    /// Lienzo que se agarra con E. Pensado para dibujar siempre comodo:
    /// - al soltarlo se apoya solo en la mesa, el piso o la pared si hay una a mano; si no, queda flotando (con un
    ///   leve vaiven) a la altura donde se lo dejo, parado y derecho;
    /// - Q lo arroja (cae con fisica y aterriza con la cara hacia arriba);
    /// - F lo da vuelta: la magia sale por la cara con tinta, asi que girarlo cambia hacia donde apunta el hechizo;
    /// - V (PC) lo fija delante de la camara y el mouse pasa a ser un puntero para dibujar sin sostener la mira.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class PhysicalCanvas : MonoBehaviour, IInteractable, IThrowable, IFlippable, IFocusable
    {
        [Header("Settings")]
        [SerializeField] private string objectName = "Lienzo Mágico";
        [SerializeField, Tooltip("Corrección de rotación al sostenerlo en la mano izquierda")]
        private Vector3 holdRotationOffset = Vector3.zero;

        [Header("Al soltarlo")]
        [SerializeField, Tooltip("Si al soltarlo queda flotando (o apoyado) en vez de caer. Desactivar en piedras con sello.")]
        private bool floatOnDrop = true;
        [SerializeField, Tooltip("Distancia (m) a la que una superficie cercana lo atrapa para apoyarlo.")]
        private float placeReach = 0.3f;
        [SerializeField, Tooltip("Empieza flotando en el aire donde esta puesto (lienzos decorativos y de practica).")]
        private bool startFloating;
        [SerializeField] private float bobHeight = 0.012f;
        [SerializeField] private float bobSpeed = 1.1f;
        [SerializeField, Tooltip("Segundos de las animaciones de apoyar, enderezar y dar vuelta.")]
        private float poseSeconds = 0.18f;

        [Header("Papel arrojado")]
        [SerializeField, Tooltip("Si, al caer suelto, se acuesta solo con la cara hacia arriba.")]
        private bool settleFlat = true;
        [SerializeField, Tooltip("Aceleracion angular (rad/s2) que acuesta la hoja.")]
        private float settleTorque = 40f;
        [SerializeField, Tooltip("Frenado del giro al acostarse (1/s).")]
        private float settleDamping = 6f;
        [SerializeField, Tooltip("Frenado del aire mientras cae suelta (planea un poco).")]
        private float airDrag = 1.2f;

        [Header("Modo dibujo de escritorio (PC)")]
        [SerializeField, Range(0.3f, 0.95f), Tooltip("Cuanto de la pantalla ocupa el lienzo fijado a la camara.")]
        private float focusFill = 0.7f;

        private Rigidbody rb;
        private Collider col;
        private DrawingCanvas canvas;
        private Transform holdParent;
        private string prompt;

        // Estado de reposo.
        private bool floating;
        private Vector3 floatAnchor;
        private float bobPhase;
        private bool focused;

        // Animacion de pose (sin allocations).
        private bool animating, animLocal;
        private float animT;
        private Vector3 animFromPos, animToPos;
        private Quaternion animFromRot, animToRot;

        public HandType PreferredHand => HandType.Left;
        public bool Focused => focused;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
            canvas = GetComponent<DrawingCanvas>();
            bobPhase = Random.value * 6.28f;
        }

        private void Start()
        {
            if (startFloating) StartFloating();
        }

        // Se arma una vez: el interactor lo pide en cada frame (sin GC).
        public string GetInteractionPrompt() => prompt ??= $"Agarrar {objectName}";

        // ---------------------------------------------------------------- agarrar / soltar

        public void OnInteract(Transform holdPoint)
        {
            holdParent = holdPoint;
            floating = false;
            animating = false;
            rb.isKinematic = true;

            // Para que no "empuje" al jugador al estar pegado a su cuerpo lo volvemos un Trigger: el rayo para dibujar
            // SI detecta triggers.
            col.isTrigger = true;

            transform.SetParent(holdPoint);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.Euler(holdRotationOffset);
        }

        public void OnDrop()
        {
            if (focused) SetFocus(null, false);
            holdParent = null;
            transform.SetParent(null);
            rb.isKinematic = false;
            col.isTrigger = false;

            if (floatOnDrop)
            {
                if (TryPlace()) return;
                StartFloating();
                return;
            }
            Fall();
        }

        /// <summary>Arrojado (Q): cae con fisica y se acuesta con la cara hacia arriba.</summary>
        public void Throw(Vector3 velocity)
        {
            floating = false;
            animating = false;
            transform.SetParent(null);
            Fall();
            rb.linearVelocity = velocity;
            rb.angularVelocity = Random.insideUnitSphere * 2f;
        }

        private void Fall()
        {
            floating = false;
            rb.isKinematic = false;
            col.isTrigger = false;
            rb.useGravity = true;
            rb.linearDamping = airDrag;
        }

        // ---------------------------------------------------------------- apoyar / flotar

        private static readonly RaycastHit[] placeHits = new RaycastHit[12];

        /// <summary>Si hay una mesa, el piso o una pared a mano, lo apoya ahi (pegado y derecho).</summary>
        private bool TryPlace()
        {
            Vector3 center = transform.position;
            Vector3 normal = canvas != null ? canvas.FaceNormal : transform.forward;
            RaycastHit best = default;
            float bestDistance = float.MaxValue;
            foreach (var dir in new[] { Vector3.down, -normal, normal })
                if (Probe(center, dir, out RaycastHit hit) && hit.distance < bestDistance) { best = hit; bestDistance = hit.distance; }
            if (bestDistance == float.MaxValue) return false;

            Vector3 up = Vector3.up;
            Quaternion rotation;
            if (best.normal.y > 0.7f)
            {
                // Sobre una superficie horizontal: cara arriba, borde de arriba alejandose de quien mira.
                Camera cam = Camera.main;
                Vector3 away = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, up) : transform.forward;
                if (away.sqrMagnitude < 1e-4f) away = transform.forward;
                rotation = Orient(transform.rotation, normal, up, away);
            }
            else if (Mathf.Abs(best.normal.y) < 0.3f)
            {
                // Contra una pared: de cara al cuarto, con el borde de arriba arriba.
                rotation = Orient(transform.rotation, normal, best.normal, up);
            }
            else return false;

            Vector3 position = best.point + best.normal * 0.004f;
            StartPose(position, rotation, false);
            rb.isKinematic = true;
            col.isTrigger = false;
            floating = false;
            // Si descansa sobre algo que se puede mover, viaja con ello.
            if (best.rigidbody != null && !best.rigidbody.isKinematic) transform.SetParent(best.collider.transform, true);
            return true;
        }

        private bool Probe(Vector3 from, Vector3 dir, out RaycastHit best)
        {
            int n = Physics.RaycastNonAlloc(from, dir, placeHits, placeReach, ~0, QueryTriggerInteraction.Ignore);
            best = default;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = placeHits[i];
                if (h.distance >= bestDistance || h.collider.transform.IsChildOf(transform)) continue;
                if (h.collider.GetComponentInParent<PlayerMovement>() != null) continue;
                best = h;
                bestDistance = h.distance;
            }
            return bestDistance < float.MaxValue;
        }

        /// <summary>Rotacion que lleva la cara visible hacia face y el borde de arriba lo mas cerca de top.</summary>
        private static Quaternion Orient(Quaternion rotation, Vector3 currentFace, Vector3 face, Vector3 top)
        {
            Quaternion r = Quaternion.FromToRotation(currentFace, face) * rotation;
            Vector3 currentTop = Vector3.ProjectOnPlane(r * Vector3.up, face);
            Vector3 wantedTop = Vector3.ProjectOnPlane(top, face);
            if (currentTop.sqrMagnitude < 1e-6f || wantedTop.sqrMagnitude < 1e-6f) return r;
            return Quaternion.AngleAxis(Vector3.SignedAngle(currentTop, wantedTop, face), face) * r;
        }

        /// <summary>Queda en el aire, parado y derecho, mirando hacia donde mira su cara.</summary>
        private void StartFloating()
        {
            Vector3 normal = canvas != null ? canvas.FaceNormal : transform.forward;
            Vector3 face = Mathf.Abs(normal.y) > 0.75f
                ? (normal.y > 0f ? Vector3.up : Vector3.down)
                : new Vector3(normal.x, 0f, normal.z).normalized;
            Vector3 top = Mathf.Abs(normal.y) > 0.75f ? transform.up : Vector3.up;
            Quaternion rotation = Orient(transform.rotation, normal, face, top);
            rb.isKinematic = true;
            col.isTrigger = true; // flotando no estorba: el cuerpo lo atraviesa, el rayo lo detecta
            floating = true;
            floatAnchor = transform.position;
            StartPose(transform.position, rotation, false);
        }

        // ---------------------------------------------------------------- animacion de pose

        private void StartPose(Vector3 position, Quaternion rotation, bool local)
        {
            animLocal = local;
            animFromPos = local ? transform.localPosition : transform.position;
            animFromRot = local ? transform.localRotation : transform.rotation;
            animToPos = position;
            animToRot = rotation;
            animT = 0f;
            animating = true;
        }

        private void Update()
        {
            if (animating)
            {
                animT += Time.deltaTime / Mathf.Max(0.01f, poseSeconds);
                float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(animT));
                Vector3 p = Vector3.Lerp(animFromPos, animToPos, s);
                Quaternion q = Quaternion.Slerp(animFromRot, animToRot, s);
                if (animLocal) transform.SetLocalPositionAndRotation(p, q);
                else transform.SetPositionAndRotation(p, q);
                if (animT >= 1f)
                {
                    animating = false;
                    if (floating) floatAnchor = transform.position;
                }
                return;
            }
            if (floating)
                transform.position = floatAnchor + Vector3.up * (Mathf.Sin(Time.time * bobSpeed + bobPhase) * bobHeight);
        }

        /// <summary>
        /// Una hoja suelta cae y se acuesta sola con la cara hacia arriba: un papel real se dobla y cae plano; la caja
        /// fina del collider, en cambio, quedaba parada de canto. Asi siempre se puede escribir en ella.
        /// </summary>
        private void FixedUpdate()
        {
            if (!settleFlat || holdParent != null || floating || rb.isKinematic) return;
            Vector3 normal = canvas != null ? canvas.FaceNormal : transform.forward;
            float facing = Vector3.Dot(normal, Vector3.up);
            Vector3 axis = Vector3.Cross(normal, Vector3.up);
            if (axis.sqrMagnitude < 1e-5f) return;
            rb.AddTorque(axis * settleTorque - rb.angularVelocity * settleDamping * (1f - Mathf.Max(0f, facing)), ForceMode.Acceleration);
        }

        // ---------------------------------------------------------------- dar vuelta

        /// <summary>Gira la hoja media vuelta sobre su eje vertical: la cara con tinta pasa al otro lado.</summary>
        public void Flip()
        {
            // Si estaba apoyada contra algo, se despega y queda flotando (si no, el giro la atravesaria).
            Quaternion target = transform.localRotation * Quaternion.Euler(0f, 180f, 0f);
            StartPose(transform.localPosition, target, true);
        }

        // ---------------------------------------------------------------- fijar a la camara (PC)

        public void SetFocus(Transform cameraTransform, bool on)
        {
            if (on == focused) return;
            if (on)
            {
                if (cameraTransform == null || holdParent == null) return;
                var cam = cameraTransform.GetComponent<Camera>();
                float vfov = cam != null ? cam.fieldOfView : 60f;
                float aspect = cam != null ? cam.aspect : 1.78f;
                Vector2 size = canvas != null ? canvas.WorldSize : Vector2.one * 0.5f;
                float tan = Mathf.Tan(vfov * 0.5f * Mathf.Deg2Rad);
                float distance = Mathf.Max(size.y / focusFill / (2f * tan), size.x / focusFill / (2f * tan * aspect));
                distance = Mathf.Clamp(distance, 0.35f, 1.4f);

                focused = true;
                animating = false;
                transform.SetParent(cameraTransform, true);
                Quaternion world = Orient(transform.rotation, canvas != null ? canvas.FaceNormal : transform.forward,
                    -cameraTransform.forward, cameraTransform.up);
                transform.SetPositionAndRotation(cameraTransform.position + cameraTransform.forward * distance, world);
                FocusMode.Enter();
            }
            else
            {
                focused = false;
                FocusMode.Exit();
                if (holdParent != null)
                {
                    transform.SetParent(holdParent, false);
                    transform.localPosition = Vector3.zero;
                    transform.localRotation = Quaternion.Euler(holdRotationOffset);
                }
            }
        }
    }
}
