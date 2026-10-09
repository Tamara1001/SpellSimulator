using UnityEngine;

namespace SpellSimulator.Player
{
    /// <summary>
    /// Pedido de agarre (F0 de MULTIPLAYER_PLAN, 2.6): quien pide, que objeto y a que distancia lo miraba. Hoy lo ejecuta
    /// <see cref="PlayerInteractor.TryGrab"/> local e inmediatamente; en red viaja al servidor, que valida (alcance, libre,
    /// no cristalizado) y acepta o rechaza.
    /// </summary>
    public struct GrabRequest
    {
        public int ownerId;
        public GameObject target;
        public float distance;

        public GrabRequest(int ownerId, GameObject target, float distance)
        {
            this.ownerId = ownerId;
            this.target = target;
            this.distance = distance;
        }
    }
}
