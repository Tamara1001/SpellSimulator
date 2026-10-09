using UnityEngine;
using SpellSimulator.Interfaces;

namespace SpellSimulator.World
{
    /// <summary>
    /// Cinturon del mundo. Al interactuar (E en PC / agarre en VR) el PlayerInteractor lo equipa (permite llevar
    /// cartuchos de las tres clases de tinta) y lo destruye; ver su caso en HandleInteraction. No es Respawnable.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Belt : MonoBehaviour, IInteractable
    {
        public HandType PreferredHand => HandType.Both;
        public string GetInteractionPrompt() => "Ponerte el cinturon (mas tintas)";
        public void OnInteract(Transform holdPoint) { }
        public void OnDrop() { }
    }
}
