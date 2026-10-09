using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Interfaces;
using SpellSimulator.Player;
using SpellSimulator.Core;

namespace SpellSimulator.World
{
    /// <summary>
    /// Lienzo que se agarra con E. Pensado para dibujar siempre comodo:
    /// - al soltarlo (E) cae con fisica y se acuesta solo con la cara hacia arriba;
    /// - G lo deja flotando en el aire donde esta, con un vaiven suave (y E vuelve a agarrarlo);
    /// - Q lo arroja;
    /// - F lo da vuelta: la magia sale por la cara con tinta, asi que girarlo cambia hacia donde apunta el hechizo;
    /// - V (PC) lo fija delante de la camara y el mouse pasa a ser un puntero para dibujar sin sostener la mira.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class PhysicalCanvas : MonoBehaviour, IInteractable, IThrowable, IFlippable, IFocusable, IPinnable, IRemoteGrabbable, IResettable, IHudInfo
    {
        [Header("Settings")]
        [SerializeField] private string objectName = "Lienzo Mágico";
        [SerializeField, Tooltip("Corrección de rotación al sostenerlo en la mano izquierda")]
        private Vector3 holdRotationOffset = Vector3.zero;

        [Header("Flotando (G)")]
        [SerializeField, Tooltip("Empieza flotando en el aire donde esta puesto (lienzos decorativos y de practica).")]
        private bool startFloating;
        [SerializeField, Tooltip("Altura (m) del vaiven.")] private float bobHeight = 0.03f;
        [SerializeField, Tooltip("Velocidad (rad/s) del vaiven.")] private float bobSpeed = 1.3f;
        [SerializeField, Tooltip("Cabeceo maximo (grados) del vaiven. Chico: se puede dibujar encima.")]
        private float swayDegrees = 2.5f;
        [SerializeField, Tooltip("Desplazamiento lateral (m) de la deriva.")] private float driftMeters = 0.015f;
        [SerializeField, Tooltip("Segundos de las animaciones de enderezar y dar vuelta.")]
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
        private string prompt, promptExhausted, hudNameExhausted;
        private Rune rune;

        // Estado de reposo.
        private bool floating;
        // Lo que el jugador deja flotando se cae solo al minuto (los que flotan por diseno no).
        private const float FloatSeconds = 60f;
        private bool floatTimed;
        private float floatSince;
        private Vector3 floatAnchor;
        private Quaternion floatRotation = Quaternion.identity;
        private float bobPhase;
        private bool focused;

        // Cuaderno de paginas (si el objeto lo tiene): cacheado en Awake. El nombre de la HUD se rearma solo al cambiar de pagina.
        private NotebookPages notebook;
        private string hudName;
        private int hudPage = -1;

        // Animacion de pose (sin allocations).
        private bool animating, animLocal;
        private float animT;
        private Vector3 animFromPos, animToPos;
        private Quaternion animFromRot, animToRot;

        public HandType PreferredHand => HandType.Left;
        public bool Focused => focused;

        // ---- Interfaz (HUD de abajo)
        public string HudName
        {
            get
            {
                if (notebook == null)
                {
                    if (rune != null && rune.IsExhausted) return hudNameExhausted ??= objectName + " (agotada)";
                    return objectName;
                }
                if (notebook.Page != hudPage || hudName == null)
                {
                    hudPage = notebook.Page;
                    hudName = $"{objectName} (pag. {hudPage + 1}/{notebook.PageCount})";
                }
                return hudName;
            }
        }
        public Color HudColor => new Color(0.93f, 0.85f, 0.6f);
        public string HudHints => notebook != null ? NotebookHints : "Clic: dibujar con una herramienta | F: dar vuelta | Tab: a pantalla | E: dejar flotando | Q: arrojar";
        public float HudGauge => -1f;

        private const string NotebookHints = "Z/X: pasar página | F: dar vuelta | Tab: a pantalla | Clic der.+mouse: girar | E: dejar flotando";

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
            canvas = GetComponent<DrawingCanvas>();
            TryGetComponent(out notebook);
            TryGetComponent(out rune);
            bobPhase = Random.value * 6.28f;
            initialKinematic = rb.isKinematic;
            initialGravity = rb.useGravity;
            initialDrag = rb.linearDamping;
        }

