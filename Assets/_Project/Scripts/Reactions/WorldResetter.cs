using UnityEngine;
using SpellSimulator.DebugTools;
using SpellSimulator.Player;

namespace SpellSimulator.Reactions
{
    /// <summary>Reinicio del mundo a pedido del jugador (Backspace / Select / Y izquierdo). Modo Libre.</summary>
    public class WorldResetter : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private MagicWorld world;

        private void Update()
        {
            if (input == null || world == null || !input.IsResettingWorld) return;
            world.ResetWorld();
            DiagnosticBus.OnDiagnosticsUpdated?.Invoke("<b>[Mundo]</b> Reiniciado.");
        }
    }
}
