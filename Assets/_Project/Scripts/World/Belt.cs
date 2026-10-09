using UnityEngine;
using SpellSimulator.Interfaces;

namespace SpellSimulator.World
{
    /// <summary>
    /// Cinturon del mundo. Al interactuar (E en PC / agarre en VR) el PlayerInteractor lo equipa (suma 2 slots,
    /// teclas 4 y 5) y lo destruye; ver su caso en HandleInteraction. No es Respawnable.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Belt : MonoBehaviour, IInteractable
    {
        public HandType PreferredHand => HandType.Both;
        public string GetInteractionPrompt() => "Ponerte el cinturon";
        public void OnInteract(Transform holdPoint) { }
        public void OnDrop() { }
    }
}
