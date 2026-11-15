using UnityEngine;

namespace SpellSimulator.Interfaces
{
    public enum HandType { Right, Left, Both }

    /// <summary>
    /// Todo objeto en el mundo 3D que el jugador pueda mirar y recoger/agarrar.
    /// </summary>
    public interface IInteractable
    {
        HandType PreferredHand { get; }
        string GetInteractionPrompt();
        
        // Se llama cuando el jugador aprieta Clic Izquierdo para agarrarlo
        void OnInteract(Transform holdPoint);
        
        // Se llama si el jugador decide soltarlo (opcional por ahora)
        void OnDrop();
    }

    /// <summary>
    /// Todo objeto agarrado que el jugador pueda "usar" (ej: apretar gatillo / Clic Derecho)
    /// </summary>
    public interface IUsable
    {
        void OnUseStart();
        void OnUseHold();
        void OnUseEnd();
        
        // Clic Derecho / Boton Secundario
        void OnSecondaryUseStart();
    }
}

namespace SpellSimulator.Interfaces
{
    /// <summary>Herramienta que escribe con tinta (lapiz, pincel).</summary>
    public interface IInkUser
    {
        bool UsesInk { get; }
        SpellSimulator.Core.InkReservoir Ink { get; }
        /// <summary>Recarga desde una fuente y devuelve cuanta tinta paso.</summary>
        float RefillFrom(IInkSource source);
        /// <summary>Clase de tinta que lleva cargada.</summary>
        SpellSimulator.Core.InkKind LoadedInk { get; }
        /// <summary>Llena la herramienta por completo con tinta de esa clase (cartucho).</summary>
        void LoadInk(SpellSimulator.Core.InkKind kind);
    }

    /// <summary>Algo de donde se puede recargar tinta (tintero, reserva personal).</summary>
    public interface IInkSource
    {
        /// <summary>Llena el deposito y devuelve cuanta tinta paso.</summary>
        float Refill(SpellSimulator.Core.InkReservoir target);
    }

    /// <summary>Algo que modifica el color de una superficie (carbon, humedad). Lo consulta quien pinta el material.</summary>
    public interface ISurfaceTint
    {
        UnityEngine.Color Tint(UnityEngine.Color color);
    }

    /// <summary>
    /// Materia de un elemento (piedra = tierra, algo ardiendo = fuego). Canon: el pull solo atrae materia de su mismo
    /// elemento, nunca cualquier objeto.
    /// </summary>
    public interface IElementMatter
    {
        SpellSimulator.Compiler.ElementType Element { get; }
    }

    /// <summary>
    /// Algo que se aprieta en el acto en vez de agarrarse: pasar una pagina, leer un libro, un boton del grimorio.
    /// Se usa con la misma tecla que agarrar (E / grip) y tiene prioridad sobre agarrar.
    /// </summary>
    public interface IPressable
    {
        string GetPressPrompt();
        void Press();
        /// <summary>True para botones de menu (grimorio): se aprietan con clic, no con E.</summary>
        bool PressWithClick => false;
    }
}

namespace SpellSimulator.Interfaces
{
    /// <summary>
    /// Marca un objeto cuyo sello no se lanza al cerrar el anillo sino cuando se activa un disparador (impacto,
    /// presion). SpellCaster lo busca en el lienzo para dejar el sello armado.
    /// </summary>
    public interface ISealTrigger { }

    /// <summary>
    /// Objeto que puede vetar su sello (una runa agotada): SpellCaster no lo arma ni lo dispara mientras
    /// <see cref="SealBlocked"/> sea true.
    /// </summary>
    public interface ISealGate
    {
        bool SealBlocked { get; }
    }
}

namespace SpellSimulator.Interfaces
{
    /// <summary>Objeto que se puede arrojar: recibe la velocidad con que sale de la mano.</summary>
    public interface IThrowable
    {
        void Throw(UnityEngine.Vector3 velocity);
    }

    /// <summary>Objeto sostenido que puede quedar flotando en el aire al soltarlo (G).</summary>
    public interface IPinnable
    {
        /// <summary>Suelta el objeto dejandolo flotando donde esta. El interactor ya lo solto de la mano.</summary>
        void Pin();
    }

