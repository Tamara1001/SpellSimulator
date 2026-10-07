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
}
