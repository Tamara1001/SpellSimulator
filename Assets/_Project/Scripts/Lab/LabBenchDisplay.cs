using System;
using System.Text;
using TMPro;
using UnityEngine;
using SpellSimulator.Audio;
using SpellSimulator.Compiler;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Pizarra del banco de sellos (Docs/LABORATORIO_V3_PLAN.md, (c).2–3 y (b)):
    /// - «Vas a lanzar»: resumen del sello (<see cref="LabSealSummary"/>),
    ///   rearmado solo cuando cambia una variable (<see cref="LabSealBench.VarChanged"/>);
    /// - banda de resultado del ultimo lanzamiento (<see cref="LabSealBench.UltimoResultado"/>): verde valido / roja invalido con
    ///   elemento, calidad % y motivo;
    /// - espejo en el atril cuando el papel real esta en la pista (<see cref="LabSealBench.PaperAtEasel"/> falso): muestra la
    ///   textura del lienzo de prueba con una flecha emisiva hacia <see cref="LabSealBench.PaperEmissionWorld"/>; otra flecha
    ///   acompaña al papel real.
    /// Sin allocations por frame: textos solo al cambiar, poses de flechas a <see cref="PollHz"/> y sin GetComponent en Update.
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

        [Header("Pizarra")]
        [SerializeField, Tooltip("Texto del resumen «Vas a lanzar».")]
        private TMP_Text summaryText;
        [SerializeField, Tooltip("Texto de la banda de resultado.")]
        private TMP_Text resultText;
        [SerializeField, Tooltip("Banda de resultado (malla clara: se tiñe con un MaterialPropertyBlock).")]
        private Renderer resultBand;

        [Header("Espejo del atril")]
        [SerializeField, Tooltip("Ancla del espejo en el atril: centro del papel, +Z hacia la pista, +Y arriba.")]
        private Transform mirrorAnchor;
        [SerializeField, Tooltip("Renderer del espejo (misma malla que el papel; el material del lienzo se toma en runtime).")]
        private Renderer mirrorRenderer;
        [SerializeField, Tooltip("Flecha emisiva del espejo (+Z = punta).")]
        private Transform easelArrow;
        [SerializeField, Tooltip("Flecha emisiva que acompaña al papel real (+Z = punta).")]
        private Transform paperArrow;
        [SerializeField, Tooltip("Distancia (m) del centro del papel al comienzo de la flecha que lo acompaña.")]
        private float paperArrowOffset = 0.05f;

        private readonly StringBuilder sb = new StringBuilder(256);
        private Func<string, string> nameOf;
        private MaterialPropertyBlock block;
        private int bandColorId;
        private bool subscribed;
        private bool summaryDirty = true, resultDirty = true;
        private float nextPoll;
        private int mirrorState = -1; // -1 sin mirar, 0 oculto, 1 visible
        private Vector3 shownEmission, shownPaperPosition, shownPaperScale;
        private Renderer paperRenderer;

        private void Awake()
        {
            nameOf = NameOf;
            block = new MaterialPropertyBlock();
            bandColorId = BaseColorId;
            if (resultBand != null && resultBand.sharedMaterial != null && !resultBand.sharedMaterial.HasProperty(BaseColorId)
                && resultBand.sharedMaterial.HasProperty(ColorId)) bandColorId = ColorId;
            if (mirrorRenderer != null) mirrorRenderer.enabled = false;
            if (easelArrow != null) easelArrow.gameObject.SetActive(false);
        }

        private void OnEnable() => Subscribe();

        private void Start()
        {
            Subscribe();
            summaryDirty = resultDirty = true;
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy() => Unsubscribe();

        private void Update()
        {
            if (bench == null) return;
            if (summaryDirty) RefreshSummary();
            if (resultDirty) RefreshResult();
            if (Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + 1f / PollHz;
            RefreshPaper();
        }

        // ---------------------------------------------------------------- eventos del banco

        private void Subscribe()
        {
            if (subscribed) return;
            if (bench == null) bench = LabSealBench.Current;
            if (bench == null) return;
            bench.VarChanged += OnVarChanged;
            bench.Launched += OnLaunched;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || bench == null) return;
            bench.VarChanged -= OnVarChanged;
            bench.Launched -= OnLaunched;
            subscribed = false;
        }

        private void OnVarChanged(LabSealVarId id, float previous, float next)
        {
            summaryDirty = true;
            // La pose del papel cambia en el proximo redibujo del banco: mirar pronto.
            if (id == LabSealVarId.PaperPose || id == LabSealVarId.PaperFace || id == LabSealVarId.PaperTilt || id == LabSealVarId.PaperSize)
                nextPoll = 0f;
        }

        private void OnLaunched(SpellIR spell)
        {
            resultDirty = true;
            AudioDirector director = AudioDirector.Instance;
            if (director != null) director.Play(ResultCue, transform.position, 0.8f);
        }

        // ---------------------------------------------------------------- pizarra

        private void RefreshSummary()
        {
            summaryDirty = false;
            if (summaryText == null) return;
            LabSealSpec spec = bench.Spec;
            LabSealSummary.Build(spec, sb, nameOf);
            summaryText.SetText(sb);
        }

        private void RefreshResult()
        {
            resultDirty = false;
            LabSealResult r = bench.UltimoResultado;
            sb.Clear();
            Color band;
            if (!r.HasResult)
            {
                sb.Append("Todavía no lanzaste\nApretá LANZAR");
                band = IdleColor;
            }
            else
            {
                sb.Append(r.Valid ? "VÁLIDO" : "INVÁLIDO");
                if (r.Valid) sb.Append(" · ").Append(ElementName(r.Element));
                sb.Append(" · calidad ").Append(Mathf.RoundToInt(r.QualityPercent)).Append(" %");
                sb.Append('\n').Append(string.IsNullOrEmpty(r.Reason) ? (r.Valid ? "Listo" : "Sin motivo") : r.Reason);
                band = r.Valid ? ValidColor : InvalidColor;
            }
            if (resultText != null) resultText.SetText(sb);
            if (resultBand != null)
            {
                block.SetColor(bandColorId, band);
                resultBand.SetPropertyBlock(block);
            }
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

        /// <summary>Nombre visible de una receta o de un signo (solo al rearmar el resumen, no por frame).</summary>
        private string NameOf(string id)
        {
            var recipes = bench.RecipeIds;
            for (int i = 0; i < recipes.Count; i++)
                if (recipes[i] == id) return bench.FormatVar(LabSealVarId.BaseRecipe, i);
            var signs = bench.SignIds;
            for (int i = 0; i < signs.Count; i++)
                if (signs[i] == id) return bench.FormatVar(LabSealVarId.SignType, i);
            return null;
        }

        // ---------------------------------------------------------------- espejo y flechas

        private void RefreshPaper()
        {
            Vector3 emission = bench.PaperEmissionWorld;
            Transform paper = bench.PaperTransform;

            // Flecha del papel real: sale del centro del papel hacia donde sale el hechizo.
            if (paperArrow != null && paper != null)
            {
                Vector3 at = paper.position + emission * paperArrowOffset;
                if (emission != shownEmission || at != shownPaperPosition)
                {
                    paperArrow.SetPositionAndRotation(at, Look(emission));
                    shownPaperPosition = at;
                }
            }

            int state = bench.PaperAtEasel ? 0 : 1;
            if (state != mirrorState)
            {
                mirrorState = state;
                if (paperRenderer == null && bench.Canvas != null) paperRenderer = bench.Canvas.GetComponent<Renderer>();
                if (mirrorRenderer != null)
                {
                    // El material del lienzo es una instancia creada en runtime (DrawingCanvas): se toma al mostrar.
                    if (state == 1 && paperRenderer != null) mirrorRenderer.sharedMaterial = paperRenderer.sharedMaterial;
                    mirrorRenderer.enabled = state == 1;
                }
                if (easelArrow != null) easelArrow.gameObject.SetActive(state == 1);
                shownEmission = Vector3.zero; // fuerza la pose
            }

            if (state == 1)
            {
                if (mirrorRenderer != null && bench.Canvas != null)
                {
                    Transform canvas = bench.Canvas.transform;
                    Vector3 scale = canvas.lossyScale;
                    if (scale != shownPaperScale)
                    {
                        mirrorRenderer.transform.localScale = scale;
                        shownPaperScale = scale;
                    }
                    if (mirrorAnchor != null && emission != shownEmission)
                        // La cara visible del espejo mira al jugador (−Z del ancla), como el papel en el atril.
                        mirrorRenderer.transform.rotation = LabSealBenchMath.FaceRotation(bench.Canvas.SurfaceNormalLocal, -mirrorAnchor.forward, mirrorAnchor.up);
                }
                if (easelArrow != null && emission != shownEmission) easelArrow.rotation = Look(emission);
            }
            shownEmission = emission;
        }

        private static Quaternion Look(Vector3 direction)
        {
            if (direction.sqrMagnitude < 1e-6f) return Quaternion.identity;
            Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            return Quaternion.LookRotation(direction, up);
        }
    }
}
