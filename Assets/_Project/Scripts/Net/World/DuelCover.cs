using UnityEngine;
using SpellSimulator.Combat;

namespace SpellSimulator.Net
{
    /// <summary>
    /// Cobertura destructible de la arena del Duelo: tiene vida (<see cref="Health"/>) y el dano de los hechizos la rompe. Solo el anfitrion decide
    /// cuando se rompe; los clientes copian el estado (mascara de rotas en el mensaje del duelo). Fuera de la ronda se restaura sola.
    /// </summary>
    public sealed class DuelCover : MonoBehaviour
    {
        public const float CoverHealth = 60f;

        public int Index { get; set; }
        public Health Health { get; private set; }
        public bool Broken { get; private set; }

        private Renderer shownRenderer;
        private Collider solid;

        public void Init(int index)
        {
            Index = index;
            Health = gameObject.AddComponent<Health>();
            Health.SetMax(CoverHealth);
            shownRenderer = GetComponent<Renderer>();
            solid = GetComponent<Collider>();
        }

        /// <summary>Se rompe (desaparece): lo decide el anfitrion o lo copia un cliente.</summary>
        public void SetBroken(bool broken)
        {
            if (Broken == broken) return;
            Broken = broken;
            if (shownRenderer != null) shownRenderer.enabled = !broken;
            if (solid != null) solid.enabled = !broken;
            if (!broken && Health != null) Health.ResetHealth();
        }
    }
}
