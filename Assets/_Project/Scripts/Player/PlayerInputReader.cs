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
        
        // Uso secundario (ej. click derecho para castear/undo)
        public bool IsSecondaryUsingTool { get; private set; }

        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction jumpAction;
        private InputAction interactAction;
        private InputAction useToolAction;
        private InputAction secondaryUseToolAction;

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

            // Vista (Mouse o Right Stick)
            lookAction = new InputAction("Look", InputActionType.Value);
            lookAction.AddBinding("<Mouse>/delta");
            lookAction.AddBinding("<Gamepad>/rightStick");

            // Salto (Space o South Button A/Cross)
            jumpAction = new InputAction("Jump", InputActionType.Button);
            jumpAction.AddBinding("<Keyboard>/space");
            jumpAction.AddBinding("<Gamepad>/buttonSouth");

            // Interactuar (Tecla E o Right Trigger)
            interactAction = new InputAction("Interact", InputActionType.Button);
            interactAction.AddBinding("<Keyboard>/e");
            interactAction.AddBinding("<Gamepad>/rightTrigger");

            // Usar Herramienta (Click Izquierdo o Left Trigger)
            useToolAction = new InputAction("UseTool", InputActionType.Button);
            useToolAction.AddBinding("<Mouse>/leftButton");
            useToolAction.AddBinding("<Gamepad>/leftTrigger");

            // Uso Secundario (Click Derecho o Boton B/Circle/East)
            secondaryUseToolAction = new InputAction("SecondaryUseTool", InputActionType.Button);
            secondaryUseToolAction.AddBinding("<Mouse>/rightButton");
            secondaryUseToolAction.AddBinding("<Gamepad>/buttonEast");
        }

        private void OnEnable()
        {
            moveAction.Enable();
            lookAction.Enable();
            jumpAction.Enable();
            interactAction.Enable();
            useToolAction.Enable();
            secondaryUseToolAction.Enable();
        }

        private void OnDisable()
        {
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
            
            // IsPressed = True mientras se mantenga apretado (o triggered para un toque)
            IsUsingTool = useToolAction.IsPressed(); 
            IsSecondaryUsingTool = secondaryUseToolAction.triggered; // Triggered para Cast/Undo (un solo click)
        }
    }
}
