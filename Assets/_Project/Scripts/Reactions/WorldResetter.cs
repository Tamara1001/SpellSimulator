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

        private void OnEnable() => Learning.Grimoire.ResetWorldRequested += ResetFromBook;
        private void OnDisable() => Learning.Grimoire.ResetWorldRequested -= ResetFromBook;

        private void ResetFromBook()
        {
            if (world == null) return;
            world.ResetWorld();
            DiagnosticBus.OnDiagnosticsUpdated?.Invoke("<b>[Mundo]</b> Reiniciado desde el grimorio.");
        }

        private void Update()
        {
            if (input == null || world == null || !input.IsResettingWorld) return;
            world.ResetWorld();
            DiagnosticBus.OnDiagnosticsUpdated?.Invoke("<b>[Mundo]</b> Reiniciado.");
        }
    }
}
