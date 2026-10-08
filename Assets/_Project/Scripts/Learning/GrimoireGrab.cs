using UnityEngine;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Learning
{
    /// <summary>
    /// Zona de agarre del grimorio abierto: un disparador grande detras de las paginas y los botones (cubre tapas,
    /// lomo, bordes y paginas vacias), asi que los botones siguen ganando el rayo del interactor.
    /// En PC el interactor lo arrastra con el movedor (el Rigidbody cinematico del libro + <see cref="Grimoire"/> como
    /// IRemoteGrabbable). En VR se lleva en la mano secundaria (<see cref="OnInteract"/>) y al soltarlo queda flotando.
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
