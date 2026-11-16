using System;
using System.Text;
using TMPro;
using UnityEngine;
using SpellSimulator.Audio;
using SpellSimulator.Casting;
using SpellSimulator.Compiler;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Textos del banco (Laboratorio v4, puntos 5, 8 y 10):
    /// - cartel «Vas a lanzar» (se ve siempre): una fila por dato con el rotulo en el color de su grupo
    ///   (<see cref="LabSealSummary.BuildRow"/>), rearmado solo cuando cambia una variable o el dibujo del atril;
    /// - datos de reconocimiento SOLO CON LENTES (los oculta/muestra <see cref="LabLensGate"/>, aca solo se escribe el texto):
    ///   banda de resultado del ultimo lanzamiento (valido/invalido, elemento, calidad %, motivo, estado previsto) y lectura del
    ///   atril de dibujo (trazos y si el anillo esta cerrado);
    /// - flecha emisiva que acompaña al papel del campo hacia donde sale el hechizo.
    /// Sin allocations por frame: textos solo al cambiar, pose de la flecha a <see cref="PollHz"/>.
    /// </summary>
    public class LabBenchDisplay : MonoBehaviour
    {
        /// <summary>Veces por segundo que se mira la pose del papel (el banco la cambia como mucho a 10 Hz).</summary>
        public const float PollHz = 10f;
        /// <summary>Sonido al recibir un resultado (cue de la biblioteca de audio; si no existe, no suena).</summary>
        private const string ResultCue = "ui_select";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color ValidColor = new Color(0.25f, 0.68f, 0.3f);
        private static readonly Color InvalidColor = new Color(0.82f, 0.22f, 0.18f);
        private static readonly Color IdleColor = new Color(0.32f, 0.3f, 0.36f);

        [Header("Dependencias")]
        [SerializeField, Tooltip("Banco de sellos (vacio = LabSealBench.Current).")]
        private LabSealBench bench;

        [Header("Cartel «Vas a lanzar»")]
        [SerializeField, Tooltip("Una fila por dato (LabSealSummary.RowCount), de arriba hacia abajo.")]
        private TMP_Text[] rowTexts;

        [Header("Solo con lentes")]
        [SerializeField, Tooltip("Texto de la banda de resultado.")]
        private TMP_Text resultText;
        [SerializeField, Tooltip("Banda de resultado (malla clara: se tiñe con un MaterialPropertyBlock).")]
        private Renderer resultBand;
        [SerializeField, Tooltip("Lectura del atril de dibujo (trazos, anillo).")]
        private TMP_Text padInfoText;
        [SerializeField, Tooltip("Lanzador (para saber si el anillo del dibujo cierra). Vacio = el de la escena.")]
        private SpellCaster caster;

        [Header("Flecha del papel")]
        [SerializeField, Tooltip("Flecha emisiva que acompaña al papel (+Z = punta).")]
        private Transform paperArrow;
        [SerializeField, Tooltip("Distancia (m) del centro del papel al comienzo de la flecha.")]
        private float paperArrowOffset = 0.05f;

        private readonly StringBuilder sb = new StringBuilder(256);
        private readonly StringBuilder row = new StringBuilder(96);
        private Func<string, string> nameOf;
        private MaterialPropertyBlock block;
        private int bandColorId;
        private bool subscribed;
        private LabSketchPad subscribedPad;
        private bool summaryDirty = true, resultDirty = true, padDirty = true;
        private float nextPoll;
        private Vector3 shownEmission, shownPaperPosition;
        private string[] rowPrefixes;

        private void Awake()
        {
            nameOf = NameOf;
            block = new MaterialPropertyBlock();
            bandColorId = BaseColorId;
            if (resultBand != null && resultBand.sharedMaterial != null && !resultBand.sharedMaterial.HasProperty(BaseColorId)
                && resultBand.sharedMaterial.HasProperty(ColorId)) bandColorId = ColorId;
            // Rotulo de cada fila en el color oscuro de su grupo (se arma una vez).
            rowPrefixes = new string[LabSealSummary.RowCount];
            for (int i = 0; i < rowPrefixes.Length; i++)
                rowPrefixes[i] = "<b><color=#" + ColorUtility.ToHtmlStringRGB(LabPalette.Dark(LabPalette.Group(LabSealSummary.RowGroup(i)))) + ">"
                                 + LabSealSummary.RowLabel(i) + "</color></b>   ";
        }

        private void OnEnable() => Subscribe();

        private void Start()
        {
            Subscribe();
            if (caster == null) caster = FindFirstObjectByType<SpellCaster>();
            summaryDirty = resultDirty = padDirty = true;
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy() => Unsubscribe();

        private void Update()
        {
            if (bench == null) return;
            if (summaryDirty) RefreshSummary();
            if (resultDirty) RefreshResult();
            if (padDirty) RefreshPad();
            if (Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + 1f / PollHz;
            RefreshArrow();
        }

        // ---------------------------------------------------------------- eventos

        private void Subscribe()
        {
            if (subscribed) return;
            if (bench == null) bench = LabSealBench.Current;
            if (bench == null) return;
            bench.VarChanged += OnVarChanged;
            bench.Launched += OnLaunched;
            subscribedPad = bench.SketchPad;
            if (subscribedPad != null) subscribedPad.Changed += OnPadChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || bench == null) return;
            bench.VarChanged -= OnVarChanged;
            bench.Launched -= OnLaunched;
            if (subscribedPad != null) subscribedPad.Changed -= OnPadChanged;
            subscribedPad = null;
            subscribed = false;
        }

        private void OnVarChanged(LabSealVarId id, float previous, float next)
        {
            summaryDirty = true;
            resultDirty = true; // el estado previsto depende del hueco del anillo
            nextPoll = 0f;
        }

        private void OnPadChanged()
        {
            summaryDirty = true;
            padDirty = true;
        }

        private void OnLaunched(SpellIR spell)
        {
            resultDirty = true;
            AudioDirector director = AudioDirector.Instance;
            if (director != null) director.Play(ResultCue, transform.position, 0.8f);
        }

        // ---------------------------------------------------------------- cartel

        private void RefreshSummary()
        {
            summaryDirty = false;
            if (rowTexts == null) return;
            LabSealSpec spec = bench.Spec;
            bool drawing = bench.UsesDrawing;
            for (int i = 0; i < rowTexts.Length && i < LabSealSummary.RowCount; i++)
            {
                if (rowTexts[i] == null) continue;
                LabSealSummary.BuildRow(spec, i, row, nameOf, drawing);
                sb.Clear();
                sb.Append(rowPrefixes[i]).Append(row);
                rowTexts[i].SetText(sb);
            }
        }

        // ---------------------------------------------------------------- solo con lentes

        private void RefreshResult()
        {
            resultDirty = false;
            LabSealResult r = bench.UltimoResultado;
            sb.Clear();
            Color band;
            if (!r.HasResult)
            {
                sb.Append("Todavía no lanzaste · Apretá LANZAR");
                band = IdleColor;
            }
            else
            {
                sb.Append(r.Valid ? "VÁLIDO" : "INVÁLIDO");
                if (r.Valid) sb.Append(" · ").Append(ElementName(r.Element));
                sb.Append(" · calidad ").Append(Mathf.RoundToInt(r.QualityPercent)).Append(" %");
                sb.Append(bench.LastLaunchFromDrawing ? " · dibujo" : " · controles");
                sb.Append('\n').Append(string.IsNullOrEmpty(r.Reason) ? (r.Valid ? "Listo" : "Sin motivo") : r.Reason);
                band = r.Valid ? ValidColor : InvalidColor;
            }
            sb.Append("\nPrevisto con los controles: ").Append(LabSealSummary.Prediction(bench.Spec));
            if (resultText != null) resultText.SetText(sb);
            if (resultBand != null)
            {
                block.SetColor(bandColorId, band);
                resultBand.SetPropertyBlock(block);
            }
        }

        private void RefreshPad()
        {
            padDirty = false;
            if (padInfoText == null) return;
            LabSketchPad pad = bench.SketchPad;
            sb.Clear();
            if (pad == null || !pad.HasDrawing) sb.Append("Atril vacío · LANZAR usa los controles");
            else
            {
                int strokes = pad.Canvas.Session.Strokes.Count;
                bool closed = caster != null && caster.IsRingClosed(pad.Canvas.Session, pad.Canvas.WorldSize);
                sb.Append(strokes).Append(strokes == 1 ? " trazo" : " trazos").Append(" · anillo ").Append(closed ? "cerrado" : "abierto");
            }
            padInfoText.SetText(sb);
        }

        private static string ElementName(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return "fuego";
                case ElementType.Water: return "agua";
                case ElementType.Wind: return "viento";
                case ElementType.Earth: return "tierra";
                case ElementType.Light: return "luz";
                default: return "sin elemento";
            }
        }

        /// <summary>Nombre visible de una receta, un sigilo o un signo (solo al rearmar el cartel, no por frame).</summary>
        private string NameOf(string id)
        {
            var recipes = bench.RecipeIds;
            for (int i = 0; i < recipes.Count; i++)
                if (recipes[i] == id) return bench.FormatVar(LabSealVarId.BaseRecipe, i);
            return bench.GlyphDisplayName(id);
        }

        // ---------------------------------------------------------------- flecha

        private void RefreshArrow()
        {
            if (paperArrow == null) return;
            Transform paper = bench.PaperTransform;
            if (paper == null) return;
            Vector3 emission = bench.PaperEmissionWorld;
            Vector3 at = bench.Canvas.WorldCenter + emission * paperArrowOffset;
            if (emission == shownEmission && at == shownPaperPosition) return;
            paperArrow.SetPositionAndRotation(at, Look(emission));
            shownEmission = emission;
            shownPaperPosition = at;
        }

        private static Quaternion Look(Vector3 direction)
        {
            if (direction.sqrMagnitude < 1e-6f) return Quaternion.identity;
            Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            return Quaternion.LookRotation(direction, up);
        }
    }
}
