using System;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.DebugTools;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Player
{
    /// <summary>
    /// Mirar / agarrar / soltar / usar objetos.
    /// - Mano principal (derecha por defecto, ver PlayerSettings): 3 slots (teclas 1-3) con lo que se usa: lapiz, goma, estampas.
    ///   E sobre una herramienta la guarda en el primer slot libre y la equipa. Q la arroja. La rueda cambia el grosor.
    /// - Mano secundaria: 1 slot con lo que se agarra con E: lienzos, cajas, el grimorio... Se ARRASTRA por el mundo con fisica
    ///   (ObjectMover): rueda = distancia (o pagina si es un cuaderno), clic derecho + mouse = rotar, F = dar vuelta,
    ///   V = pantalla para dibujar, clic de la rueda = dejar flotando, E = soltar.
    /// En PC el rayo sale de la camara; en VR del controlador derecho (y lo secundario se lleva en la mano izquierda).
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField, Tooltip("Punto de agarre de la mano derecha.")]
        private Transform rightHoldPoint;
        [SerializeField, Tooltip("Punto de agarre de la mano izquierda.")]
        private Transform leftHoldPoint;

        [Header("VR References")]
        [SerializeField, Tooltip("[VR] Controlador derecho: origen del rayo y punto de agarre.")]
        private Transform vrRightController;
        [SerializeField, Tooltip("[VR] Controlador izquierdo.")]
        private Transform vrLeftController;
        [SerializeField, Tooltip("[VR] Rotacion (grados, sobre el eje X del controlador) para alinear el rayo con la mano.")]
        private float vrRayPitchOffset = 90f;
        [SerializeField, Tooltip("[VR] Material del puntero laser. Si falta se usa Sprites/Default.")]
        private Material vrPointerMaterial;

        [Header("Settings")]
        [SerializeField] private float interactRange = 3f;
        [SerializeField, Tooltip("Velocidad (m/s) con que sale lo que se arroja (Q).")]
        private float throwSpeed = 7f;
        [SerializeField] private LayerMask interactLayer = Physics.DefaultRaycastLayers;

        public const int HotbarSlots = 3;

        private static readonly RaycastHit[] hoverHits = new RaycastHit[24];

        private PlayerInputReader inputReader;
        private InkSupply inkSupply;
        private Rigidbody body;
        private ObjectMover mover;

        private IInteractable currentHovered;
        private IPressable hoveredPressable;
        private IFlippable hoveredFlippable;
        private IInkSource hoveredInkSource;
        private float hoverDistance;
        private bool wasClicking;
        private bool wasUsingTool;
        private LineRenderer vrPointerLine;

        // Mano principal.
        private readonly GameObject[] hotbar = new GameObject[HotbarSlots];
        private int activeSlot;
        private GameObject heldMain;
        private IUsable mainUsable;
        private bool lastMainRight = true;

        // Mano secundaria en VR (en PC lo arrastra ObjectMover).
        private GameObject handSecondary;
        private GameObject lastSecondary;

        /// <summary>Cambio en la barra de objetos o en lo agarrado: la HUD vuelve a dibujar.</summary>
        public event Action InventoryChanged;

        /// <summary>Texto de lo que se mira (compatibilidad; la HUD nueva solo muestra la tecla).</summary>
        public string HoverPrompt { get; private set; } = "";
        /// <summary>True si lo que se mira se aprieta con clic (botones del grimorio) y no con E.</summary>
        public bool HoverUsesClick => hoveredPressable != null && hoveredPressable.PressWithClick;
        /// <summary>True si lo que se mira se puede agarrar o apretar con E (la HUD muestra el simbolo [E]).</summary>
        public bool HoverInteractable => (hoveredPressable != null && !hoveredPressable.PressWithClick) || (hoveredPressable == null && currentHovered != null);
        /// <summary>Lo que se esta mirando y se puede agarrar o apretar (null si nada).</summary>
        public GameObject HoveredObject => currentHovered is MonoBehaviour mb ? mb.gameObject : hoveredPressable is MonoBehaviour pb ? pb.gameObject : null;

        /// <summary>Pistas junto a la mira ("E: Agarrar | V: Vista completa | R: Recargar tinta"), vacio si no hay nada que hacer.</summary>
        public string HoverHints { get; private set; } = "";

        // Superficie mirada que admite vista completa (V): un lienzo, o una parte de una superficie del ambiente.
        private DrawingCanvas viewCanvas;
        private bool viewSurface;
        private Vector3 viewPoint, viewNormal;

        private static readonly string[] HoverHintTable = BuildHoverHintTable();
        private IPressable hintPressable;
        private string hintPressableText = "";
        private string hintPressablePrompt;

        private static string[] BuildHoverHintTable()
        {
            // Mascara: 1 = agarrar, 2 = vista completa, 4 = recargar tinta.
            var table = new string[8];
            for (int mask = 0; mask < 8; mask++)
            {
                string text = "";
                if ((mask & 1) != 0) text += "E: Agarrar";
                if ((mask & 2) != 0) text += (text.Length > 0 ? " | " : "") + "Tab: Vista completa";
                if ((mask & 4) != 0) text += (text.Length > 0 ? " | " : "") + "R: Recargar tinta";
                table[mask] = text;
            }
            return table;
        }

        private string ComputeHoverHints()
        {
            if (hoveredPressable != null)
            {
                if (hoveredPressable.PressWithClick) return "";
                // Se rearma si cambia lo mirado o su texto (puerta: Abrir/Cerrar). Los textos son constantes cacheadas:
                // comparar referencias no aloca.
                string p = hoveredPressable.GetPressPrompt();
                if (!ReferenceEquals(hintPressable, hoveredPressable) || !ReferenceEquals(hintPressablePrompt, p))
                {
                    hintPressable = hoveredPressable;
                    hintPressablePrompt = p;
                    hintPressableText = string.IsNullOrEmpty(p) ? "E" : "E: " + p;
                }
                return hintPressableText;
            }
            hintPressable = null;
            int mask = 0;
            if (currentHovered != null) mask |= 1;
            if (viewCanvas != null || viewSurface) mask |= 2;
            if (hoveredInkSource != null && !(hoveredInkSource is InkSupply)) mask |= 4;
            if ((mask & 4) != 0 && hoveredInkSource is World.Inkwell well) return InkwellHint(mask, well);
            return HoverHintTable[mask];
        }

        // Pistas del tintero apuntado. Fijo: "Tintero fijo | R: Recargar tinta". Portatil: "<clase> | E: Guardar tintero".
        // Un string por (clase, fijo/portatil), armado la primera vez y reutilizado: sin allocations por frame.
        private readonly string[,] inkwellHints = new string[3, 2];

        private string InkwellHint(int mask, World.Inkwell well)
        {
            int k = (int)well.Kind;
            int f = well.Portable ? 1 : 0;
            string text = inkwellHints[k, f];
            if (text != null) return text;
            text = well.Portable
                ? SpellSimulator.Core.InkKindInfo.DisplayName(well.Kind) + " | E: Guardar tintero"
                : "Tintero fijo (" + SpellSimulator.Core.InkKindInfo.DisplayName(well.Kind) + ") | R: Recargar tinta";
            inkwellHints[k, f] = text;
            return text;
        }

        /// <summary>Decide si lo mirado admite vista completa: lienzos (no del grimorio) y superficies dibujables del ambiente (no el suelo).</summary>
        private void FindViewTarget(RaycastHit hit)
        {
            if (PlatformDetector.IsVR) return;
            Collider c = hit.collider;
            DrawingCanvas canvas = c.GetComponentInParent<DrawingCanvas>();
            if (canvas != null)
            {
                if (canvas.GetComponentInParent<Learning.Grimoire>() != null) return;
                viewCanvas = canvas;
                return;
            }
            if (c is TerrainCollider || hit.normal.y > 0.7f) return;
            if (c.GetComponentInParent<World.Inscribable>() == null) return;
            viewSurface = true;
            viewPoint = hit.point;
            viewNormal = hit.normal;
        }

        /// <summary>Objeto de un slot de la barra (null si esta vacio).</summary>
        public GameObject HotbarItem(int index) => index >= 0 && index < HotbarSlots ? hotbar[index] : null;
        public int ActiveHotbar => activeSlot;
        /// <summary>Lo que esta en la mano principal (null si ninguno).</summary>
        public GameObject MainItem => heldMain;
        /// <summary>Lo que se agarro con E (mano secundaria), arrastrado o en la mano en VR. Null si nada.</summary>
        public GameObject SecondaryItem => mover != null && mover.HeldBody != null ? mover.HeldBody.gameObject : handSecondary;

        private Transform MainHold => PlayerSettings.MainHandRight ? rightHoldPoint : leftHoldPoint;

        private void Awake()
        {
            inputReader = GetComponent<PlayerInputReader>();
            inkSupply = GetComponent<InkSupply>();
            body = GetComponent<Rigidbody>();
            mover = GetComponent<ObjectMover>();
        }

        private void Start()
        {
            if (!PlatformDetector.IsVR) return;
            if (vrRightController != null) rightHoldPoint = vrRightController;
            if (vrLeftController != null) leftHoldPoint = vrLeftController;
            if (vrRightController != null) vrPointerLine = CreatePointer(vrRightController.gameObject);
        }

        private LineRenderer CreatePointer(GameObject host)
        {
            var line = host.AddComponent<LineRenderer>();
            line.startWidth = 0.005f;
            line.endWidth = 0.005f;
            line.material = vrPointerMaterial != null ? vrPointerMaterial : new Material(Shader.Find("Sprites/Default"));
            line.startColor = Color.white;
            line.endColor = new Color(1f, 1f, 1f, 0f);
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void Update()
        {
            // Lo de la mano que ya no esta (destruido, desactivado) se suelta solo.
            if (heldMain != null && !heldMain.activeInHierarchy) DropMain(false);
            if (handSecondary != null && !handSecondary.activeInHierarchy) { handSecondary = null; Changed(); }
            FollowMainHandSetting();

            GameObject secondary = SecondaryItem;
            if (!ReferenceEquals(secondary, lastSecondary))
            {
                lastSecondary = secondary;
                Changed();
            }

            // Con el lector de libros abierto el juego no recibe clics, rueda ni teclas de mano.
            if (UI.BookReaderUI.IsOpen) return;

            // Esc: lector de libros -> vista completa -> grimorio (el lector ya cerro arriba; el grimorio ignora este Esc).
            if (FocusMode.CanvasView && inputReader.IsPressingEscapeRaw && Time.frameCount != UI.BookReaderUI.ClosedFrame)
            {
                ExitCanvasView();
                return;
            }

            // Vista completa de algo que no se agarro: solo se dibuja, se cambia de herramienta o se sale.
            if (SurfaceFocus.IsActive)
            {
                currentHovered = null;
                hoveredPressable = null;
                hoveredFlippable = null;
                hoveredInkSource = null;
                HoverHints = "";
                HandleHotbarKeys();
                HandleWheel();
                HandleUsage();
                if (inputReader.IsTogglingFocus || inputReader.IsFlipping) SurfaceFocus.Close();
                return;
            }

            HandleHotbarKeys();
            HandleHover();
            HandleInteraction();
            HandlePin();
            HandleClickPress();
            HandleThrow();
            HandleFlip();
            HandleWheel();
            HandlePageKeys();
            HandleUsage();
            if (inputReader.IsReloading) Reload();
            if (inputReader.IsTogglingFocus) ToggleFocus();
        }

        private void Changed() => InventoryChanged?.Invoke();

        // ---------------------------------------------------------------- mirar

        private void HandleHover()
        {
            bool vrRay = PlatformDetector.IsVR && vrRightController != null;
            Transform origin = vrRay ? vrRightController : cameraTransform;
            if (origin == null) return;

            Vector3 direction = vrRay ? Quaternion.AngleAxis(vrRayPitchOffset, origin.right) * origin.forward : origin.forward;
            Vector3 end = origin.position + direction * interactRange;

            currentHovered = null;
            hoveredPressable = null;
            hoveredInkSource = null;
            hoveredFlippable = null;
            viewCanvas = null;
            viewSurface = false;
            if (PickHit(origin.position, direction, out RaycastHit hit))
            {
                end = hit.point;
                hoverDistance = hit.distance;
                hoveredInkSource = hit.collider.GetComponentInParent<IInkSource>();
                hoveredPressable = hit.collider.GetComponentInParent<IPressable>();
                hoveredFlippable = hit.collider.GetComponentInParent<IFlippable>();
                var interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null) currentHovered = interactable;
                FindViewTarget(hit);
            }
            HoverPrompt = hoveredPressable != null ? (hoveredPressable.PressWithClick ? "" : hoveredPressable.GetPressPrompt())
                : currentHovered != null ? currentHovered.GetInteractionPrompt() : "";
            HoverHints = ComputeHoverHints();

            if (vrPointerLine != null)
            {
                vrPointerLine.SetPosition(0, origin.position);
                vrPointerLine.SetPosition(1, end);
            }
        }

        /// <summary>
        /// Primer golpe util del rayo: ignora el propio cuerpo, lo que se lleva en la mano (salvo botones) y los volumenes
        /// disparadores que no se pueden usar (agua, campos magicos).
        /// </summary>
        private bool PickHit(Vector3 origin, Vector3 direction, out RaycastHit best)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, hoverHits, interactRange, interactLayer, QueryTriggerInteraction.Collide);
            best = default;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hoverHits[i];
                if (hit.distance >= bestDistance) continue;
                Collider c = hit.collider;
                if (c.transform.IsChildOf(transform)) continue;
                bool pressable = c.GetComponentInParent<IPressable>() != null;
                bool held = IsHeld(c.transform);
                if (held && !pressable) continue;
                if (c.isTrigger && !pressable && c.GetComponentInParent<IInteractable>() == null &&
                    c.GetComponentInParent<IInkSource>() == null && c.GetComponentInParent<IFlippable>() == null) continue;
                best = hit;
                bestDistance = hit.distance;
            }
            return bestDistance < float.MaxValue;
        }

        private bool IsHeld(Transform t)
        {
            return (heldMain != null && t.IsChildOf(heldMain.transform)) || (handSecondary != null && t.IsChildOf(handSecondary.transform));
        }

        // ---------------------------------------------------------------- E

        private void HandleInteraction()
        {
            if (!inputReader.IsInteracting) return;

            // Otra E deja en el aire lo que se arrastra (para soltarlo en el piso: Q lo arroja).
            if (mover != null && mover.IsHolding) { mover.End(true); return; }
            if (handSecondary != null) { DropHandSecondary(); return; }

            if (hoveredPressable != null) { if (!hoveredPressable.PressWithClick) hoveredPressable.Press(); return; }
            if (currentHovered == null) return;

            // Tinteros: los portatiles se guardan como cartucho, los fijos no se levantan.
            if (currentHovered is World.Inkwell inkwell)
            {
                if (!inkwell.Portable) Hud.Notify("inkinfo", "Tintero fijo: apuntalo y apreta R para recargar", 2f);
                else if (!inkwell.TryPocket(inkSupply)) Hud.Notify("inkinfo", "Sin lugar para mas cartuchos", 2f);
                else { currentHovered = null; Hud.Notify("inkinfo", "Tintero guardado como cartucho", 2f); }
                return;
            }

            if (currentHovered is World.PhysicalTool || currentHovered is World.Stamp) PickUpToHotbar(currentHovered);
            else GrabSecondary(currentHovered);
        }

        /// <summary>Herramientas y estampas: al primer slot libre de la barra (o reemplazan a la equipada) y quedan en la mano.</summary>
        private void PickUpToHotbar(IInteractable interactable)
        {
            if (!(interactable is MonoBehaviour mb)) return;
            int slot = -1;
            for (int i = 0; i < HotbarSlots; i++) if (hotbar[i] == null) { slot = i; break; }
            if (slot < 0)
            {
                slot = activeSlot;
                if (hotbar[slot] != null) { SelectSlotInternal(slot); DropMain(false); }
            }

            StowMain();
            hotbar[slot] = mb.gameObject;
            activeSlot = slot;
            heldMain = mb.gameObject;
            mb.TryGetComponent(out mainUsable);
            interactable.OnInteract(MainHold);
            currentHovered = null;
            Changed();
        }

        /// <summary>Todo lo demas (lienzos, cajas, pesas, el grimorio): se arrastra con fisica en la mano secundaria.</summary>
        private void GrabSecondary(IInteractable interactable)
        {
            if (!(interactable is MonoBehaviour mb)) return;
            if (interactable is World.Movable movable && !movable.CanLift) return;

            if (PlatformDetector.IsVR || mover == null)
            {
                // VR: se lleva en la mano izquierda.
                handSecondary = mb.gameObject;
                interactable.OnInteract(PlatformDetector.IsVR && vrLeftController != null ? vrLeftController : leftHoldPoint);
                currentHovered = null;
                Changed();
                return;
            }
            var rb = mb.GetComponentInParent<Rigidbody>();
            if (rb == null) return;
            mover.TryBegin(rb, Mathf.Max(hoverDistance, 0.9f));
        }

        private void DropHandSecondary()
        {
            if (handSecondary != null && handSecondary.TryGetComponent(out IInteractable interactable)) interactable.OnDrop();
            handSecondary = null;
            Changed();
        }

        // ---------------------------------------------------------------- barra de objetos (mano principal)

        private void HandleHotbarKeys()
        {
            int key = inputReader.HotbarKey;
            if (key < 0 || key >= HotbarSlots) return;
            SelectSlotInternal(key);
        }

        private void SelectSlotInternal(int index)
        {
            if (index == activeSlot && (heldMain != null || hotbar[index] == null)) return;
            StowMain();
            activeSlot = index;
            GameObject next = hotbar[index];
            if (next != null)
            {
                next.SetActive(true);
                heldMain = next;
                next.TryGetComponent(out mainUsable);
            }
            Changed();
        }

        /// <summary>Esconde lo que hay en la mano principal (sigue en su slot).</summary>
        private void StowMain()
        {
            if (heldMain == null) return;
            if (mainUsable != null && wasUsingTool) mainUsable.OnUseEnd();
            heldMain.SetActive(false);
            heldMain = null;
            mainUsable = null;
            wasUsingTool = false;
        }

        /// <summary>Suelta lo de la mano principal y libera su slot. Con arrojar = true sale con velocidad.</summary>
        private void DropMain(bool throwIt)
        {
            if (heldMain == null) return;
            GameObject obj = heldMain;
            if (mainUsable != null && wasUsingTool) mainUsable.OnUseEnd();
            if (obj.TryGetComponent(out IInteractable interactable)) interactable.OnDrop();
            if (throwIt)
            {
                Transform view = PlatformDetector.IsVR && vrRightController != null ? vrRightController : cameraTransform;
                Vector3 velocity = view.forward * throwSpeed + Vector3.up * 1.2f + (body != null ? body.linearVelocity : Vector3.zero);
                if (obj.TryGetComponent(out IThrowable throwable)) throwable.Throw(velocity);
                else if (obj.TryGetComponent(out Rigidbody rb) && !rb.isKinematic) rb.linearVelocity = velocity;
            }
            if (hotbar[activeSlot] == obj) hotbar[activeSlot] = null;
            heldMain = null;
            mainUsable = null;
            wasUsingTool = false;
            Changed();
        }

        /// <summary>Si cambia la mano principal en las opciones, lo equipado pasa a la otra mano.</summary>
        private void FollowMainHandSetting()
        {
            bool right = PlayerSettings.MainHandRight;
            if (right == lastMainRight) return;
            lastMainRight = right;
            if (heldMain != null)
            {
                heldMain.transform.SetParent(MainHold, false);
                heldMain.transform.localPosition = Vector3.zero;
            }
        }

        // ---------------------------------------------------------------- otras acciones

        private void HandleClickPress()
        {
            bool clicking = inputReader.IsUsingTool;
            bool pressed = clicking && !wasClicking;
            wasClicking = clicking;
            if (!pressed || FocusMode.Active || hoveredPressable == null || !hoveredPressable.PressWithClick) return;
            hoveredPressable.Press();
        }

        private void HandleThrow()
        {
            if (!inputReader.IsThrowing) return;
            if (mover != null && mover.IsHolding)
            {
                Rigidbody thrown = mover.HeldBody;
                mover.End(false);
                if (thrown != null && !thrown.isKinematic) thrown.linearVelocity = cameraTransform.forward * throwSpeed + Vector3.up * 1.2f;
                return;
            }
            if (handSecondary != null)
            {
                GameObject obj = handSecondary;
                DropHandSecondary();
                if (obj.TryGetComponent(out IThrowable t)) t.Throw(cameraTransform.forward * throwSpeed + Vector3.up * 1.2f);
                return;
            }
            DropMain(true);
        }

        private void HandleFlip()
        {
            if (!inputReader.IsFlipping) return;
            // F saca la hoja de la vista completa y la da vuelta.
            if (mover != null && mover.IsHolding && mover.HeldBody != null && mover.HeldBody.TryGetComponent(out IFocusable focused) && focused.Focused)
                focused.SetFocus(cameraTransform, false);
            if (mover != null && mover.IsHolding) { mover.Flip(); return; }
            if (handSecondary != null && handSecondary.TryGetComponent(out IFlippable held)) { held.Flip(); return; }
            hoveredFlippable?.Flip();
        }

        /// <summary>Clic de la rueda: deja flotando (E tambien, ver HandleInteraction) lo que se arrastra.</summary>
        private void HandlePin()
        {
            if (!inputReader.IsPinning) return;
            if (mover != null && mover.IsHolding) { mover.End(true); return; }
            if (handSecondary != null && handSecondary.TryGetComponent(out IPinnable pinnable))
            {
                DropHandSecondary();
                pinnable.Pin();
            }
        }

        /// <summary>
        /// Rueda del mouse: cambia SOLO el grosor de la herramienta equipada (tambien con un cuaderno en la otra mano).
        /// Las paginas pasan con Z / X (HandlePageKeys). Arrastrando otra cosa, la rueda la acerca o aleja (ObjectMover).
        /// </summary>
        private void HandleWheel()
        {
            float scroll = inputReader.Scroll;
            if (scroll == 0f) return;
            var book = Learning.Grimoire.Instance;
            if (book != null && book.IsOpen) return;
            int step = scroll > 0f ? 1 : -1;

            // Arrastrando algo que no es un cuaderno la rueda es del movedor (distancia).
            if (mover != null && mover.IsHolding && !inputReader.IsRunning && !mover.HeldBody.TryGetComponent(out IPageable _)) return;
            if (heldMain != null && heldMain.TryGetComponent(out IWidthSelectable widths)) widths.CycleWidth(step);
        }

        /// <summary>Z (anterior) / X (siguiente): pasa la pagina del cuaderno agarrado o arrastrado (con el grimorio cerrado).</summary>
        private void HandlePageKeys()
        {
            int delta = inputReader.PageDelta;
            if (delta == 0) return;
            var book = Learning.Grimoire.Instance;
            if (book != null && book.IsOpen) return;
            IPageable pages = null;
            if (mover != null && mover.IsHolding) mover.HeldBody.TryGetComponent(out pages);
            if (pages == null && handSecondary != null) pages = handSecondary.GetComponentInParent<IPageable>();
            pages?.Turn(delta);
        }

        private void HandleUsage()
        {
            bool using_ = inputReader.IsUsingTool;
            if (mainUsable != null)
            {
                if (using_ && !wasUsingTool) mainUsable.OnUseStart();
                else if (using_) mainUsable.OnUseHold();
                else if (wasUsingTool) mainUsable.OnUseEnd();
                if (inputReader.IsSecondaryUsingTool) mainUsable.OnSecondaryUseStart();
            }
            wasUsingTool = using_ && mainUsable != null;
        }

        /// <summary>
        /// Recargar tinta de la herramienta equipada. Si se apunta un tintero fijo, la pluma toma su clase de tinta; si no,
        /// se gasta UN uso de cartucho: la pluma se llena y queda cargada con la clase del cartucho.
        /// </summary>
        public void Reload()
        {
            if (!(mainUsable is IInkUser user) || !user.UsesInk)
            {
                if (hoveredInkSource != null && !(hoveredInkSource is InkSupply)) Hud.Notify("inkinfo", "Equipá una herramienta con tinta para recargar", 2f);
                return;
            }

            if (hoveredInkSource != null && !(hoveredInkSource is InkSupply))
            {
                // Tintero apuntado: fijo = recarga (clase del tintero); portatil = hay que guardarlo con E.
                if (hoveredInkSource is World.Inkwell well && well.Portable)
                {
                    Hud.Notify("inkinfo", "Guardalo con E para tener sus cartuchos", 2f);
                    return;
                }
                if (user.Ink.Missing <= 0f && (!(hoveredInkSource is World.Inkwell same) || user.LoadedInk == same.Kind)) return;
                if (user.RefillFrom(hoveredInkSource) > 0f) Haptics.Pulse(HandType.Right, 0.3f, 0.08f);
                Hud.Notify("inkinfo", $"{SpellSimulator.Core.InkKindInfo.DisplayName(user.LoadedInk)} {user.Ink.Fraction:P0}", 2.5f);
                return;
            }

            if (inkSupply == null) return;
            var stock = inkSupply.Cartridges;
            if (!stock.TryUse(out SpellSimulator.Core.InkKind kind))
            {
                Hud.Notify("inkinfo", "Sin cartuchos de tinta", 2.5f);
                return;
            }
            user.LoadInk(kind);
            Haptics.Pulse(HandType.Right, 0.3f, 0.08f);
            Hud.Notify("inkinfo", $"{SpellSimulator.Core.InkKindInfo.DisplayName(kind)} | cartuchos {stock.CartridgeCount} ({stock.TotalUses} usos)", 2.5f);
        }

        /// <summary>V: fija el lienzo agarrado delante de la camara para dibujar sin sostener la mira.</summary>
        private void ToggleFocus()
        {
            if (PlatformDetector.IsVR) return;
            // Ya hay una vista completa: V la cierra.
            if (SurfaceFocus.IsActive) { SurfaceFocus.Close(); return; }
            GameObject target = SecondaryItem;
            if (target != null && target.TryGetComponent(out IFocusable focusable))
            {
                focusable.SetFocus(cameraTransform, !focusable.Focused);
                return;
            }
            if (FocusMode.CanvasView) return;
            // Sin nada agarrado: vista completa de lo que se mira.
            if (viewCanvas != null) SurfaceFocus.ShowCanvas(cameraTransform, viewCanvas);
            else if (viewSurface) SurfaceFocus.ShowRegion(cameraTransform, viewPoint, viewNormal);
        }

        /// <summary>Esc con la vista completa activa: la cierra (la de un lienzo agarrado o la de lo que se miraba).</summary>
        private void ExitCanvasView()
        {
            if (SurfaceFocus.IsActive) { SurfaceFocus.Close(); return; }
            GameObject target = SecondaryItem;
            if (target != null && target.TryGetComponent(out IFocusable focusable) && focusable.Focused)
                focusable.SetFocus(cameraTransform, false);
            else FocusMode.ExitCanvasView();
        }

        // ---------------------------------------------------------------- harness y tests

        /// <summary>Agarra un objeto como si se apuntara y apretara E (la usan el harness y los tests de Play).</summary>
        public void GrabItem(GameObject item)
        {
            if (item == null || !item.TryGetComponent(out IInteractable interactable)) return;
            if (interactable is World.PhysicalTool || interactable is World.Stamp) PickUpToHotbar(interactable);
            else GrabSecondary(interactable);
        }

        /// <summary>Elige un slot como si se apretara su tecla (0 a 2).</summary>
        public void SelectHotbar(int key) => SelectSlotInternal(Mathf.Clamp(key, 0, HotbarSlots - 1));
    }
}
