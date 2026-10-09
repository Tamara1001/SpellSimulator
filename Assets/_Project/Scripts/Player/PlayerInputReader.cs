using UnityEngine;
using UnityEngine.InputSystem;

namespace SpellSimulator.Player
{
    /// <summary>
    /// Lee el Input del jugador usando el New Input System desde codigo puro.
    /// Esto evita tener que configurar el .inputactions a mano por ahora, 
    /// y facilita la integracion para VR mas adelante.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        public Vector2 MoveValue { get; private set; }
        public Vector2 LookValue { get; private set; }
        public bool IsJumping { get; private set; }
        public bool IsInteracting { get; private set; }
        
        // Uso continuo (ej. mantener click izquierdo para dibujar)
        public bool IsUsingTool { get; private set; }
        
        // Uso secundario (click derecho / A: deshacer)
        public bool IsSecondaryUsingTool { get; private set; }

        // Recargar tinta (R / boton West). En VR se recarga mojando la punta en un tintero.
        public bool IsReloading { get; private set; }
        /// <summary>Reiniciar el mundo (modo Libre): Backspace, Select del gamepad, boton Y del control izquierdo.</summary>
        public bool IsResettingWorld { get; private set; }
        /// <summary>Abrir/cerrar el grimorio: Tab, Y del gamepad, boton de menu del control izquierdo.</summary>
        public bool IsTogglingBook { get; private set; }

        /// <summary>Correr: Shift, click del stick izquierdo (gamepad y VR).</summary>
        public bool IsRunning { get; private set; }
        /// <summary>Agacharse (mantener): Ctrl o C (toque), click del stick derecho.</summary>
        public bool IsCrouching { get; private set; }
        /// <summary>Arrojar lo que se tiene en la mano: Q, hombro izquierdo.</summary>
        public bool IsThrowing { get; private set; }
        /// <summary>Dar vuelta la hoja en la mano (o la que se mira): F, hombro derecho. En VR se gira con la mano.</summary>
        public bool IsFlipping { get; private set; }
        /// <summary>Fijar el lienzo en mano a la camara para dibujar comodo (alterna): V.</summary>
        public bool IsTogglingFocus { get; private set; }
        /// <summary>Pasar pagina (cuaderno, grimorio, lector): -1 / +1 (Z / X, flechas, cruceta). Valido solo el frame en que se aprieta.</summary>
        public int PageDelta { get; private set; }
        /// <summary>Pagina anterior (Z, flecha izquierda, cruceta izquierda) en este frame.</summary>
        public bool IsTurningPagePrev => PageDelta < 0;
        /// <summary>Pagina siguiente (X, flecha derecha, cruceta derecha) en este frame.</summary>
        public bool IsTurningPageNext => PageDelta > 0;
        /// <summary>Rueda del mouse (grosor del pincel; acerca/aleja lo que se sostiene). Ya no pasa paginas.</summary>
        public float Scroll { get; private set; }
        /// <summary>Escape apretado en este frame (abre/cierra el grimorio).</summary>
        public bool IsPressingEscape { get; private set; }
        /// <summary>Escape apretado, sin filtrar por la vista completa (la usa el interactor para cerrarla).</summary>
        public bool IsPressingEscapeRaw { get; private set; }

        /// <summary>Tumbarse / levantarse (alterna): mantener C ~0.35 s (o cruceta abajo). Para dibujar en el piso.</summary>
        public bool IsTogglingProne { get; private set; }
        /// <summary>Dejar lo que llevas flotando en el aire (en vez de soltarlo): G.</summary>
        public bool IsPinning { get; private set; }

        /// <summary>Zoom (mantener): Alt izquierdo, cruceta arriba.</summary>
        public bool IsZooming { get; private set; }
        /// <summary>Sin uso: mover a distancia ahora se hace con E sobre algo movible (ObjectMover).</summary>
        public bool IsRemoteGrabbing { get; private set; }
        /// <summary>Clic derecho mantenido (con G: rotar el objeto con el mouse en vez de la vista).</summary>
        public bool IsRotateHeld { get; private set; }
        /// <summary>Mostrar u ocultar el panel de depuracion: F3.</summary>
        public bool IsTogglingDebug { get; private set; }
        /// <summary>Fijar el grimorio abierto a la camara: B.</summary>
        public bool IsTogglingBookFocus { get; private set; }
        /// <summary>Slot de la barra de objetos: teclas 1 a 4 (0 a 3; 1-2 mano derecha, 3-4 mano izquierda). -1 si no se apreto ninguna.</summary>
        public int HotbarKey { get; private set; } = -1;
        private InputAction[] hotbarActions;

        /// <summary>Grosor de la pluma: teclas 5, 6, 7, 8, 9 y 0 (0 a 5). -1 si no se apreto ninguna.</summary>
        public int WidthPreset { get; private set; } = -1;