        private bool initialKinematic, initialGravity;
        private float initialDrag;

        // ---------------------------------------------------------------- agarre a distancia (G) y reinicio

        private bool remoteHeld;
        // Un lienzo que cayo (soltado o arrojado) se acuesta cara arriba; uno apoyado de pie desde el arranque no.
        private bool settling;

        public void OnRemoteGrab()
        {
            remoteHeld = true;
            if (focused) SetFocus(null, false);
            floating = false;
            animating = false;
            holdParent = null;
            transform.SetParent(null);
            col.isTrigger = false;
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.linearDamping = 3f;
        }

        public void OnRemoteRelease(bool pin)
        {
            remoteHeld = false;
            if (focused) SetFocus(null, false);
            // La vista completa (V) lo habia hecho hijo de la camara: sin esto, soltarlo con E/Q/rueda lo dejaba pegado al centro de la pantalla.
            transform.SetParent(null);
            if (pin) StartFloating(true);
            else Fall();
        }

        /// <summary>Reinicio del mundo: vuelve a su estado de fisica inicial (la pose la repone Respawnable).</summary>
        public void ResetToInitial()
        {
            if (focused) SetFocus(null, false);
            holdParent = null;
            floating = false;
            animating = false;
            transform.SetParent(null);
            col.isTrigger = false;
            if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            settling = false;
            rb.isKinematic = initialKinematic;
            rb.useGravity = initialGravity;
            rb.linearDamping = initialDrag;
            if (startFloating) StartFloating();
        }

        private void Start()
        {
            if (startFloating) StartFloating();
        }

