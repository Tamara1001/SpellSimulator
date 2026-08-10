using UnityEngine;

namespace SpellSimulator.Interfaces
{
    /// <summary>
    /// Todo objeto en el mundo 3D que el jugador pueda mirar y recoger/agarrar.
    /// </summary>
    public interface IInteractable
    {
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