        private InputAction zoomAction;
        private InputAction remoteAction;
        private InputAction rotateAction;
        private InputAction debugAction;
        private InputAction bookFocusAction;
        private InputAction[] widthActions;
        private InputAction proneAction;
        private InputAction pinAction;
        private InputAction runAction;
        private InputAction crouchAction;
        private InputAction crouchKeyAction;
        private InputAction escapeAction;
        private const float ProneHoldSeconds = 0.35f;
        private float crouchKeyHeldAt = -1f;
        private InputAction throwAction;
        private InputAction flipAction;
        private InputAction focusAction;
        private InputAction pagePrevAction;
        private InputAction pageNextAction;
        private InputAction scrollAction;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction jumpAction;
        private InputAction interactAction;
        private InputAction useToolAction;
        private InputAction secondaryUseToolAction;
        private InputAction reloadAction;
        private InputAction resetWorldAction;
        private InputAction bookAction;

        private void Awake()
        {
            // Movimiento (WASD o Left Stick)
            moveAction = new InputAction("Move", InputActionType.Value);
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            moveAction.AddBinding("<Gamepad>/leftStick");
            moveAction.AddBinding("<XRController>{LeftHand}/thumbstick"); // VR: Joystick izquierdo

            // Vista (Mouse o Right Stick)
            lookAction = new InputAction("Look", InputActionType.Value);
            lookAction.AddBinding("<Mouse>/delta");
            lookAction.AddBinding("<Gamepad>/rightStick");

            // Salto (Space o South Button A/Cross)
            jumpAction = new InputAction("Jump", InputActionType.Button);
            jumpAction.AddBinding("<Keyboard>/space");
            jumpAction.AddBinding("<Gamepad>/buttonSouth");

            // Interactuar (Tecla E, Right Trigger de Gamepad, o Grip del controlador VR)
            interactAction = new InputAction("Interact", InputActionType.Button);
            interactAction.AddBinding("<Keyboard>/e");
            interactAction.AddBinding("<Gamepad>/rightTrigger");
            interactAction.AddBinding("<XRController>{RightHand}/grip"); // VR: apretar el puño para agarrar

            // Usar Herramienta (Click Izquierdo, Left Trigger de Gamepad, o Gatillo del controlador VR)
            // Definido como Value/Axis para que lea correctamente el recorrido del gatillo analógico
            useToolAction = new InputAction("UseTool", InputActionType.Value, "Axis");
            useToolAction.AddBinding("<Mouse>/leftButton");
            useToolAction.AddBinding("<Gamepad>/leftTrigger");
            useToolAction.AddBinding("<XRController>{RightHand}/trigger"); // VR: gatillo para dibujar

            // Uso Secundario (Click Derecho, Boton East de Gamepad, o Boton A del controlador VR)
            secondaryUseToolAction = new InputAction("SecondaryUseTool", InputActionType.Button);
            secondaryUseToolAction.AddBinding("<Mouse>/rightButton");
            secondaryUseToolAction.AddBinding("<Gamepad>/buttonEast");
            secondaryUseToolAction.AddBinding("<XRController>{RightHand}/primaryButton"); // VR: Boton A para castear/undo

            // Recargar tinta
            reloadAction = new InputAction("Reload", InputActionType.Button);
            reloadAction.AddBinding("<Keyboard>/r");
            reloadAction.AddBinding("<Gamepad>/buttonWest");

            resetWorldAction = new InputAction("ResetWorld", InputActionType.Button);
            resetWorldAction.AddBinding("<Keyboard>/backspace");
            resetWorldAction.AddBinding("<Gamepad>/select");
            resetWorldAction.AddBinding("<XRController>{LeftHand}/secondaryButton");

            proneAction = new InputAction("Prone", InputActionType.Button);
            proneAction.AddBinding("<Gamepad>/dpad/down");
            pinAction = new InputAction("Pin", InputActionType.Button);
                pinAction.AddBinding("<Mouse>/middleButton"); // clic de la rueda: dejar lo que se sostiene donde esta

            zoomAction = new InputAction("Zoom", InputActionType.Button);
            zoomAction.AddBinding("<Keyboard>/leftAlt");
            zoomAction.AddBinding("<Gamepad>/dpad/up");
            remoteAction = new InputAction("RemoteGrab", InputActionType.Button);
            rotateAction = new InputAction("RotateHeld", InputActionType.Button);
            rotateAction.AddBinding("<Mouse>/rightButton");
            debugAction = new InputAction("Debug", InputActionType.Button);
            debugAction.AddBinding("<Keyboard>/f3");
            bookFocusAction = new InputAction("BookFocus", InputActionType.Button);
            // B eliminada: el grimorio a pantalla completa ahora es Esc (lo maneja Grimoire).
            hotbarActions = new InputAction[3];
            for (int i = 0; i < hotbarActions.Length; i++)
            {
                hotbarActions[i] = new InputAction("Hotbar" + (i + 1), InputActionType.Button);
                hotbarActions[i].AddBinding("<Keyboard>/" + (i + 1));
            }
            string[] widthKeys = { "5", "6", "7", "8", "9", "0" };
            widthActions = new InputAction[widthKeys.Length];
            for (int i = 0; i < widthActions.Length; i++)
            {
                widthActions[i] = new InputAction("Width" + widthKeys[i], InputActionType.Button);
                widthActions[i].AddBinding("<Keyboard>/" + widthKeys[i]);
            }

            runAction = new InputAction("Run", InputActionType.Button);
            runAction.AddBinding("<Keyboard>/leftShift");
            runAction.AddBinding("<Gamepad>/leftStickPress");
            runAction.AddBinding("<XRController>{LeftHand}/thumbstickClicked");

            crouchAction = new InputAction("Crouch", InputActionType.Button);
            crouchAction.AddBinding("<Keyboard>/leftCtrl");
            crouchKeyAction = new InputAction("CrouchKey", InputActionType.Button);
            crouchKeyAction.AddBinding("<Keyboard>/c");
            escapeAction = new InputAction("Escape", InputActionType.Button);
            escapeAction.AddBinding("<Keyboard>/escape");
            crouchAction.AddBinding("<Gamepad>/rightStickPress");

            throwAction = new InputAction("Throw", InputActionType.Button);
            throwAction.AddBinding("<Keyboard>/q");
            throwAction.AddBinding("<Gamepad>/leftShoulder");
            throwAction.AddBinding("<XRController>{RightHand}/secondaryButton"); // VR: boton B

            flipAction = new InputAction("Flip", InputActionType.Button);
            flipAction.AddBinding("<Keyboard>/f");
            flipAction.AddBinding("<Gamepad>/rightShoulder");
            flipAction.AddBinding("<XRController>{RightHand}/thumbstickClicked"); // VR: click del stick derecho

            focusAction = new InputAction("Focus", InputActionType.Button);
            focusAction.AddBinding("<Keyboard>/tab");

            pagePrevAction = new InputAction("PagePrev", InputActionType.Button);
            pagePrevAction.AddBinding("<Keyboard>/z");
            pagePrevAction.AddBinding("<Keyboard>/leftArrow");
            pagePrevAction.AddBinding("<Gamepad>/dpad/left");
            pageNextAction = new InputAction("PageNext", InputActionType.Button);
            pageNextAction.AddBinding("<Keyboard>/x");
            pageNextAction.AddBinding("<Keyboard>/rightArrow");
            pageNextAction.AddBinding("<Gamepad>/dpad/right");

            scrollAction = new InputAction("Scroll", InputActionType.Value);
            scrollAction.AddBinding("<Mouse>/scroll/y");

            bookAction = new InputAction("Book", InputActionType.Button);
            bookAction.AddBinding("<Keyboard>/t");
            bookAction.AddBinding("<Gamepad>/buttonNorth");
            bookAction.AddBinding("<XRController>{LeftHand}/menuButton");
        }