        // Se arma una vez: el interactor lo pide en cada frame (sin GC).
        public string GetInteractionPrompt()
        {
            if (rune != null && rune.IsExhausted) return promptExhausted ??= $"Agarrar {objectName} (agotada)";
            return prompt ??= $"Agarrar {objectName}";
        }

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
            Fall(); // al soltar cae y se acuesta cara arriba; G lo deja flotando
        }

        /// <summary>G: lo deja flotando en el aire, enderezado, con un vaiven suave. El interactor ya lo solto.</summary>
        public void Pin() => StartFloating(true);

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
            settling = true;
            floating = false;
            rb.isKinematic = false;
            col.isTrigger = false;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearDamping = airDrag;
        }

        // ---------------------------------------------------------------- flotar

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
        private void StartFloating(bool timed = false)
        {
            floatTimed = timed;
            floatSince = GameClock.Time;
            Vector3 normal = canvas != null ? canvas.FaceNormal : transform.forward;
            Vector3 face = Mathf.Abs(normal.y) > 0.75f
                ? (normal.y > 0f ? Vector3.up : Vector3.down)
                : new Vector3(normal.x, 0f, normal.z).normalized;
            Vector3 top = Mathf.Abs(normal.y) > 0.75f ? transform.up : Vector3.up;
            Quaternion rotation = Orient(transform.rotation, normal, face, top);
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
            col.isTrigger = true; // flotando no estorba: el cuerpo lo atraviesa, el rayo lo detecta
            floating = true;
            floatAnchor = transform.position;
            floatRotation = rotation;
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
                animT += Time.deltaTime / Mathf.Max(0.01f, poseSeconds); // visual
                float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(animT));
                Vector3 p = Vector3.Lerp(animFromPos, animToPos, s);
                Quaternion q = Quaternion.Slerp(animFromRot, animToRot, s);
                if (animLocal) transform.SetLocalPositionAndRotation(p, q);
                else transform.SetPositionAndRotation(p, q);
                if (animT >= 1f)
                {
                    animating = false;
                    if (floating) { floatAnchor = transform.position; floatRotation = transform.rotation; }
                }
                return;
            }
            if (floating)
            {
                if (floatTimed)
                {
                    // Enfocado: no cae (se vuelve a contar el minuto).
                    if (focused) floatSince = GameClock.Time;
                    else if (PinMath.ShouldRelease(GameClock.Time, floatSince, FloatSeconds)) { Fall(); return; }
                }
                float t = Time.time * bobSpeed + bobPhase; // visual
                // Tres ondas de periodos distintos: se mueve sin repetirse de forma obvia.
                Vector3 offset = new Vector3(Mathf.Sin(t * 0.61f) * driftMeters, Mathf.Sin(t) * bobHeight, Mathf.Cos(t * 0.47f) * driftMeters);
                Quaternion sway = Quaternion.Euler(Mathf.Sin(t * 0.83f) * swayDegrees, Mathf.Sin(t * 0.53f + 1.3f) * swayDegrees * 1.4f, Mathf.Sin(t * 0.71f + 2.1f) * swayDegrees);
                transform.SetPositionAndRotation(floatAnchor + offset, floatRotation * sway);
            }
        }

        /// <summary>
        /// Una hoja suelta cae y se acuesta sola con la cara hacia arriba: un papel real se dobla y cae plano; la caja
        /// fina del collider, en cambio, quedaba parada de canto. Asi siempre se puede escribir en ella.
        /// </summary>
        private void FixedUpdate()
        {
            if (!settleFlat || !settling || remoteHeld || holdParent != null || floating || rb.isKinematic) return;
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
            if (floating)
            {
                StartPose(floatAnchor, floatRotation * Quaternion.Euler(0f, 180f, 0f), false);
                return;
            }
            Quaternion target = transform.localRotation * Quaternion.Euler(0f, 180f, 0f);
            StartPose(transform.localPosition, target, true);
        }

        // ---------------------------------------------------------------- fijar a la camara (PC)

        public void SetFocus(Transform cameraTransform, bool on)
        {
            if (on == focused) return;
            if (on)
            {
                if (cameraTransform == null || (holdParent == null && !remoteHeld)) return;
                // El transform recibido puede ser la raiz de la camara (CameraRootPC), no el objeto con el componente Camera: sin esto
                // el plano cercano real (0,3 m) se ignoraba y los lienzos chicos quedaban DELANTE de el (invisibles y sin dibujar).
                var cam = cameraTransform.GetComponent<Camera>();
                if (cam == null) cam = cameraTransform.GetComponentInChildren<Camera>();
                if (cam == null) cam = Camera.main; // presentacion: camara del lienzo en vista completa
                float vfov = cam != null ? cam.fieldOfView : 60f;
                float aspect = cam != null ? cam.aspect : 1.78f;
                Vector2 size = canvas != null ? canvas.WorldSize : Vector2.one * 0.5f;
                float tan = Mathf.Tan(vfov * 0.5f * Mathf.Deg2Rad);
                float distance = Mathf.Max(size.y / focusFill / (2f * tan), size.x / focusFill / (2f * tan * aspect));
                // Minimo: delante del plano cercano de la camara (un lienzo MUY chico se ve entero sin quedar recortado).
                float near = cam != null ? cam.nearClipPlane : 0.1f;
                distance = Mathf.Clamp(distance, Mathf.Max(0.1f, near * 2f), 1.4f);

                focused = true;
                animating = false;
                rb.isKinematic = true;
                transform.SetParent(cameraTransform, true);
                Quaternion world = Orient(transform.rotation, canvas != null ? canvas.FaceNormal : transform.forward,
                    -cameraTransform.forward, cameraTransform.up);
                // El CENTRO de la superficie (no el pivote) queda en el eje de la camara: si el pivote esta corrido del
                // centro del mesh, el lienzo se veia descentrado y el cursor no coincidia con la tinta.
                Vector3 centerOffset = canvas != null ? world * canvas.CenterOffsetLocalScaled : Vector3.zero;
                transform.SetPositionAndRotation(cameraTransform.position + cameraTransform.forward * distance - centerOffset, world);
                FocusMode.EnterCanvasView();
                FocusMode.FocusedCanvas = canvas;
            }
            else
            {
                focused = false;
                FocusMode.ExitCanvasView();
                if (remoteHeld)
                {
                    // Vuelve a ser arrastrado por el movedor.
                    transform.SetParent(null);
                    rb.isKinematic = false;
                    rb.useGravity = false;
                }
                else if (holdParent != null)
                {
                    transform.SetParent(holdParent, false);
                    transform.localPosition = Vector3.zero;
                    transform.localRotation = Quaternion.Euler(holdRotationOffset);
                }
            }
        }
    }
}