    /// <summary>Hoja o libro que se puede dar vuelta (F en PC, girar la mano en VR).</summary>
    public interface IFlippable
    {
        void Flip();
    }

    /// <summary>Objeto sostenido que pide quedar fijo delante de la camara para dibujar (PC).</summary>
    public interface IFocusable
    {
        bool Focused { get; }
        void SetFocus(UnityEngine.Transform camera, bool on);
    }
}

namespace SpellSimulator.Interfaces
{
    /// <summary>
    /// Objeto que reacciona al agarre a distancia (G): al tomarlo deja de flotar/estar fijo y se mueve con fisica;
    /// al soltarlo puede quedar flotando donde esta (pin) o caer. Si un objeto con Rigidbody no la implementa,
    /// el movedor usa un comportamiento generico (sin gravedad mientras se sostiene).
    /// </summary>
    public interface IRemoteGrabbable
    {
        void OnRemoteGrab();
        void OnRemoteRelease(bool pin);
        /// <summary>True si el objeto es cinematico y el movedor lo lleva con MovePosition/MoveRotation (por ejemplo el grimorio).</summary>
        bool DriveKinematic => false;
    }
}

namespace SpellSimulator.Interfaces
{
    /// <summary>Objeto con estado propio que vuelve a su estado inicial al reiniciar el mundo (tras recolocarlo Respawnable).</summary>
    public interface IResettable
    {
        void ResetToInitial();
    }
}


namespace SpellSimulator.Interfaces
{
    /// <summary>Entrada de un frame para un <see cref="IManipulable"/>. Struct de solo lectura: no aloca.</summary>
    public readonly struct ManipulationInput
    {
        /// <summary>PC: delta del mouse de este frame (unidades crudas). En VR es cero.</summary>
        public readonly UnityEngine.Vector2 PointerDelta;
        /// <summary>VR: posicion de la mano (controlador) en el mundo. En PC, la de la camara.</summary>
        public readonly UnityEngine.Vector3 HandPosition;
        /// <summary>VR: giro de la muñeca en grados, alrededor del rayo (-180..180; desenrollar con LabControlMath.UnwrapAngle).</summary>
        public readonly float WristRoll;
        public readonly UnityEngine.Vector3 RayOrigin;
        public readonly UnityEngine.Vector3 RayDirection;
        /// <summary>Eje derecho de la camara (para orientar el arrastre horizontal del mouse en PC).</summary>
        public readonly UnityEngine.Vector3 CameraRight;
        /// <summary>Rueda del mouse de este frame (0 si no giro).</summary>
        public readonly float Wheel;
        public readonly bool IsVR;

        public ManipulationInput(UnityEngine.Vector2 pointerDelta, UnityEngine.Vector3 handPosition, float wristRoll,
            UnityEngine.Vector3 rayOrigin, UnityEngine.Vector3 rayDirection, UnityEngine.Vector3 cameraRight, float wheel, bool isVR)
        {
            PointerDelta = pointerDelta;
            HandPosition = handPosition;
            WristRoll = wristRoll;
            RayOrigin = rayOrigin;
            RayDirection = rayDirection;
            CameraRight = cameraRight;
            Wheel = wheel;
            IsVR = isVR;
        }
    }

    /// <summary>
    /// Control que se captura con clic/gatillo (grip en VR) y se manipula mientras se mantiene: perillas, deslizadores,
    /// selectores. El interactor congela la vista en PC, marca el clic como consumido y llama a Update cada frame.
    /// Un giro de rueda sobre el control (sin captura) se entrega como Begin + Update(Wheel != 0) + End en el mismo frame.
    /// </summary>
    public interface IManipulable
    {
        void BeginManipulation(UnityEngine.RaycastHit hit);
        void UpdateManipulation(in ManipulationInput input);
        void EndManipulation();
        /// <summary>Pista junto a la mira. Debe ser una cadena cacheada (se compara por referencia).</summary>
        string HelpText { get; }
    }
}