        private void OnEnable()
        {
            reloadAction.Enable();
            runAction.Enable();
            proneAction.Enable();
            zoomAction.Enable();
            remoteAction.Enable();
            rotateAction.Enable();
            debugAction.Enable();
            bookFocusAction.Enable();
            foreach (var w in widthActions) w.Enable();
            foreach (var h in hotbarActions) h.Enable();
            pinAction.Enable();
            crouchAction.Enable();
            crouchKeyAction.Enable();
            escapeAction.Enable();
            throwAction.Enable();
            flipAction.Enable();
            focusAction.Enable();
            pagePrevAction.Enable();
            pageNextAction.Enable();
            scrollAction.Enable();
            resetWorldAction.Enable();
            bookAction.Enable();
            moveAction.Enable();
            lookAction.Enable();
            jumpAction.Enable();
            interactAction.Enable();
            useToolAction.Enable();
            secondaryUseToolAction.Enable();
        }

        private void OnDisable()
        {
            reloadAction.Disable();
            runAction.Disable();
            proneAction.Disable();
            zoomAction.Disable();
            remoteAction.Disable();
            rotateAction.Disable();
            debugAction.Disable();
            bookFocusAction.Disable();
            foreach (var w in widthActions) w.Disable();
            foreach (var h in hotbarActions) h.Disable();
            pinAction.Disable();
            crouchAction.Disable();
            crouchKeyAction.Disable();
            escapeAction.Disable();
            crouchKeyHeldAt = -1f;
            throwAction.Disable();
            flipAction.Disable();
            focusAction.Disable();
            pagePrevAction.Disable();
            pageNextAction.Disable();
            scrollAction.Disable();
            resetWorldAction.Disable();
            bookAction.Disable();
            moveAction.Disable();
            lookAction.Disable();
            jumpAction.Disable();
            interactAction.Disable();
            useToolAction.Disable();
            secondaryUseToolAction.Disable();
        }

