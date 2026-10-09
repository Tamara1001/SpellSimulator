using UnityEngine;
using SpellSimulator.Core;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Player
{
    /// <summary>Cartuchos de tinta que el jugador lleva encima. Es la fuente de recarga cuando no hay un tintero a mano.</summary>
    public class InkSupply : MonoBehaviour, IInkSource
    {
        public InkCartridgeStock Cartridges = new InkCartridgeStock();

        /// <summary>Clase del ultimo uso gastado por <see cref="Refill"/>.</summary>
        public InkKind LastRefillKind { get; private set; } = InkKind.Normal;

        /// <summary>Gasta UN uso de la clase activa y llena el deposito por completo. Devuelve la tinta transferida (0 si no hay cartuchos).</summary>
        public float Refill(InkReservoir target)
        {
            if (target.Missing <= 0f || !Cartridges.TryUse(out InkKind kind)) return 0f;
            LastRefillKind = kind;
            float missing = target.Missing;
            target.FillFrom(new InkReservoir(missing, missing));
            return missing;
        }
    }
}
