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
