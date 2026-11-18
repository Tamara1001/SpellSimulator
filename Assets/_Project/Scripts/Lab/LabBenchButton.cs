using UnityEngine;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Lab
{
    /// <summary>Que hace un boton del banco de sellos. Los valores no se renumeran (estan serializados en la escena).</summary>
    public enum LabBenchAction { Launch = 0, ResetSeal = 1, ResetGroup = 2, ClearDrawing = 3, DeleteSelected = 4, DeleteAll = 5, ClearEffects = 6 }

    /// <summary>
    /// Boton del banco de sellos (Laboratorio v2/v3): «Lanzar» llama a <see cref="LabSealBench.Launch"/>, «Por defecto» a
    /// <see cref="LabSealBench.ResetSpec"/> y la pestaña de color de cada grupo de la consola a
    /// <see cref="LabSealBench.ResetGroup"/>. Se aprieta apuntando + clic (gatillo en VR). El bloqueo en red lo decide el banco;
    /// el lanzamiento tiene un enfriamiento corto para que un doble clic no dispare dos hechizos.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class LabBenchButton : MonoBehaviour, IPressable, LabParameters.IHoverTarget, ILabHelp
    {
        /// <summary>Segundos minimos entre dos lanzamientos.</summary>
        private const float LaunchCooldown = 0.6f;

        [SerializeField, Tooltip("Lanzar el sello, volver al sello por defecto o reiniciar un grupo de la consola.")]
        private LabBenchAction action;
        [SerializeField, Tooltip("Grupo de la consola que reinicia (1..4; solo con ResetGroup).")]
        private int group = 1;
        [SerializeField, Tooltip("Banco de sellos (vacio = LabSealBench.Current).")]
        private LabSealBench bench;
        [SerializeField, Tooltip("Renderer que se resalta al apuntar (vacio = el primero en los hijos).")]
        private Renderer highlight;

        private LabControl.HoverTint tint;
        private float nextPress;
        private string prompt;
        private string helpLine;

        /// <summary>Que hace el boton.</summary>
        public LabBenchAction Action => action;
        /// <summary>Grupo que reinicia (solo con <see cref="LabBenchAction.ResetGroup"/>).</summary>
        public int Group => group;

        /// <summary>Texto para la tira de ayuda de la consola (cacheado).</summary>
        public string HelpLine
        {
            get
            {
                if (helpLine != null) return helpLine;
                switch (action)
                {
                    case LabBenchAction.Launch:
                        helpLine = LabHelpStrip.Line("LANZAR", "Lanza tu dibujo del atril o, si está vacío, el sello armado.");
                        break;
                    case LabBenchAction.ResetSeal:
                        helpLine = LabHelpStrip.Line("Reiniciar sello", "Vuelve todos los controles del sello a la receta base.");
                        break;
                    case LabBenchAction.ClearDrawing:
                        helpLine = LabHelpStrip.Line("Borrar dibujo", "Limpia el atril: LANZAR vuelve a usar los controles.");
                        break;
                    case LabBenchAction.DeleteSelected:
                        helpLine = LabHelpStrip.Line("Borrar elegido", "Saca del campo la caja elegida (el papel se queda).");
                        break;
                    case LabBenchAction.ClearEffects:
                        helpLine = LabHelpStrip.Line("Limpiar efectos", "Saca los efectos de hechizos: rocas, fuego, agua, marcas. Cajas y papel se quedan.");
                        break;
                    case LabBenchAction.DeleteAll:
                        helpLine = LabHelpStrip.Line("Borrar todo", "Saca cajas y viento del campo. El papel se queda.");
                        break;
                    default:
                        helpLine = LabHelpStrip.Line(LabSealCatalog.GroupName(group) + " · Reiniciar",
                            "Vuelve las variables de este grupo a su valor inicial.");
                        break;
                }
                return helpLine;
            }
        }

        /// <summary>Lo llama el generador de escena (Editor) despues de AddComponent. <paramref name="group"/> solo vale con ResetGroup.</summary>
        public void Configure(LabBenchAction action, LabSealBench bench, Renderer highlight, int group = 1)
        {
            this.action = action;
            this.bench = bench;
            this.highlight = highlight;
            this.group = Mathf.Clamp(group, 1, LabSealCatalog.GroupCount);
            prompt = null;
            helpLine = null;
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

        public bool PressWithClick => true;

        public string GetPressPrompt()
        {
            if (prompt != null) return prompt;
            switch (action)
            {
                case LabBenchAction.Launch: prompt = "Lanzar el sello"; break;
                case LabBenchAction.ResetSeal: prompt = "Reiniciar sello"; break;
                case LabBenchAction.ClearDrawing: prompt = "Borrar el dibujo del atril"; break;
                case LabBenchAction.DeleteSelected: prompt = "Borrar el objeto elegido"; break;
                case LabBenchAction.DeleteAll: prompt = "Borrar todo lo del campo"; break;
case LabBenchAction.ClearEffects: prompt = "Limpiar los efectos de hechizos"; break;
                default: prompt = "Reiniciar " + LabSealCatalog.GroupName(group); break;
            }
            return prompt;
        }

        public void Press()
        {
            LabSealBench b = bench != null ? bench : LabSealBench.Current;
            switch (action)
            {
                case LabBenchAction.ClearDrawing:
                    if (b != null && b.SketchPad != null) b.SketchPad.Clear();
                    return;
                case LabBenchAction.DeleteSelected:
                    if (LabField.Current != null) LabField.Current.DeleteSelected();
                    return;
                case LabBenchAction.DeleteAll:
                    if (LabField.Current != null) LabField.Current.DeleteAll();
                    return;
                case LabBenchAction.ClearEffects:
                    LabEffects.Clear();
                    return;
            }
            if (b == null) return;
            switch (action)
            {
                case LabBenchAction.ResetSeal:
                    b.ResetSpec();
                    return;
                case LabBenchAction.ResetGroup:
                    b.ResetGroup(group);
                    return;
            }
            if (Time.unscaledTime < nextPress) return;
            nextPress = Time.unscaledTime + LaunchCooldown;
            b.Launch();
        }

        public void SetHovered(bool on)
        {
            tint.Set(on);
            if (on) LabHelpStrip.Focus(this);
            else LabHelpStrip.Unfocus(this);
        }
    }
}
