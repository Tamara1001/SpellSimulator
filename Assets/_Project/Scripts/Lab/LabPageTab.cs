using UnityEngine;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Pestaña de una pagina de la consola (Laboratorio v5, punto A.3): se aprieta apuntando + clic (gatillo en VR) y muestra su
    /// pagina en el <see cref="LabPageSet"/>. La elegida levanta una marca dorada (<see cref="SetSelected"/>).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class LabPageTab : MonoBehaviour, IPressable, LabParameters.IHoverTarget, ILabHelp
    {
        [SerializeField, Tooltip("Paginas del panel.")]
        private LabPageSet set;
        [SerializeField, Tooltip("Pagina que muestra.")]
        private int index;
        [SerializeField, Tooltip("Nombre de la pagina (pista y ayuda).")]
        private string title = "Página";
        [SerializeField, Tooltip("Que hay en la pagina (tira de ayuda).")]
        private string help = "";
        [SerializeField, Tooltip("Renderer que se resalta al apuntar.")]
        private Renderer highlight;
        [SerializeField, Tooltip("Marca de la pestaña elegida (se prende y apaga).")]
        private GameObject selectedMark;

        private LabControl.HoverTint tint;
        private string prompt, helpLine;

        /// <summary>Pagina que muestra.</summary>
        public int Index => index;

        public string HelpLine => helpLine ??= LabHelpStrip.Line(title, help);

        public bool PressWithClick => true;

        /// <summary>Lo llama el generador de escena (Editor) despues de AddComponent.</summary>
        public void Configure(LabPageSet set, int index, string title, string help, Renderer highlight, GameObject selectedMark)
        {
            this.set = set;
            this.index = index;
            this.title = title;
            this.help = help;
            this.highlight = highlight;
            this.selectedMark = selectedMark;
            prompt = helpLine = null;
        }

        private void Awake()
        {
            if (highlight == null) highlight = GetComponentInChildren<Renderer>();
            tint.Init(highlight);
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnDisable()
        {
            tint.Set(false);
            LabHelpStrip.Unfocus(this);
        }

        public string GetPressPrompt() => prompt ??= "Ver " + title;

        public void Press()
        {
            if (set != null) set.Show(index);
        }

        /// <summary>Marca (o desmarca) la pestaña como la de la pagina visible.</summary>
        public void SetSelected(bool on)
        {
            if (selectedMark != null && selectedMark.activeSelf != on) selectedMark.SetActive(on);
        }

        public void SetHovered(bool on)
        {
            tint.Set(on);
            if (on) LabHelpStrip.Focus(this);
            else LabHelpStrip.Unfocus(this);
        }
    }
}
