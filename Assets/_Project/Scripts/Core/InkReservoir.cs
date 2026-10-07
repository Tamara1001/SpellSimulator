using System;
using UnityEngine;

namespace SpellSimulator.Core
{
    /// <summary>
    /// Deposito de tinta de conjuro (canon: solo esta tinta activa sellos). Logica pura y testeable.
    /// Lo tienen las herramientas que escriben, los tinteros y la reserva personal del jugador.
    /// </summary>
    [Serializable]
    public class InkReservoir
    {
        [SerializeField, Min(0f)] private float capacity = 100f;
        [SerializeField, Min(0f)] private float level = 100f;

        public InkReservoir(float capacity, float level)
        {
            this.capacity = capacity;
            this.level = Mathf.Clamp(level, 0f, capacity);
        }

        public float Capacity => capacity;
        public float Level => level;
        public float Fraction => capacity > 0f ? level / capacity : 0f;
        public bool IsEmpty => level <= 0f;
        public float Missing => capacity - level;

        /// <summary>Gasta tinta. Devuelve false si no alcanzo (y deja el deposito en 0).</summary>
        public bool TryConsume(float amount)
        {
            if (amount <= 0f) return true;
            if (level >= amount)
            {
                level -= amount;
                return true;
            }
            level = 0f;
            return false;
        }

        /// <summary>Pasa tinta desde otro deposito hasta llenar este. Devuelve cuanto se transfirio.</summary>
        public float FillFrom(InkReservoir source)
        {
            float amount = Mathf.Min(Missing, source.level);
            source.level -= amount;
            level += amount;
            return amount;
        }
    }
}
