using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace SpellSimulator.Lab
{
    /// <summary>De donde lee y escribe un control del laboratorio.</summary>
    public enum LabControlSource { Seal = 0, Environment = 1 }

    /// <summary>
    /// Enlace serializable de un <see cref="LabControl"/> con su variable (lo arma el generador de escena, MapLab). Los
    /// delegados de <see cref="LabControl.Setup"/> no se serializan, por eso el enlace se hace en runtime (Start): con el banco
    /// de sellos (<see cref="LabSealBench"/>, variables <see cref="LabSealVarId"/>) o con la mesa de entorno
    /// (<see cref="LabParameters"/>, variables <see cref="LabVariableId"/>). Los rangos de las listas (recetas, sigilos, signos)
    /// salen del banco al enlazar. Sin allocations por frame: los delegados se crean una sola vez.
    /// </summary>
    [RequireComponent(typeof(LabControl))]
    public class LabControlBinding : MonoBehaviour
    {
        [SerializeField, Tooltip("Banco de sellos o mesa de entorno.")]
        private LabControlSource source;
        [SerializeField, Tooltip("(int)LabSealVarId si source = Seal; (int)LabVariableId si source = Environment.")]
        private int variable;
        [SerializeField, Tooltip("Perilla, deslizador o selector.")]
        private LabControlKind kind;
        [SerializeField, Tooltip("Pieza que gira (perilla/selector) o se desliza (deslizador).")]
        private Transform movable;
        [SerializeField, Tooltip("Extremos del riel del deslizador.")]
        private Transform railStart, railEnd;
        [SerializeField, Tooltip("Lectura del valor.")]
        private TMP_Text readout;
        [SerializeField, Tooltip("Banco de sellos (vacio = LabSealBench.Current).")]
        private LabSealBench bench;

        private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

        private LabControl control;

        /// <summary>Fuente de la variable.</summary>
        public LabControlSource Source => source;
        /// <summary>Variable enlazada (ver <see cref="Source"/>).</summary>
        public int Variable => variable;
        /// <summary>El control manipulable.</summary>
        public LabControl Control => control != null ? control : (control = GetComponent<LabControl>());
        /// <summary>True cuando ya se llamo a <see cref="LabControl.Setup"/>.</summary>
        public bool Bound { get; private set; }

        /// <summary>Lo llama el generador de escena (Editor) despues de AddComponent.</summary>
        public void Configure(LabControlSource source, int variable, LabControlKind kind, Transform movable, Transform railStart,
            Transform railEnd, TMP_Text readout, LabSealBench bench)
        {
            this.source = source;
            this.variable = variable;
            this.kind = kind;
            this.movable = movable;
            this.railStart = railStart;
            this.railEnd = railEnd;
            this.readout = readout;
            this.bench = bench;
        }

        private void Start() => Bind();

        /// <summary>Enlaza el control con su variable. Devuelve false si falta el banco o la mesa.</summary>
        public bool Bind()
        {
            LabControl c = Control;
            if (c == null) return false;
            Bound = source == LabControlSource.Seal ? BindSeal(c) : BindEnvironment(c);
            if (!Bound) Debug.LogWarning($"[Laboratorio] {name}: no se pudo enlazar el control ({source} {variable}).", this);
            return Bound;
        }

        // ---------------------------------------------------------------- banco de sellos

        private bool BindSeal(LabControl c)
        {
            LabSealBench b = bench != null ? bench : LabSealBench.Current;
            if (b == null) return false;
            bench = b;
            var id = (LabSealVarId)variable;
            LabSealVarDef def = LabSealCatalog.Get(id);
            float min = def.Min, max = def.Max;
            switch (id)
            {
                case LabSealVarId.BaseRecipe: min = 0f; max = b.RecipeIds.Count - 1; break;
                case LabSealVarId.Sigil: min = -1f; max = b.SigilIds.Count - 1; break;
                case LabSealVarId.SignType: min = -1f; max = b.SignIds.Count - 1; break;
                case LabSealVarId.PaperSize: min = 0f; max = LabSealSpec.PaperSizes.Length - 1; break;
                case LabSealVarId.Ink: min = 0f; max = LabSealBenchMath.InkNames.Length - 1; break;
                case LabSealVarId.PenWidth: min = 0f; max = LabSealSpec.PenWidthCount - 1; break;
            }
            bool isList = def.Kind == LabSealVarKind.List;
            float step = isList ? 1f : def.Step;
            int detents = 0;
            if (def.Kind != LabSealVarKind.Continuous && step > 0f) detents = Mathf.RoundToInt((max - min) / step) + 1;

            Func<float> get = () => b.GetVar(id);
            Action<float> set = v =>
            {
                // Las listas solo se escriben al cambiar de indice (la receta se recarga entera en cada SetVar).
                if (isList && Mathf.RoundToInt(v) == Mathf.RoundToInt(b.GetVar(id))) return;
                b.SetVar(id, v);
            };
            Func<float, string> format = v => b.FormatVar(id, v);
            c.Setup(kind, def.Label, get, set, format, min, max, detents, movable, railStart, railEnd, readout);
            if (!isList) c.SetResetValue(def.Default);
            return true;
        }

        // ---------------------------------------------------------------- mesa de entorno

        private bool BindEnvironment(LabControl c)
        {
            LabParameters p = LabParameters.Current;
            if (p == null) return false;
            var id = (LabVariableId)variable;
            LabVariableDef def = LabCatalog.Get(id);
            Func<float> get;
            Action<float> set;
            Func<float, string> format;
            float min, max;
            int detents;
            if (def.HasSteps)
            {
                // Tabla discreta (masa en escala logaritmica): el control recorre indices.
                float[] steps = def.Steps;
                min = 0f;
                max = steps.Length - 1;
                detents = steps.Length;
                get = () => NearestIndex(steps, p.Get(id));
                set = v => p.Set(id, steps[Mathf.Clamp(Mathf.RoundToInt(v), 0, steps.Length - 1)]);
                format = v => Format(def, steps[Mathf.Clamp(Mathf.RoundToInt(v), 0, steps.Length - 1)]);
            }
            else
            {
                min = def.Min;
                max = def.Max;
                // Muescas solo si el default cae en la grilla (el debilitamiento 0,78 no: queda continuo y LabParameters ajusta).
                detents = 0;
                if (def.Step > 0f)
                {
                    float k = (def.Default - min) / def.Step;
                    if (Mathf.Abs(k - Mathf.Round(k)) < 1e-3f) detents = Mathf.RoundToInt((max - min) / def.Step) + 1;
                }
                get = () => p.Get(id);
                set = v => p.Set(id, v);
                format = v => Format(def, v);
            }
            c.Setup(kind, def.Label, get, set, format, min, max, detents, movable, railStart, railEnd, readout);
            c.SetResetValue(def.HasSteps ? NearestIndex(def.Steps, def.Default) : def.Default);
            return true;
        }

        private static int NearestIndex(float[] steps, float value)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < steps.Length; i++)
            {
                float d = Mathf.Abs(steps[i] - value);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        private static string Format(LabVariableDef def, float value)
        {
            string number = value.ToString(def.Format, Spanish);
            return string.IsNullOrEmpty(def.Unit) ? number : number + " " + def.Unit;
        }
    }
}
