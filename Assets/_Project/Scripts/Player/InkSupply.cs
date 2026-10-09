using UnityEngine;
using SpellSimulator.Core;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Player
{
    /// <summary>Cartuchos de tinta que el jugador lleva encima. Es la fuente de recarga cuando no hay un tintero a mano.</summary>
    public class InkSupply : MonoBehaviour, IInkSource
    {
        public InkCartridgeStock Cartridges = new InkCartridgeStock();

        /// <summary>Gasta UN uso de cartucho y llena el deposito por completo. Devuelve la tinta transferida (0 si no hay cartuchos).</summary>
        public float Refill(InkReservoir target)
        {
            if (target.Missing <= 0f || !Cartridges.TryUse(out _)) return 0f;
            float missing = target.Missing;
            target.FillFrom(new InkReservoir(missing, missing));
            return missing;
        }
    }
}