        private void Update()
        {
            MoveValue = moveAction.ReadValue<Vector2>();
            LookValue = lookAction.ReadValue<Vector2>();
            
            // triggered = True solo en el frame que se aprieta
            IsJumping = jumpAction.triggered;
            IsInteracting = interactAction.triggered;
            
            // Leer el valor como float > 0.1f garantiza que funcione tanto para botones de mouse como para gatillos VR
            IsUsingTool = useToolAction.ReadValue<float>() > 0.1f; 
            IsSecondaryUsingTool = secondaryUseToolAction.triggered; // Un solo click (deshacer)
            IsReloading = reloadAction.triggered;
            IsResettingWorld = resetWorldAction.triggered;
            IsTogglingBook = bookAction.triggered;
            IsRunning = runAction.IsPressed();
            IsTogglingProne = proneAction.triggered;
            IsPressingEscapeRaw = escapeAction.triggered;
            // Con la vista completa activa Esc la cierra (lo hace el interactor) y NO abre el grimorio.
            IsPressingEscape = IsPressingEscapeRaw && !FocusMode.CanvasView && FocusMode.CanvasViewExitFrame != Time.frameCount;
            IsZooming = zoomAction.IsPressed();
            IsRemoteGrabbing = remoteAction.IsPressed();
            IsRotateHeld = rotateAction.IsPressed();
            IsTogglingDebug = debugAction.triggered;
            IsTogglingBookFocus = bookFocusAction.triggered;
            WidthPreset = -1;
            for (int i = 0; i < widthActions.Length; i++) if (widthActions[i].triggered) WidthPreset = i;
            HotbarKey = -1;
            for (int i = 0; i < hotbarActions.Length; i++) if (hotbarActions[i].triggered) HotbarKey = i;
            IsPinning = pinAction.triggered;
            // C: toque = agacharse (mientras se mantiene); mantener ~0.35 s = tumbarse / levantarse (una vez por pulsacion).
            bool cDown = crouchKeyAction.IsPressed();
            if (cDown)
            {
                if (crouchKeyHeldAt < 0f) crouchKeyHeldAt = Time.unscaledTime;
                else if (crouchKeyHeldAt < float.MaxValue && Time.unscaledTime - crouchKeyHeldAt >= ProneHoldSeconds)
                {
                    IsTogglingProne = true;
                    crouchKeyHeldAt = float.MaxValue; // ya conmuto: no repite hasta soltar
                }
            }
            else crouchKeyHeldAt = -1f;
            IsCrouching = crouchAction.IsPressed() || cDown;
            IsThrowing = throwAction.triggered;
            IsFlipping = flipAction.triggered;
            IsTogglingFocus = focusAction.triggered;
            PageDelta = (pageNextAction.triggered ? 1 : 0) - (pagePrevAction.triggered ? 1 : 0);
            Scroll = scrollAction.ReadValue<float>();
            if (Gate != InputGate.Full) ApplyGate();
        }

        /// <summary>
        /// Quien puede usar la entrada: todo el juego (Full), solo el grimorio (BookOnly: Tab/Esc, paginas y clic; lo usa
        /// la opcion Informacion del menu principal) o nadie (None: menu principal en pantalla). Lo maneja MainMenu.
        /// </summary>
        public InputGate Gate { get; set; } = InputGate.Full;

        private void ApplyGate()
        {
            bool book = Gate == InputGate.BookOnly;
            MoveValue = Vector2.zero;
            LookValue = Vector2.zero;
            IsJumping = IsInteracting = IsSecondaryUsingTool = IsReloading = IsResettingWorld = false;
            IsRunning = IsTogglingProne = IsZooming = IsRemoteGrabbing = IsRotateHeld = IsTogglingDebug = false;
            IsTogglingBookFocus = IsPinning = IsCrouching = IsThrowing = IsFlipping = IsTogglingFocus = false;
            WidthPreset = -1;
            HotbarKey = -1;
            Scroll = 0f;
            if (book) return;
            IsUsingTool = IsTogglingBook = IsPressingEscape = IsPressingEscapeRaw = false;
            PageDelta = 0;
        }
    }

    /// <summary>Compuerta de la entrada del jugador (ver <see cref="PlayerInputReader.Gate"/>).</summary>
    public enum InputGate { Full, BookOnly, None }
}
