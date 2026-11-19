using UnityEngine;
using SpellSimulator.Interfaces;

namespace SpellSimulator.World
{
    /// <summary>
    /// Boton con flechas circulares al pie del atril (Docs/LIENZOS_PLAN.md): invierte el sentido de disparo DEL ATRIL, o sea de cualquier
    /// lienzo que tenga puesto (<see cref="CanvasEasel.SetReversed"/>). Apuntar + E en PC (agarre/interaccion en VR).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class EaselReverseButton : MonoBehaviour, IPressable
    {
        [SerializeField] private CanvasEasel easel;

        public bool PressWithClick => false; // con E (o el agarre en VR): asi el aviso del boton se ve al apuntarlo

        /// <summary>Lo llama el generador de escena (Editor).</summary>
        public void Configure(CanvasEasel easel) => this.easel = easel;

        public string GetPressPrompt() => easel != null && easel.Reversed
            ? "Sentido del atril: invertido (volver al normal)"
            : "Sentido del atril: normal (invertirlo)";

        public void Press()
        {
            if (easel == null) return;
            easel.ToggleReversed();
            SpellSimulator.Player.Hud.Notify("easelrev", easel.Reversed ? "Atril: dispara hacia el otro lado" : "Atril: dispara hacia el lado normal", 2f);
        }
    }
}
