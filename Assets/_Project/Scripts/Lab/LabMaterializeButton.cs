using UnityEngine;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Boton «materializar» de la mesa del laboratorio: se aprieta apuntando + clic (gatillo en VR) y pide a
    /// <see cref="LabSpawner"/> una caja, una zona de viento, un parche de fuego o (fase 3) una fuente con los valores actuales.
    /// Los topes, el enfriamiento y el bloqueo en red los decide el spawner.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class LabMaterializeButton : MonoBehaviour, IPressable, LabParameters.IHoverTarget, ILabHelp
    {
        [SerializeField, Tooltip("Que materializa este boton.")]
        private LabSpawnKind kind = LabSpawnKind.Box;
        [SerializeField, Tooltip("Renderer que se resalta al apuntar (vacio = el primero en los hijos).")]
        private Renderer highlight;

        private string prompt;
        private string helpLine;

        /// <summary>Ayuda para la tira del panel (v4).</summary>
        public string HelpLine => helpLine ??= kind switch
        {
            LabSpawnKind.Box => LabHelpStrip.Line("+ Caja", "Pone una caja en la pista (hasta 4) y la elige."),
            LabSpawnKind.Wind => LabHelpStrip.Line("+ Viento", "Sopla una zona de viento; ajustala con las perillas."),
            LabSpawnKind.Fire => LabHelpStrip.Line("+ Fuego", "Prende el pasto en el fogón con la semilla elegida."),
            _ => LabHelpStrip.Line("Materializar", "")
        };
        private LabControl.HoverTint tint;

        private void Awake()
        {
            if (highlight == null) highlight = GetComponentInChildren<Renderer>();
            tint.Init(highlight);
            prompt = kind switch
            {
                LabSpawnKind.Box => "Agregar una caja",
                LabSpawnKind.Wind => "Agregar viento",
                LabSpawnKind.Fire => "Prender fuego",
                LabSpawnKind.Fountain => "Materializar fuente",
                _ => "Materializar"
            };
        }

        public bool PressWithClick => true;

        public string GetPressPrompt() => prompt ?? "";

        public void Press()
        {
            LabSpawner spawner = LabSpawner.Current;
            if (spawner != null) spawner.Materialize(kind);
        }

        public void SetHovered(bool on)
        {
            tint.Set(on);
            if (on) LabHelpStrip.Focus(this);
            else LabHelpStrip.Unfocus(this);
        }

        private void OnDisable() => LabHelpStrip.Unfocus(this);
    }
}
