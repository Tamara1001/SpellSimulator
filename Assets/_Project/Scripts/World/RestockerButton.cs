using UnityEngine;
using SpellSimulator.Interfaces;

namespace SpellSimulator.World
{
    /// <summary>Que hace un boton del restocker.</summary>
    public enum RestockerAction { Brush = 0, Kind = 1, Size = 2, Shape = 3, PagesLess = 4, PagesMore = 5, DispenseCanvas = 6 }

    /// <summary>Boton del restocker (apuntar + clic, gatillo en VR). <see cref="RestockerAction.Brush"/> usa <c>argument</c> = indice del catalogo de pinceles.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class RestockerButton : MonoBehaviour, IPressable
    {
        [SerializeField] private CanvasRestocker restocker;
        [SerializeField] private RestockerAction action;
        [SerializeField] private int argument;
        [SerializeField] private string prompt = "Restocker";

        public bool PressWithClick => true;

        public void Configure(CanvasRestocker restocker, RestockerAction action, int argument, string prompt)
        {
            this.restocker = restocker;
            this.action = action;
            this.argument = argument;
            this.prompt = prompt;
        }

        public string GetPressPrompt() => prompt;

        public void Press()
        {
            if (restocker != null) restocker.Press(action, argument);
        }
    }
}
