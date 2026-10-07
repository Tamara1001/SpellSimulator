using UnityEngine;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Learning
{
    /// <summary>
    /// Agarradera del grimorio abierto (el lomo inferior). Se lleva en la mano izquierda y, al soltarlo, queda flotando
    /// donde se lo dejo: el libro se puede mover y apoyar a gusto. La logica del libro sigue en <see cref="Grimoire"/>.
    /// </summary>
    public class GrimoireGrab : MonoBehaviour, IInteractable
    {
        private Grimoire book;
        private Transform bookRoot;
        private Transform home;

        public HandType PreferredHand => HandType.Left;

        public void Configure(Grimoire owner, Transform root, Transform homeParent)
        {
            book = owner;
            bookRoot = root;
            home = homeParent;
        }

        public string GetInteractionPrompt() => "Agarrar grimorio";

        public void OnInteract(Transform holdPoint)
        {
            bookRoot.SetParent(holdPoint, false);
            bookRoot.localPosition = new Vector3(0f, -0.05f, 0f);
            bookRoot.localRotation = Quaternion.Euler(-12f, 0f, 0f);
        }

        public void OnDrop() => bookRoot.SetParent(home, true);
    }
}
