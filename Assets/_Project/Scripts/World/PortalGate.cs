using UnityEngine;

namespace SpellSimulator.World
{
    /// <summary>
    /// Volumen de cruce de un <see cref="Portal"/>: va en el Collider trigger del disco (lo agrega el Portal solo). Cuando algo
    /// lo atraviesa avisa al portal, que decide si viaja (solo si esta Open).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortalGate : MonoBehaviour
    {
        private Portal portal;

        internal void Bind(Portal owner) => portal = owner;

        private void OnTriggerEnter(Collider other)
        {
            if (portal != null) portal.OnCross(other);
        }
    }
}
