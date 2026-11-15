using UnityEngine;
using SpellSimulator.Interfaces;
using SpellSimulator.Net;
using SpellSimulator.Player;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Placa «−» o «+» de una perilla del laboratorio. Se aprieta apuntando y haciendo clic (gatillo en VR), como los botones
    /// del grimorio: sube o baja un paso la variable en <see cref="LabParameters"/>. El collider es un trigger chico que
    /// dimensiona el generador (separado ≥ 6 cm de la otra placa por el temblor del rayo en VR).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class LabStepPlate : MonoBehaviour, IPressable, LabParameters.IHoverTarget
    {
        [SerializeField, Tooltip("Variable que ajusta esta placa.")]
        private LabVariableId variable = LabVariableId.Mass;
        [SerializeField, Tooltip("+1 sube un paso, -1 lo baja.")]
        private int dir = 1;
        [SerializeField, Tooltip("Renderer que se resalta al apuntar (vacio = el primero en los hijos).")]
        private Renderer highlight;

        private string prompt;
        private HoverTint tint;

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            if (highlight == null) highlight = GetComponentInChildren<Renderer>();
            tint.Init(highlight);
            prompt = LabCatalog.Get(variable).Label + (dir >= 0 ? ": subir" : ": bajar");
        }

        public bool PressWithClick => true;

        public string GetPressPrompt() => prompt ?? "";

        public void Press()
        {
            if (NetSession.Exists)
            {
                Hud.Notify("lab", "Laboratorio: solo en modo Libre", 2.5f);
                return;
            }
            LabParameters parameters = LabParameters.Current;
            if (parameters != null) parameters.Step(variable, dir >= 0 ? 1 : -1);
        }

        public void SetHovered(bool on) => tint.Set(on);

        /// <summary>
        /// Resaltado al apuntar compartido por las piezas del laboratorio: aclara el color base del renderer con un
        /// MaterialPropertyBlock (sin instanciar materiales). Solo cambia cuando cambia el estado.
        /// </summary>
        internal struct HoverTint
        {
            private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
            private static readonly int ColorId = Shader.PropertyToID("_Color");
            private static readonly Color HoverGlow = new Color(1f, 0.84f, 0.42f);

            private Renderer renderer;
            private MaterialPropertyBlock block;
            private int propertyId;
            private Color hoverColor;

            public void Init(Renderer target)
            {
                renderer = target;
                if (renderer == null) return;
                Material m = renderer.sharedMaterial;
                Color baseColor = Color.white;
                propertyId = BaseColorId;
                if (m != null)
                {
                    if (m.HasProperty(BaseColorId)) baseColor = m.GetColor(BaseColorId);
                    else if (m.HasProperty(ColorId)) { propertyId = ColorId; baseColor = m.GetColor(ColorId); }
                }
                hoverColor = Color.Lerp(baseColor, HoverGlow, 0.55f);
                block = new MaterialPropertyBlock();
            }

            public void Set(bool on)
            {
                if (renderer == null) return;
                if (!on)
                {
                    renderer.SetPropertyBlock(null);
                    return;
                }
                block.SetColor(propertyId, hoverColor);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
