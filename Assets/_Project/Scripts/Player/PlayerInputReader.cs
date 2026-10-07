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
        /// <summary>Agacharse (mantener): Ctrl o C, click del stick derecho.</summary>
        public bool IsCrouching { get; private set; }
        /// <summary>Arrojar lo que se tiene en la mano: Q, hombro izquierdo.</summary>
        public bool IsThrowing { get; private set; }
        /// <summary>Dar vuelta la hoja en la mano (o la que se mira): F, hombro derecho. En VR se gira con la mano.</summary>
        public bool IsFlipping { get; private set; }
        /// <summary>Fijar el lienzo en mano a la camara para dibujar comodo (alterna): V.</summary>
        public bool IsTogglingFocus { get; private set; }
        /// <summary>Pasar pagina del grimorio abierto: -1 / +1 (flechas, cruceta).</summary>
        public int PageDelta { get; private set; }
        /// <summary>Rueda del mouse (acerca/aleja lo que se sostiene; pasa paginas con el grimorio abierto).</summary>
        public float Scroll { get; private set; }

        /// <summary>Tumbarse / levantarse (alterna): Z. Para dibujar en el piso.</summary>
        public bool IsTogglingProne { get; private set; }
        /// <summary>Dejar lo que llevas flotando en el aire (en vez de soltarlo): G.</summary>
        public bool IsPinning { get; private set; }

        private InputAction proneAction;
        private InputAction pinAction;
        private InputAction runAction;
        private InputAction crouchAction;
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
            proneAction.AddBinding("<Keyboard>/z");
            proneAction.AddBinding("<Gamepad>/dpad/down");
            pinAction = new InputAction("Pin", InputActionType.Button);
            pinAction.AddBinding("<Keyboard>/g");

            runAction = new InputAction("Run", InputActionType.Button);
            runAction.AddBinding("<Keyboard>/leftShift");
            runAction.AddBinding("<Gamepad>/leftStickPress");
            runAction.AddBinding("<XRController>{LeftHand}/thumbstickClicked");

            crouchAction = new InputAction("Crouch", InputActionType.Button);
            crouchAction.AddBinding("<Keyboard>/leftCtrl");
            crouchAction.AddBinding("<Keyboard>/c");
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
            focusAction.AddBinding("<Keyboard>/v");

            pagePrevAction = new InputAction("PagePrev", InputActionType.Button);
            pagePrevAction.AddBinding("<Keyboard>/leftArrow");
            pagePrevAction.AddBinding("<Gamepad>/dpad/left");
            pageNextAction = new InputAction("PageNext", InputActionType.Button);
            pageNextAction.AddBinding("<Keyboard>/rightArrow");
            pageNextAction.AddBinding("<Gamepad>/dpad/right");

            scrollAction = new InputAction("Scroll", InputActionType.Value);
            scrollAction.AddBinding("<Mouse>/scroll/y");

            bookAction = new InputAction("Book", InputActionType.Button);
            bookAction.AddBinding("<Keyboard>/tab");
            bookAction.AddBinding("<Gamepad>/buttonNorth");
            bookAction.AddBinding("<XRController>{LeftHand}/menuButton");
        }

        private void OnEnable()
        {
            reloadAction.Enable();
            runAction.Enable();
            proneAction.Enable();
            pinAction.Enable();
            crouchAction.Enable();
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
            pinAction.Disable();
            crouchAction.Disable();
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
            IsPinning = pinAction.triggered;
            IsCrouching = crouchAction.IsPressed();
            IsThrowing = throwAction.triggered;
            IsFlipping = flipAction.triggered;
            IsTogglingFocus = focusAction.triggered;
            PageDelta = (pageNextAction.triggered ? 1 : 0) - (pagePrevAction.triggered ? 1 : 0);
            Scroll = scrollAction.ReadValue<float>();
        }
    }
}
