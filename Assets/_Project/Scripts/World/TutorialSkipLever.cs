using UnityEngine;
using SpellSimulator.Core;
using SpellSimulator.Interfaces;
using SpellSimulator.Player;

namespace SpellSimulator.World
{
    /// <summary>Palanca de la terraza: saltea el tutorial y lleva al nexo. Sirve tambien en VR, que no tiene menu.</summary>
    public class TutorialSkipLever : MonoBehaviour, IPressable
    {
        public string GetPressPrompt() => "Saltear el tutorial (ir al nexo)";

        public void Press()
        {
            IPlayerContext local = PlayerRegistry.Local;
            PlayerSpawn spawn = local != null && local.Root != null ? local.Root.GetComponent<PlayerSpawn>() : null;
            if (spawn == null) spawn = FindFirstObjectByType<PlayerSpawn>();
            if (spawn != null) spawn.Begin(true);
        }
    }
}
