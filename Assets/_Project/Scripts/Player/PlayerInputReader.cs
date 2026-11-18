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
        /// <summary>Ciclar la clase de tinta activa (V; solo PC). Vale el frame en que se aprieta.</summary>
        public bool IsCyclingInk { get; private set; }
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

        // ---------------------------------------------------------------- VR (Touch): una entrada por mano
        /// <summary>Segundos que hay que mantener Y (control izquierdo) para reiniciar el mundo; un toque corto cambia la tinta.</summary>
        public const float VrResetHoldSeconds = 1.5f;
        private const float VrInkTapSeconds = 0.4f;
        private const float VrFlickOn = 0.75f;
        private const float VrFlickOff = 0.4f;

        /// <summary>[VR] True si la mano principal es la derecha: su gatillo usa la herramienta y su A/X deshace. Lo fija el interactor.</summary>
        public bool VrMainRight { get; set; } = true;
        /// <summary>[VR] Grip de cada mano: se apreta este frame / se suelta este frame / esta apretado.</summary>
        public bool VrGripDownLeft { get; private set; }
        public bool VrGripDownRight { get; private set; }
        public bool VrGripUpLeft { get; private set; }
        public bool VrGripUpRight { get; private set; }
        public bool VrGripHeldLeft { get; private set; }
        public bool VrGripHeldRight { get; private set; }
        /// <summary>[VR] Recorrido del gatillo de cada mano (0 a 1) y si se paso del medio este frame.</summary>
        public float VrTriggerLeft { get; private set; }
        public float VrTriggerRight { get; private set; }
        public bool VrTriggerDownLeft { get; private set; }
        public bool VrTriggerDownRight { get; private set; }
        /// <summary>[VR] Golpe del stick derecho hacia los costados (-1 / +1): gira a saltos o pasa la pagina. Solo el frame del golpe.</summary>
        public int VrFlickX { get; private set; }
        /// <summary>[VR] Golpe del stick derecho hacia arriba (+1) o abajo (-1): cambia de slot de la barra. Solo el frame del golpe.</summary>
        public int VrSlotDelta { get; private set; }
        /// <summary>[VR] Golpe del stick derecho hacia arriba (+1) o abajo (-1): cambia el grosor de la herramienta. Solo el frame del golpe.</summary>
        public int VrWidthDelta { get; private set; }

        private InputAction bButtonAction, crouchToggleAction;
        private float bDownAt = -1f;
        private bool bHoldDone;
        private bool vrCrouchLatched;
        private const float VrThrowToolHoldSeconds = 0.6f;
        private const float VrTapSeconds = 0.4f;

        private InputAction gripLeftAction, gripRightAction, triggerLeftAction, triggerRightAction;
        private InputAction primaryLeftAction, primaryRightAction, yButtonAction, stickRightAction;
        private float prevTriggerLeft, prevTriggerRight;
        private float yDownAt = -1f;
        private bool yHoldDone;
        private bool stickArmed = true;

        /// <summary>[VR] Grip apretado de esa mano.</summary>
        public bool VrGripHeld(bool right) => right ? VrGripHeldRight : VrGripHeldLeft;
        /// <summary>[VR] Recorrido del gatillo de esa mano.</summary>
        public float VrTrigger(bool right) => right ? VrTriggerRight : VrTriggerLeft;

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
        private InputAction inkCycleAction;
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
            // VR: el grip de cada mano se lee aparte (gripLeftAction / gripRightAction) y se suma a IsInteracting en Update.

            // Usar Herramienta (Click Izquierdo, Left Trigger de Gamepad, o Gatillo del controlador VR)
            // Definido como Value/Axis para que lea correctamente el recorrido del gatillo analógico
            useToolAction = new InputAction("UseTool", InputActionType.Value, "Axis");
            useToolAction.AddBinding("<Mouse>/leftButton");
            useToolAction.AddBinding("<Gamepad>/leftTrigger");
            // VR: el gatillo de la mano principal se suma a IsUsingTool en Update (triggerLeftAction / triggerRightAction).

            // Uso Secundario (Click Derecho, Boton East de Gamepad, o Boton A del controlador VR)
            secondaryUseToolAction = new InputAction("SecondaryUseTool", InputActionType.Button);
            secondaryUseToolAction.AddBinding("<Mouse>/rightButton");
            secondaryUseToolAction.AddBinding("<Gamepad>/buttonEast");
            // VR: A (derecha) o X (izquierda) de la mano principal se suma a IsSecondaryUsingTool en Update (deshacer).

            // Recargar tinta
            reloadAction = new InputAction("Reload", InputActionType.Button);
            reloadAction.AddBinding("<Keyboard>/r");
            reloadAction.AddBinding("<Gamepad>/buttonWest");
            inkCycleAction = new InputAction("InkCycle", InputActionType.Button);
            inkCycleAction.AddBinding("<Keyboard>/v");

            resetWorldAction = new InputAction("ResetWorld", InputActionType.Button);
            resetWorldAction.AddBinding("<Keyboard>/backspace");
            resetWorldAction.AddBinding("<Gamepad>/select");
            // VR: Y (izquierda) se lee en Update (toque = cambiar tinta, mantener = reiniciar el mundo).

            gripLeftAction = new InputAction("GripL", InputActionType.Button);
            gripLeftAction.AddBinding("<XRController>{LeftHand}/grip");
            gripRightAction = new InputAction("GripR", InputActionType.Button);
            gripRightAction.AddBinding("<XRController>{RightHand}/grip");
            triggerLeftAction = new InputAction("TriggerL", InputActionType.Value, expectedControlType: "Axis");
            triggerLeftAction.AddBinding("<XRController>{LeftHand}/trigger");
            triggerRightAction = new InputAction("TriggerR", InputActionType.Value, expectedControlType: "Axis");
            triggerRightAction.AddBinding("<XRController>{RightHand}/trigger");
            primaryLeftAction = new InputAction("PrimaryL", InputActionType.Button);
            primaryLeftAction.AddBinding("<XRController>{LeftHand}/primaryButton");
            primaryRightAction = new InputAction("PrimaryR", InputActionType.Button);
            primaryRightAction.AddBinding("<XRController>{RightHand}/primaryButton");
            yButtonAction = new InputAction("YButton", InputActionType.Button);
            yButtonAction.AddBinding("<XRController>{LeftHand}/secondaryButton");
            bButtonAction = new InputAction("BButton", InputActionType.Button);
            bButtonAction.AddBinding("<XRController>{RightHand}/secondaryButton");
            crouchToggleAction = new InputAction("CrouchToggle", InputActionType.Button);
            crouchToggleAction.AddBinding("<XRController>{LeftHand}/thumbstickClicked");
            stickRightAction = new InputAction("StickR", InputActionType.Value, expectedControlType: "Vector2");
            stickRightAction.AddBinding("<XRController>{RightHand}/thumbstick");

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
            // VR: sin correr (mas comodo); el click del stick izquierdo alterna agacharse (crouchToggleAction).

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
            // VR: B se lee en Update (toque = slot siguiente, mantener = arrojar la herramienta).

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
            // VR: en el layout de los Touch (OpenXR) el boton de menu se llama "menu" (uso MenuButton); "menuButton" no existe como nombre.
            bookAction.AddBinding("<XRController>{LeftHand}/menu");
            bookAction.AddBinding("<XRController>{LeftHand}/{MenuButton}");
        }

        private void OnEnable()
        {
            gripLeftAction.Enable(); gripRightAction.Enable();
            triggerLeftAction.Enable(); triggerRightAction.Enable();
            primaryLeftAction.Enable(); primaryRightAction.Enable();
            yButtonAction.Enable(); stickRightAction.Enable();
            bButtonAction.Enable(); crouchToggleAction.Enable();
            reloadAction.Enable();
            inkCycleAction.Enable();
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
            gripLeftAction.Disable(); gripRightAction.Disable();
            triggerLeftAction.Disable(); triggerRightAction.Disable();
            primaryLeftAction.Disable(); primaryRightAction.Disable();
            yButtonAction.Disable(); stickRightAction.Disable();
            bButtonAction.Disable(); crouchToggleAction.Disable();
            reloadAction.Disable();
            inkCycleAction.Disable();
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
            IsCyclingInk = inkCycleAction.triggered;
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
            ReadVr();
            if (Gate != InputGate.Full) ApplyGate();
        }

        /// <summary>
        /// [VR] Lee los controles Touch y los suma a las acciones de siempre: grip de cualquier mano = interactuar, gatillo de la mano
        /// principal = usar, A/X de la mano principal = deshacer, Y (toque) = cambiar tinta, Y (mantener) = reiniciar, stick derecho
        /// (golpe) = girar / pasar pagina / cambiar de slot. Sin control conectado todo da cero y no cambia nada de PC.
        /// </summary>
        private void ReadVr()
        {
            VrGripDownLeft = gripLeftAction.WasPressedThisFrame();
            VrGripDownRight = gripRightAction.WasPressedThisFrame();
            VrGripUpLeft = gripLeftAction.WasReleasedThisFrame();
            VrGripUpRight = gripRightAction.WasReleasedThisFrame();
            VrGripHeldLeft = gripLeftAction.IsPressed();
            VrGripHeldRight = gripRightAction.IsPressed();
            if (VrGripDownLeft || VrGripDownRight) IsInteracting = true;

            VrTriggerLeft = triggerLeftAction.ReadValue<float>();
            VrTriggerRight = triggerRightAction.ReadValue<float>();
            VrTriggerDownLeft = VrTriggerLeft > 0.5f && prevTriggerLeft <= 0.5f;
            VrTriggerDownRight = VrTriggerRight > 0.5f && prevTriggerRight <= 0.5f;
            prevTriggerLeft = VrTriggerLeft;
            prevTriggerRight = VrTriggerRight;
            if (VrTrigger(VrMainRight) > 0.1f) IsUsingTool = true;
            if (VrMainRight ? primaryRightAction.WasPressedThisFrame() : primaryLeftAction.WasPressedThisFrame()) IsSecondaryUsingTool = true;

            if (yButtonAction.WasPressedThisFrame()) { yDownAt = Time.unscaledTime; yHoldDone = false; }
            else if (yDownAt >= 0f && !yHoldDone && yButtonAction.IsPressed() && Time.unscaledTime - yDownAt >= VrResetHoldSeconds)
            {
                IsResettingWorld = true;
                yHoldDone = true;
            }
            if (yButtonAction.WasReleasedThisFrame())
            {
                if (!yHoldDone && yDownAt >= 0f && Time.unscaledTime - yDownAt < VrInkTapSeconds) IsCyclingInk = true;
                yDownAt = -1f;
            }

            // Salto: A o X de la mano que NO lleva la herramienta (la otra deshace). Agacharse: click del stick izquierdo (alterna).
            if (VrMainRight ? primaryLeftAction.WasPressedThisFrame() : primaryRightAction.WasPressedThisFrame()) IsJumping = true;
            if (crouchToggleAction.WasPressedThisFrame()) vrCrouchLatched = !vrCrouchLatched;
            if (vrCrouchLatched) IsCrouching = true;

            // B: toque = slot siguiente, mantener = arrojar la herramienta.
            VrSlotDelta = 0;
            if (bButtonAction.WasPressedThisFrame()) { bDownAt = Time.unscaledTime; bHoldDone = false; }
            else if (bDownAt >= 0f && !bHoldDone && bButtonAction.IsPressed() && Time.unscaledTime - bDownAt >= VrThrowToolHoldSeconds)
            {
                IsThrowing = true;
                bHoldDone = true;
            }
            if (bButtonAction.WasReleasedThisFrame())
            {
                if (!bHoldDone && bDownAt >= 0f && Time.unscaledTime - bDownAt < VrTapSeconds) VrSlotDelta = 1;
                bDownAt = -1f;
            }

            VrFlickX = 0;
            VrWidthDelta = 0;
            Vector2 stick = stickRightAction.ReadValue<Vector2>();
            float mag = stick.magnitude;
            if (!stickArmed) { if (mag < VrFlickOff) stickArmed = true; }
            else if (mag > VrFlickOn)
            {
                stickArmed = false;
                if (Mathf.Abs(stick.x) > Mathf.Abs(stick.y)) VrFlickX = stick.x > 0f ? 1 : -1;
                else VrWidthDelta = stick.y > 0f ? 1 : -1;
            }
        }

        private void ClearVrFrame()
        {
            VrGripDownLeft = VrGripDownRight = VrGripUpLeft = VrGripUpRight = false;
            VrGripHeldLeft = VrGripHeldRight = false;
            VrTriggerDownLeft = VrTriggerDownRight = false;
            VrFlickX = 0;
            VrSlotDelta = 0;
            VrWidthDelta = 0;
        }

        /// <summary>
        /// Quien puede usar la entrada: todo el juego (Full), solo el grimorio (BookOnly: Tab/Esc, paginas y clic; lo usa
        /// la opcion Informacion del menu principal) o nadie (None: menu principal en pantalla). Lo maneja MainMenu.
        /// </summary>
        public InputGate Gate { get; set; } = InputGate.Full;

        private void ApplyGate()
        {
            bool book = Gate == InputGate.BookOnly;
            bool typing = Gate == InputGate.Typing;
            MoveValue = Vector2.zero;
            LookValue = Vector2.zero;
            ClearVrFrame();
            IsJumping = IsInteracting = IsSecondaryUsingTool = IsReloading = IsCyclingInk = IsResettingWorld = false;
            IsRunning = IsTogglingProne = IsZooming = IsRemoteGrabbing = IsRotateHeld = IsTogglingDebug = false;
            IsTogglingBookFocus = IsPinning = IsCrouching = IsThrowing = IsFlipping = IsTogglingFocus = false;
            WidthPreset = -1;
            HotbarKey = -1;
            Scroll = 0f;
            if (typing)
            {
                // Escribiendo un nombre: el teclado es del texto. Solo quedan el clic (botones del libro) y Esc "crudo" (cancelar).
                IsTogglingBook = IsPressingEscape = false;
                PageDelta = 0;
                return;
            }
            if (book) return;
            IsUsingTool = IsTogglingBook = IsPressingEscape = IsPressingEscapeRaw = false;
            PageDelta = 0;
        }
    }

    /// <summary>Compuerta de la entrada del jugador (ver <see cref="PlayerInputReader.Gate"/>).</summary>
    public enum InputGate { Full, BookOnly, None, Typing }
}
