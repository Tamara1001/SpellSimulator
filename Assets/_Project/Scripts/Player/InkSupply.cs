using UnityEngine;
using SpellSimulator.Core;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Player
{
    /// <summary>Tinta que el jugador lleva encima. Es la fuente de recarga cuando no hay un tintero a mano.</summary>
    public class InkSupply : MonoBehaviour, IInkSource
    {
        [SerializeField] private InkReservoir reserve = new InkReservoir(300f, 300f);

        public InkReservoir Reserve => reserve;

        public float Refill(InkReservoir target) => target.FillFrom(reserve);

        /// <summary>Guarda tinta de un tintero en la reserva personal.</summary>
        public float Stock(IInkSource source) => source.Refill(reserve);
    }
}
