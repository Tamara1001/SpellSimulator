using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SpellSimulator.Lab
{
    /// <summary>Pieza del laboratorio con una ayuda propia para la tira de ayuda (LabHelpStrip): titulo y explicacion en dos renglones.</summary>
    public interface ILabHelp
    {
        /// <summary>Texto ya armado (LabHelpStrip.Line), cacheado por la pieza.</summary>
        string HelpLine { get; }
    }

    /// <summary>
    /// Tira de ayuda en el mundo de una consola del laboratorio (Docs/LABORATORIO_V3_PLAN.md, (c).6): muestra la ayuda corta
    /// (<see cref="LabSealVarDef.Help"/>) del control apuntado o agarrado. Va en el mundo porque la HUD de UI Toolkit no se ve
    /// en VR (KI-095). Los controles y botones avisan con <see cref="Focus"/>/<see cref="Unfocus"/>; cada tira solo muestra lo
    /// que cuelga de su <see cref="scope"/> (su consola). Sin allocations por frame: reescribe el texto solo cuando cambia el
    /// foco, y el texto de cada control se arma una sola vez y queda cacheado.
    /// </summary>
    public class LabHelpStrip : MonoBehaviour
    {
        /// <summary>Color del renglon de titulo (grupo y variable).</summary>
        private const string TitleColor = "#FFD173";

        [SerializeField, Tooltip("Texto de la tira.")]
        private TMP_Text text;
        [SerializeField, Tooltip("Consola cuyos controles muestra esta tira (vacio = cualquiera).")]
        private Transform scope;
        [SerializeField, Tooltip("Texto cuando no se apunta ningun control de la consola."), TextArea]
        private string idleText = "Apuntá un control para ver qué hace.";

        private static Component focused;
        private static int focusVersion;
        private static readonly Dictionary<int, string> HelpCache = new Dictionary<int, string>(48);

        private int seenVersion = -1;
        private Component shown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            focused = null;
            focusVersion = 0;
            HelpCache.Clear();
        }

        /// <summary>Control o boton que paso a estar apuntado o agarrado (lo llaman LabControl y LabBenchButton).</summary>
        public static void Focus(Component target)
        {
            if (ReferenceEquals(focused, target)) return;
            focused = target;
            focusVersion++;
        }

        /// <summary>Deja de mostrar <paramref name="target"/> si era el que tenia el foco.</summary>
        public static void Unfocus(Component target)
        {
            if (!ReferenceEquals(focused, target)) return;
            focused = null;
            focusVersion++;
        }

        /// <summary>Lo llama el generador de escena (Editor) despues de AddComponent.</summary>
        public void Configure(TMP_Text text, Transform scope, string idleText)
        {
            this.text = text;
            this.scope = scope;
            if (!string.IsNullOrEmpty(idleText)) this.idleText = idleText;
        }

        private void Awake()
        {
            if (text == null) text = GetComponent<TMP_Text>();
        }

        private void OnEnable() => seenVersion = -1;

        private void Update()
        {
            if (seenVersion == focusVersion || text == null) return;
            seenVersion = focusVersion;

            // El foco puede ser un objeto destruido (Unity null): se trata como sin foco.
            Component target = focused != null && (scope == null || focused.transform.IsChildOf(scope)) ? focused : null;
            if (ReferenceEquals(target, shown) && target != null) return;
            shown = target;
            text.SetText(target != null ? HelpFor(target) : idleText);
        }

        /// <summary>Texto de ayuda de un control o boton (cacheado por instancia: se arma una vez).</summary>
        private static string HelpFor(Component target)
        {
            int key = target.GetInstanceID();
            if (HelpCache.TryGetValue(key, out string cached)) return cached;

            string help;
            if (target is ILabHelp custom) help = custom.HelpLine;
            else if (target.TryGetComponent(out LabControlBinding binding)) help = BindingHelp(binding);
            else if (target is LabControl control) help = control.HelpText;
            else help = target.name;
            HelpCache[key] = help;
            return help;
        }

        private static string BindingHelp(LabControlBinding binding)
        {
            if (binding.Source == LabControlSource.Seal)
            {
                LabSealVarDef def = LabSealCatalog.Get((LabSealVarId)binding.Variable);
                return Line(LabSealCatalog.GroupName(def.Group) + " · " + def.Label, def.Help);
            }
            if (binding.Source == LabControlSource.Field)
            {
                var v = (LabFieldVar)binding.Variable;
                LabSealVarDef def = LabSealCatalog.Get(LabField.SealVar(v));
                bool boxToo = v == LabFieldVar.X || v == LabFieldVar.Distance || v == LabFieldVar.Yaw;
                return Line("Objeto elegido · " + def.Label + (boxToo ? "" : " (solo papel)"), def.Help);
            }
            LabVariableDef env = LabCatalog.Get((LabVariableId)binding.Variable);
            return Line(env.Label, "Doble clic: valor por defecto · Rueda: un paso.");
        }

        /// <summary>Dos renglones: titulo dorado y la ayuda.</summary>
        public static string Line(string title, string help)
            => "<color=" + TitleColor + ">" + title + "</color>\n" + (help ?? string.Empty);
    }
}
