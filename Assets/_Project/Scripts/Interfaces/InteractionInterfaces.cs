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
