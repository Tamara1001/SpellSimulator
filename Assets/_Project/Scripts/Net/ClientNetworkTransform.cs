using Unity.Netcode.Components;
using UnityEngine;

namespace SpellSimulator.Net
{
    /// <summary>
    /// NetworkTransform con autoridad del DUENO (decision de movimiento A del plan multijugador: cada jugador manda sobre su cuerpo;
    /// el servidor solo retransmite). Se usa en el avatar de red de cada jugador.
    /// </summary>
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
