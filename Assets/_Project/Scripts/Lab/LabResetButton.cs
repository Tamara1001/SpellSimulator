using UnityEngine;
using SpellSimulator.Interfaces;
using SpellSimulator.Net;
using SpellSimulator.Player;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Boton «valores por defecto» de la mesa del laboratorio (apuntar + clic, gatillo en VR): vuelve todas las variables al
    /// catalogo y el fuego a sus defaults (<see cref="LabParameters.ResetAll"/>). No borra lo materializado.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class LabResetButton : MonoBehaviour, IPressable, LabParameters.IHoverTarget
    {
        [SerializeField, Tooltip("Renderer que se resalta al apuntar (vacio = el primero en los hijos).")]
        private Renderer highlight;

        private LabStepPlate.HoverTint tint;

        private void Awake()
        {
            if (highlight == null) highlight = GetComponentInChildren<Renderer>();
            tint.Init(highlight);
        }

        public bool PressWithClick => true;

        public string GetPressPrompt() => "Valores por defecto";

        public void Press()
        {
            if (NetSession.Exists)
            {
                Hud.Notify("lab", "Laboratorio: solo en modo Libre", 2.5f);
                return;
            }
            LabParameters parameters = LabParameters.Current;
            if (parameters == null) return;
            parameters.ResetAll();
            Hud.Notify("lab", "Laboratorio: valores por defecto", 2f);
        }

        public void SetHovered(bool on) => tint.Set(on);
    }
}
