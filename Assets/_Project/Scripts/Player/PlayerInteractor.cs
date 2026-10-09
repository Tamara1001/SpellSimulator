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
    /// Partes (F0 de MULTIPLAYER_PLAN): este archivo = ENTRADA (teclas, clics, rueda); <c>PlayerInteractor.Hints.cs</c> = mirar
    /// (rayo, pistas de la HUD, vista completa); <c>PlayerInteractor.Grab.cs</c> = pedidos de agarre/soltar/arrojar y efectos de
    /// escena de las manos. El ESTADO del inventario vive en <see cref="PlayerInventory"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    public partial class PlayerInteractor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform cameraTransform;
        // Ojo: LocalPlayerContext lee rightHoldPoint/leftHoldPoint por reflexion; no renombrar.
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

        /// <summary>Slots de la barra (teclas 1-3), con o sin cinturon.</summary>
        public const int HotbarSlots = HotbarMath.BaseSlots;
        // Alias obsoletos (valen 3): la HUD los usaba para los slots del cinturon.
        public const int BaseHotbarSlots = HotbarSlots;
        public const int MaxHotbarSlots = HotbarSlots;

        private PlayerInputReader inputReader;
        private InkSupply inkSupply;
        private Rigidbody body;
        private ObjectMover mover;

        private bool wasClicking;
        private bool wasUsingTool;
        private LineRenderer vrPointerLine;

        // Estado del inventario (barra, manos, cinturon). Lo de la mano principal que se USA se cachea aca (componente de escena).
        private readonly PlayerInventory inventory = new PlayerInventory();
        private IUsable mainUsable;

        /// <summary>Cambio en la barra de objetos o en lo agarrado: la HUD vuelve a dibujar.</summary>
        public event Action InventoryChanged;

        /// <summary>Estado del inventario (sin escena). Para leerlo; los cambios pasan por el interactor.</summary>
        public PlayerInventory Inventory => inventory;

        /// <summary>Objeto de un slot de la barra (null si esta vacio).</summary>
        public GameObject HotbarItem(int index) => inventory.HotbarItem(index);
        /// <summary>True con el cinturon equipado (una clase de tinta por slot de cartuchos).</summary>
        public bool HasBelt => inventory.HasBelt;
        /// <summary>Slots de la barra: siempre 3.</summary>
        public int SlotCount => inventory.SlotCount;

        /// <summary>Equipa o quita el cinturon (permite llevar cartuchos de varias clases de tinta; no cambia la barra).</summary>
        public void SetBelt(bool equipped)
        {
            if (inventory.HasBelt == equipped) return;
            inventory.SetBelt(equipped);
            if (inkSupply != null) inkSupply.Cartridges.SetBelt(equipped);
        }
        public int ActiveHotbar => inventory.ActiveSlot;
        /// <summary>Lo que esta en la mano principal (null si ninguno).</summary>
        public GameObject MainItem => inventory.HeldMain;
        /// <summary>Lo que se agarro con E (mano secundaria), arrastrado o en la mano en VR. Null si nada.</summary>
        public GameObject SecondaryItem => inventory.Secondary;

        private Transform MainHold => PlayerSettings.MainHandRight ? rightHoldPoint : leftHoldPoint;
        private bool IsDragging => mover != null && mover.IsHolding;

        private void Awake()
        {
            inputReader = GetComponent<PlayerInputReader>();
            inkSupply = GetComponent<InkSupply>();
            if (inkSupply != null) inkSupply.Cartridges.Changed += RefreshInkCycleHints;
            RefreshInkCycleHints();
            body = GetComponent<Rigidbody>();
            mover = GetComponent<ObjectMover>();
            inventory.DraggedQuery = DraggedObject;
            inventory.Changed += Changed;
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
            if (inventory.HeldMain != null && !inventory.HeldMain.activeInHierarchy) DropMain(false, Vector3.zero);
            GameObject hand = inventory.HandSecondary;
            if (hand != null && !hand.activeInHierarchy)
            {
                World.Holdable.Mark(hand, World.Holdable.NoOwner);
                inventory.ClearHandSecondary();
            }
            FollowMainHandSetting();
            inventory.PollSecondary();

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
                ClearHover();
                HandleHotbarKeys();
                HandleWheel();
                HandleUsage();
                if (inputReader.IsReloading) Reload();
                HandleInkCycle();
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
            HandleInkCycle();
            if (inputReader.IsTogglingFocus) ToggleFocus();
        }

        private void Changed() => InventoryChanged?.Invoke();

        private void OnDestroy()
        {
            if (inkSupply != null) inkSupply.Cartridges.Changed -= RefreshInkCycleHints;
            inventory.Changed -= Changed;
        }

        // ---------------------------------------------------------------- entrada: E

        private void HandleInteraction()
        {
            if (!inputReader.IsInteracting) return;

            // Otra E deja en el aire lo que se arrastra (para soltarlo en el piso: Q lo arroja).
            if (IsDragging) { Release(LocalOwnerId, true); return; }
            if (inventory.HandSecondary != null) { Release(LocalOwnerId, false); return; }

            if (hoveredPressable != null) { if (!hoveredPressable.PressWithClick) hoveredPressable.Press(); return; }
            if (currentHovered == null) return;

            // Tinteros: los portatiles se guardan como cartucho, los fijos no se levantan.
            if (currentHovered is World.Inkwell inkwell)
            {
                if (!inkwell.Portable) Hud.Notify("inkinfo", "Tintero fijo: apuntalo y apreta R para recargar", 2f);
                else if (inkSupply != null && !inkSupply.Cartridges.CanCarry(inkwell.Kind))
                    Hud.Notify("inkinfo", "Necesitas el cinturon para llevar tintas de distintas clases", 3f);
                else if (!inkwell.TryPocket(inkSupply)) Hud.Notify("inkinfo", "Sin lugar para mas cartuchos", 2f);
                else { currentHovered = null; Hud.Notify("inkinfo", "Tintero guardado como cartucho", 2f); }
                return;
            }

            // Cinturon: se equipa y permite llevar cartuchos de las tres clases de tinta.
            if (currentHovered is World.Belt belt)
            {
                if (HasBelt) Hud.Notify("belt", "Ya llevas un cinturon", 2f);
                else
                {
                    SetBelt(true);
                    Hud.Notify("belt", "Cinturon equipado: guarda una clase de tinta por slot (V cambia la activa)", 3f);
                    currentHovered = null;
                    Destroy(belt.gameObject);
                }
                return;
            }

            if (currentHovered is MonoBehaviour target) TryGrab(new GrabRequest(LocalOwnerId, target.gameObject, hoverDistance));
        }

        // ---------------------------------------------------------------- entrada: barra de objetos (mano principal)

        private void HandleHotbarKeys()
        {
            int key = inputReader.HotbarKey;
            key = HotbarMath.ClampKey(key, SlotCount);
            if (key < 0) return;
            SelectSlotInternal(key);
        }

        // ---------------------------------------------------------------- entrada: otras acciones

        private void HandleClickPress()
        {
            bool clicking = inputReader.IsUsingTool;
            bool pressed = clicking && !wasClicking;
            wasClicking = clicking;
            if (!pressed || FocusMode.Active || hoveredPressable == null || !hoveredPressable.PressWithClick) return;
            hoveredPressable.Press();
        }

        /// <summary>Q: arroja lo arrastrado, lo de la mano secundaria (VR) o lo de la mano principal.</summary>
        private void HandleThrow()
        {
            if (!inputReader.IsThrowing) return;
            Vector3 velocity = IsDragging || inventory.HandSecondary != null
                ? cameraTransform.forward * throwSpeed + Vector3.up * 1.2f
                : MainThrowVelocity();
            Throw(LocalOwnerId, velocity);
        }

        private void HandleFlip()
        {
            if (!inputReader.IsFlipping) return;
            // F saca la hoja de la vista completa y la da vuelta.
            if (IsDragging && mover.HeldBody != null && mover.HeldBody.TryGetComponent(out IFocusable focused) && focused.Focused)
                focused.SetFocus(cameraTransform, false);
            if (IsDragging) { mover.Flip(); return; }
            GameObject hand = inventory.HandSecondary;
            if (hand != null && hand.TryGetComponent(out IFlippable held)) { held.Flip(); return; }
            hoveredFlippable?.Flip();
        }

        /// <summary>Clic de la rueda: deja flotando (E tambien, ver HandleInteraction) lo que se arrastra.</summary>
        private void HandlePin()
        {
            if (!inputReader.IsPinning) return;
            if (IsDragging) { Release(LocalOwnerId, true); return; }
            GameObject hand = inventory.HandSecondary;
            if (hand != null && hand.TryGetComponent(out IPinnable _)) Release(LocalOwnerId, true);
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

            // Arrastrando cualquier cosa (tambien un cuaderno) la rueda es del movedor (distancia), salvo en vista completa,
            // donde la distancia no tiene sentido y la rueda vuelve a ser el grosor de la herramienta.
            if (IsDragging && !inputReader.IsRunning && !FocusMode.CanvasView) return;
            GameObject main = inventory.HeldMain;
            if (main != null && main.TryGetComponent(out IWidthSelectable widths)) widths.CycleWidth(step);
        }

        /// <summary>Z (anterior) / X (siguiente): pasa la pagina del cuaderno agarrado o arrastrado (con el grimorio cerrado).</summary>
        private void HandlePageKeys()
        {
            int delta = inputReader.PageDelta;
            if (delta == 0) return;
            var book = Learning.Grimoire.Instance;
            if (book != null && book.IsOpen) return;
            IPageable pages = null;
            if (IsDragging) mover.HeldBody.TryGetComponent(out pages);
            GameObject hand = inventory.HandSecondary;
            if (pages == null && hand != null) pages = hand.GetComponentInParent<IPageable>();
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
            Hud.Notify("inkinfo", $"{SpellSimulator.Core.InkKindInfo.DisplayName(kind)} | cartuchos {stock.CartridgeCount(kind)} ({stock.TotalUses(kind)} usos)", 2.5f);
        }

        /// <summary>V (solo PC): cicla la clase de tinta activa entre las que tienen cartuchos (hace falta el cinturon).</summary>
        private void HandleInkCycle()
        {
            if (!inputReader.IsCyclingInk || inkSupply == null) return;
            var stock = inkSupply.Cartridges;
            if (!stock.CycleActive())
            {
                Hud.Notify("inkinfo", stock.TotalCartridges == 0 ? "Sin cartuchos de tinta" : "No hay otra clase de tinta para cambiar", 2f);
                return;
            }
            SpellSimulator.Core.InkKind kind = stock.ActiveKind;
            Hud.Notify("inkinfo", $"Tinta activa: {SpellSimulator.Core.InkKindInfo.DisplayName(kind)} ({stock.CartridgeCount(kind)} cartuchos)", 2f);
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
            if (item == null) return;
            TryGrab(new GrabRequest(LocalOwnerId, item, hoverDistance));
        }

        /// <summary>Elige un slot como si se apretara su tecla (0 a 2).</summary>
        public void SelectHotbar(int key) => SelectSlotInternal(Mathf.Clamp(key, 0, SlotCount - 1));
    }
}
