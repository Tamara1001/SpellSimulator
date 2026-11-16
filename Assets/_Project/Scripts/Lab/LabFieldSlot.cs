using UnityEngine;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Casilla de la lista de objetos del campo (Laboratorio v4, punto 7): «P» = papel de prueba, 1..4 = cajas. Tocarla
    /// (clic / gatillo) elige ese objeto (<see cref="LabField.Select"/>). Se pinta con un MaterialPropertyBlock segun su estado:
    /// elegida (dorada, brilla), ocupada (color normal) o vacia (apagada). Sondeo a 5 Hz, sin allocations.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class LabFieldSlot : MonoBehaviour, IPressable, LabParameters.IHoverTarget, ILabHelp
    {
        private const float PollSeconds = 0.2f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color SelectedColor = new Color(2.2f, 1.7f, 0.7f);
        private static readonly Color HoverColor = new Color(1.35f, 1.25f, 1f);
        private static readonly Color EmptyColor = new Color(0.32f, 0.3f, 0.34f);

        [SerializeField, Tooltip("0 = papel de prueba, 1..4 = cajas.")]
        private int slot;
        [SerializeField, Tooltip("Placa que se tiñe segun el estado.")]
        private Renderer plate;

        private MaterialPropertyBlock block;
        private int shownState = -1;
        private bool hovered;
        private float nextPoll;
        private string helpLine, prompt;

        /// <summary>Casilla (0 papel, 1..4 cajas).</summary>
        public int Slot => slot;

        public string HelpLine => helpLine ??= slot == LabField.PaperSlot
            ? LabHelpStrip.Line("Papel de prueba", "Elegilo para moverlo, girarlo, inclinarlo o cambiar su tamaño.")
            : LabHelpStrip.Line("Caja " + slot, "Elegila para moverla y cambiar su masa. Vacía: tocá «+ Caja».");

        /// <summary>Lo llama el generador de escena (Editor) despues de AddComponent.</summary>
        public void Configure(int slot, Renderer plate)
        {
            this.slot = slot;
            this.plate = plate;
        }

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            if (plate == null) plate = GetComponentInChildren<Renderer>();
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnDisable()
        {
            hovered = false;
            LabHelpStrip.Unfocus(this);
        }

        private void Update()
        {
            if (Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + PollSeconds;
            Refresh();
        }

        public bool PressWithClick => true;

        public string GetPressPrompt() => prompt ??= slot == LabField.PaperSlot ? "Elegir el papel de prueba" : "Elegir la caja " + slot;

        public void Press()
        {
            LabField field = LabField.Current;
            if (field == null) return;
            if (!field.HasObject(slot))
            {
                SpellSimulator.Player.Hud.Notify("lab", "Laboratorio: esa casilla está vacía (tocá «+ Caja»)", 2f);
                return;
            }
            field.Select(slot);
            nextPoll = 0f;
        }

        public void SetHovered(bool on)
        {
            hovered = on;
            if (on) LabHelpStrip.Focus(this);
            else LabHelpStrip.Unfocus(this);
            shownState = -1;
            Refresh();
        }

        private void Refresh()
        {
            LabField field = LabField.Current;
            int state = field == null || !field.HasObject(slot) ? 0 : field.Selected == slot ? 2 : 1;
            int key = state * 2 + (hovered ? 1 : 0);
            if (key == shownState || plate == null) return;
            shownState = key;
            if (state == 1 && !hovered)
            {
                plate.SetPropertyBlock(null);
                return;
            }
            Color c = state == 2 ? SelectedColor : state == 0 ? EmptyColor : HoverColor;
            if (hovered && state != 1) c *= 1.15f;
            block.SetColor(BaseColorId, c);
            plate.SetPropertyBlock(block);
        }
    }
}
