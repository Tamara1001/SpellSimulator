using UnityEngine;
using SpellSimulator.Interfaces;

namespace SpellSimulator.World
{
    /// <summary>
    /// Boton con flechas circulares al pie del atril (Docs/LIENZOS_PLAN.md): invierte el sentido de disparo DEL ATRIL, o sea de cualquier
    /// lienzo que tenga puesto (<see cref="CanvasEasel.SetReversed"/>). Apuntar + clic en PC, gatillo en VR.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class EaselReverseButton : MonoBehaviour, IPressable
    {
        [SerializeField] private CanvasEasel easel;

        public bool PressWithClick => true;

        /// <summary>Lo llama el generador de escena (Editor).</summary>
        public void Configure(CanvasEasel easel) => this.easel = easel;

        public string GetPressPrompt() => easel != null && easel.Reversed
            ? "Sentido del atril: invertido (clic para volver al normal)"
            : "Sentido del atril: normal (clic para invertirlo)";

        public void Press()
        {
            if (easel != null) easel.ToggleReversed();
        }
    }
}
