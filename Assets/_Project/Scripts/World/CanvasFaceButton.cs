using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Interfaces;

namespace SpellSimulator.World
{
    /// <summary>Boton de la esquina del marco (<see cref="CanvasFrame"/>): apuntar + clic (o gatillo en VR) invierte la cara de disparo.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class CanvasFaceButton : MonoBehaviour, IPressable
    {
        [SerializeField] private CanvasFrame frame;

        public bool PressWithClick => true;

        public void Configure(CanvasFrame frame) => this.frame = frame;

        public string GetPressPrompt() => frame != null && frame.Face == FiringFace.Front
            ? "Cara de disparo: frente (cambiar a espalda)"
            : "Cara de disparo: espalda (cambiar a frente)";

        public void Press()
        {
            if (frame != null) frame.Toggle();
        }
    }
}
