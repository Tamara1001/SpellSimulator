using UnityEngine;

namespace SpellSimulator.Core
{
    /// <summary>
    /// Lo que la logica de juego necesita saber de UN jugador (preparacion para multijugador, F0 de MULTIPLAYER_PLAN): quien es,
    /// si es el local y donde tiene la cabeza y las manos. Reemplaza a Camera.main y FindFirstObjectByType en reglas del mundo
    /// (presupuesto de objetos, cristal, portales, hechizos...). Hoy hay un solo jugador (<see cref="PlayerRegistry.Local"/>).
    /// </summary>
    public interface IPlayerContext
    {
        /// <summary>Identificador del jugador (0 = jugador local en solitario).</summary>
        int OwnerId { get; }
        /// <summary>True si lo controla este cliente.</summary>
        bool IsLocal { get; }
        /// <summary>Raiz del jugador (su cuerpo).</summary>
        Transform Root { get; }
        /// <summary>Cabeza/camara: de aca sale la mirada y el punto de vista que decide hacia donde sale un hechizo.</summary>
        Transform Head { get; }
        /// <summary>Mano principal y secundaria (pueden ser null en PC).</summary>
        Transform MainHand { get; }
        Transform SecondaryHand { get; }
        /// <summary>True mientras el contexto sigue siendo valido (el objeto no se destruyo).</summary>
        bool IsAlive { get; }
    }
}
