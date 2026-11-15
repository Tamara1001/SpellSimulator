using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Compiler;
using SpellSimulator.Core;
using SpellSimulator.DebugTools;
using SpellSimulator.Glyphs;
using SpellSimulator.Learning;
using SpellSimulator.Net;
using SpellSimulator.World;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Banco de sellos del Laboratorio v2 (Docs/LABORATORIO_V2_PLAN.md, «Cómo funciona»): guarda un <see cref="LabSealSpec"/>,
    /// lo sintetiza con <see cref="LabSealSynth.Build"/> sobre un lienzo de prueba (Clear + AddStroke + Redraw) y lo lanza con
    /// <see cref="SpellCaster.Cast"/> (no OnStrokeFinished: sin auto-lanzamiento ni gasto de tinta). El ONNX, el compilador y
    /// SpellIR corren de verdad; el registro y las mediciones lo toman de SpellCaster.SpellCompiled.
    /// Riesgo 2: el lienzo de prueba no se puede agarrar ni dibujar (se le apagan los colliders y el PhysicalCanvas), no gasta
    /// tinta, no se graba en recordSeals y en red el banco no hace nada (el lienzo queda vacio: no hay trazos que sincronizar).
    /// Sin allocations por frame: la vista previa se rehace solo cuando cambio algo, a lo sumo <see cref="PreviewHz"/> veces por segundo.
    /// </summary>
    public class LabSealBench : MonoBehaviour
    {
        [Header("Dependencias")]
        [SerializeField, Tooltip("Lienzo de prueba del atril (DrawingCanvas con emissionSide = InkSide, sin PhysicalCanvas).")]
        private DrawingCanvas canvas;
        [SerializeField, Tooltip("SpellCaster de la escena: lee y lanza el sello (su diccionario da las plantillas de los glifos).")]
        private SpellCaster caster;
        [SerializeField, Tooltip("Blanco del disparo (p. ej. el centro de la pista). Null = el papel conserva su orientacion.")]
        private Transform aim;

        [Header("Recetas")]
        [SerializeField, Tooltip("Hechizos (Data/Spells/*.json). Vacio = SpellCatalog.Shared (lo carga el grimorio); en el Editor, la carpeta.")]
        private TextAsset[] spellFiles;
        [SerializeField, Tooltip("Receta con que arranca el banco (gust = Ráfaga).")]
        private string initialRecipe = "gust";

        [Header("Pluma")]
        [SerializeField, Tooltip("Ancho del trazo (m) de cada grosor. Origen: Lapiz.asset strokeWidth 0,04 x presets fino/medio/normal/grueso de PhysicalTool.")]
        private float[] penWidthsMeters = { 0.008f, 0.018f, 0.04f, 0.088f };
        [SerializeField, Tooltip("Nombre de cada grosor.")]
        private string[] penWidthNames = { "fino", "medio", "normal", "grueso" };

        [Header("Vista previa")]
        [SerializeField, Tooltip("Redibuja el sello en el lienzo al cambiar una variable (sin lanzar).")]
        private bool autoPreview = true;

        /// <summary>Tope de redibujos de la vista previa por segundo (cada redibujo arma listas: no por frame).</summary>
        public const float PreviewHz = 10f;

        /// <summary>Banco activo (null si no hay laboratorio en la escena).</summary>
        public static LabSealBench Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = null;

        // Provisional: SpellCaster no expone recordSeals. Se apaga solo durante el Cast del banco. TODO: reemplazar por una API publica.
        private static readonly FieldInfo RecordSealsField =
            typeof(SpellCaster).GetField("recordSeals", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>Cambio de una variable: (id, valor anterior, valor nuevo). Solo si cambio. Al cargar una receta se emite una por variable que cambie.</summary>
        public event Action<LabSealVarId, float, float> VarChanged;
        /// <summary>Despues de cada <see cref="Launch"/>, con el hechizo leido (valido o no).</summary>
        public event Action<SpellIR> Launched;

        private LabSealSpec spec;
        private readonly List<string> recipeIds = new List<string>(24);
        private readonly List<SpellDef> recipeDefs = new List<SpellDef>(24);
        private readonly List<string> sigilIds = new List<string>(12);
        private readonly List<string> signIds = new List<string>(16);
        private readonly float[] scratchValues = new float[LabSealCatalog.Count];
        private Func<string, List<List<Vector2>>> templateOf;
        private GlyphDictionary glyphs;
        private Vector3 baseLocalScale = Vector3.one;
        private Vector2 baseWorldSize;
        private bool previewDirty;
        private float nextPreviewTime;
        private bool warnedRecording;
        private SpellCatalog triedShared;

        // ---------------------------------------------------------------- API

        /// <summary>Sello actual (ya recortado a rango).</summary>
        public LabSealSpec Spec => spec;
        /// <summary>Lienzo de prueba (lo usa el registro para reconocer los casts del banco).</summary>
        public DrawingCanvas Canvas => canvas;
        /// <summary>Recetas disponibles (ids de Data/Spells, ordenadas por dificultad) y «libre» al final.</summary>
        public IReadOnlyList<string> RecipeIds { get { EnsureRecipes(); return recipeIds; } }
        /// <summary>Sigilos del diccionario (orden de clase del modelo).</summary>
        public IReadOnlyList<string> SigilIds { get { EnsureGlyphs(); return sigilIds; } }
        /// <summary>Signos del diccionario (orden de clase del modelo).</summary>
        public IReadOnlyList<string> SignIds { get { EnsureGlyphs(); return signIds; } }
        /// <summary>True mientras <see cref="ApplyRecipe"/> emite los cambios de cada variable (el registro escribe solo la receta).</summary>
        public bool ApplyingRecipe { get; private set; }
        /// <summary>False si la receta cargada no entra exacta en el modelo del banco (signos mezclados o no repartidos parejo).</summary>
        public bool RecipeExact { get; private set; } = true;

        /// <summary>True si el ultimo lanzamiento compilo un hechizo valido.</summary>
        public bool LastCastValid { get; private set; }
        /// <summary>Elemento del ultimo lanzamiento (None si no fue valido).</summary>
        public ElementType LastElement { get; private set; }
        /// <summary>Calidad (0..1) del ultimo lanzamiento.</summary>
        public float LastQuality { get; private set; }
        /// <summary>Hechizo completo del ultimo lanzamiento.</summary>
        public SpellIR LastSpell { get; private set; }
        /// <summary>Lanzamientos hechos en esta sesion (lo usa el smoke).</summary>
        public int LaunchCount { get; private set; }

        /// <summary>Valor actual de una variable (listas: indice; sigilo/signo -1 = ninguno; papel: indice en PaperSizes).</summary>
        public float GetVar(LabSealVarId id)
        {
            EnsureRecipes();
            EnsureGlyphs();
            return LabSealBenchMath.Get(spec, id, recipeIds, sigilIds, signIds);
        }

        /// <summary>Fija una variable (recortada a rango; la receta base carga la receta). Emite <see cref="VarChanged"/> si cambia.</summary>
        public void SetVar(LabSealVarId id, float value)
        {
            if (id == LabSealVarId.BaseRecipe)
            {
                EnsureRecipes();
                if (recipeIds.Count == 0) return;
                ApplyRecipe(recipeIds[Mathf.Clamp(Mathf.RoundToInt(value), 0, recipeIds.Count - 1)]);
                return;
            }
            EnsureGlyphs();
            float previous = GetVar(id);
            spec = LabSealBenchMath.With(spec, id, value, recipeIds, sigilIds, signIds);
            float next = GetVar(id);
            if (Mathf.Approximately(previous, next)) return;
            MarkPreview();
            VarChanged?.Invoke(id, previous, next);
        }

        /// <summary>Texto en español de un valor (p. ej. «45°», «0,45 m», «Ráfaga», «fuego»).</summary>
        public string FormatVar(LabSealVarId id, float value)
        {
            int index = Mathf.RoundToInt(value);
            switch (id)
            {
                case LabSealVarId.BaseRecipe:
                    EnsureRecipes();
                    if (index < 0 || index >= recipeIds.Count) return "—";
                    SpellDef def = recipeDefs[index];
                    return def != null ? def.Name : "Libre";
                case LabSealVarId.Sigil:
                    EnsureGlyphs();
                    return GlyphName(sigilIds, index);
                case LabSealVarId.SignType:
                    EnsureGlyphs();
                    return GlyphName(signIds, index);
                case LabSealVarId.PenWidth:
                    return penWidthNames != null && index >= 0 && index < penWidthNames.Length ? penWidthNames[index] : (index + 1).ToString();
                default:
                    return LabSealBenchMath.FormatNumeric(id, value) ?? value.ToString("0.##", LabCsv.NumberFormat);
            }
        }

        /// <summary>
        /// Carga una receta de Data/Spells (sigilo, signos, angulo y giro); «libre» = sello vacio. Emite <see cref="VarChanged"/>
        /// por cada variable que cambie, con <see cref="ApplyingRecipe"/> en true. Id desconocido: aviso y no cambia nada.
        /// </summary>
        public void ApplyRecipe(string recipeId)
        {
            EnsureRecipes();
            EnsureGlyphs();
            int index = recipeIds.IndexOf(recipeId);
            if (index < 0)
            {
                Debug.LogWarning($"[Laboratorio] Receta desconocida: {recipeId}", this);
                return;
            }
            for (int i = 0; i < scratchValues.Length; i++) scratchValues[i] = LabSealBenchMath.Get(spec, (LabSealVarId)i, recipeIds, sigilIds, signIds);
            spec = LabSealBenchMath.FromRecipe(spec, recipeDefs[index], out bool exact);
            RecipeExact = exact;
            if (!exact) DiagnosticBus.OnDiagnosticsUpdated?.Invoke($"<b>[Laboratorio]</b> La receta «{FormatVar(LabSealVarId.BaseRecipe, index)}» se aproximo: el banco usa un solo tipo de signo repartido parejo.");
            EmitChanges();
        }

        /// <summary>Vuelve al sello por defecto con la receta inicial (un evento por variable que cambie).</summary>
        public void ResetSpec()
        {
            EnsureRecipes();
            EnsureGlyphs();
            for (int i = 0; i < scratchValues.Length; i++) scratchValues[i] = LabSealBenchMath.Get(spec, (LabSealVarId)i, recipeIds, sigilIds, signIds);
            spec = LabSealSpec.Default();
            int index = recipeIds.IndexOf(string.IsNullOrEmpty(initialRecipe) ? spec.recipeId : initialRecipe);
            if (index >= 0) spec = LabSealBenchMath.FromRecipe(spec, recipeDefs[index], out _);
            RecipeExact = true;
            EmitChanges();
        }

        /// <summary>Semilla del temblor (misma semilla + mismo sello = mismos trazos).</summary>
        public int Seed => spec.seed;

        /// <summary>Cambia la semilla del temblor (no es una variable de la tabla: no emite VarChanged).</summary>
        public void SetSeed(int seed)
        {
            if (spec.seed == seed) return;
            spec.seed = seed;
            MarkPreview();
        }

        /// <summary>Blanco del disparo (lo puede reasignar el generador de escena o una prueba).</summary>
        public void SetAim(Transform target)
        {
            aim = target;
            MarkPreview();
        }

        /// <summary>Dibuja el sello actual en el lienzo de prueba sin lanzarlo (aplica papel, cara, inclinacion, tinta y grosor).</summary>
        public void Rebuild()
        {
            previewDirty = false;
            if (NetSession.Exists || !EnsureReady()) return;
            ApplyPaper();

            List<List<Vector2>> strokes = LabSealSynth.Build(spec, templateOf);
            Vector2 iso = canvas.UvToIsotropic;
            float width = PenWidth(spec.penWidthIndex);
            var ink = (InkKind)(int)spec.ink;
            canvas.Clear();
            for (int k = 0; k < strokes.Count; k++)
            {
                List<Vector2> points = strokes[k];
                for (int i = 0; i < points.Count; i++) points[i] = LabSealBenchMath.SquareToCanvasUv(points[i], iso);
                canvas.Session.AddStroke(new Stroke(points) { Width = width, Ink = ink });
            }
            canvas.Redraw();
        }

        /// <summary>
        /// Arma el sello en el lienzo y lo lanza con <see cref="SpellCaster.Cast"/>. Deja el resultado en <see cref="LastCastValid"/>,
        /// <see cref="LastElement"/> y <see cref="LastQuality"/> y emite <see cref="Launched"/>. En red no hace nada.
        /// </summary>
        public void Launch()
        {
            if (NetSession.Exists)
            {
                DiagnosticBus.OnDiagnosticsUpdated?.Invoke("<b>[Laboratorio]</b> El banco de sellos no funciona en partidas en red.");
                return;
            }
            if (!EnsureReady()) return;
            Rebuild();

            bool restore = SuppressRecording(out bool previousRecording);
            SpellIR spell;
            try
            {
                spell = caster.Cast(canvas);
            }
            finally
            {
                if (restore) RecordSealsField.SetValue(caster, previousRecording);
            }

            LastSpell = spell;
            LastCastValid = spell.valid;
            LastElement = spell.valid ? spell.element : ElementType.None;
            LastQuality = spell.quality;
            LaunchCount++;
            Launched?.Invoke(spell);
        }

        // ---------------------------------------------------------------- ciclo de vida

        private void Awake()
        {
            Current = this;
            spec = LabSealSpec.Default();
            if (caster == null)
            {
                caster = FindFirstObjectByType<SpellCaster>();
                if (caster != null) Debug.LogWarning("[Laboratorio] LabSealBench sin SpellCaster asignado: se uso el de la escena.", this);
            }
            if (canvas == null)
            {
                canvas = GetComponentInChildren<DrawingCanvas>(true);
                if (canvas != null) Debug.LogWarning("[Laboratorio] LabSealBench sin lienzo asignado: se uso el hijo.", this);
            }
            templateOf = TemplateOf;
            IsolateCanvas();
        }

        private void Start()
        {
            if (canvas != null)
            {
                Vector3 scale = canvas.transform.localScale;
                Vector2 world = canvas.WorldSize;
                baseLocalScale = scale;
                baseWorldSize = world;
            }
            // Sin eventos: el estado inicial no es un cambio que registrar.
            EnsureRecipes();
            int index = recipeIds.IndexOf(initialRecipe);
            if (index >= 0)
            {
                spec = LabSealBenchMath.FromRecipe(spec, recipeDefs[index], out bool exact);
                RecipeExact = exact;
            }
            MarkPreview();
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        private void LateUpdate()
        {
            if (!previewDirty || Time.unscaledTime < nextPreviewTime) return;
            nextPreviewTime = Time.unscaledTime + 1f / PreviewHz;
            Rebuild();
        }

        // ---------------------------------------------------------------- interno

        private void MarkPreview()
        {
            if (autoPreview) previewDirty = true;
        }

        private void EmitChanges()
        {
            MarkPreview();
            if (VarChanged == null) return;
            ApplyingRecipe = true;
            try
            {
                // La receta primero: el registro escribe esa fila y omite las demas mientras ApplyingRecipe.
                for (int i = 0; i < scratchValues.Length; i++)
                {
                    var id = (LabSealVarId)i;
                    float next = LabSealBenchMath.Get(spec, id, recipeIds, sigilIds, signIds);
                    if (!Mathf.Approximately(scratchValues[i], next)) VarChanged.Invoke(id, scratchValues[i], next);
                }
            }
            finally
            {
                ApplyingRecipe = false;
            }
        }

        private bool EnsureReady()
        {
            if (canvas == null || caster == null)
            {
                Debug.LogWarning("[Laboratorio] LabSealBench necesita un lienzo y un SpellCaster.", this);
                return false;
            }
            EnsureGlyphs();
            if (glyphs == null)
            {
                Debug.LogWarning("[Laboratorio] El SpellCaster no tiene diccionario de glifos.", this);
                return false;
            }
            return true;
        }

        /// <summary>Riesgo 2: sin colliders el lienzo no se agarra ni se dibuja (las herramientas y el interactor usan raycasts).</summary>
        private void IsolateCanvas()
        {
            if (canvas == null) return;
            Collider[] colliders = canvas.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;
            if (canvas.TryGetComponent(out PhysicalCanvas physical))
            {
                physical.enabled = false;
                Debug.LogWarning("[Laboratorio] El lienzo del banco no debe llevar PhysicalCanvas (se apago).", this);
            }
            if (canvas.TryGetComponent(out Rigidbody body)) body.isKinematic = true;
        }

        /// <summary>Papel cuadrado del tamaño elegido, cara visible hacia el jugador y tinta del lado que pide la cara del papel.</summary>
        private void ApplyPaper()
        {
            Transform t = canvas.transform;
            if (baseWorldSize.x > 1e-4f && baseWorldSize.y > 1e-4f)
                t.localScale = new Vector3(baseLocalScale.x * spec.paperSize / baseWorldSize.x,
                    baseLocalScale.y * spec.paperSize / baseWorldSize.y, baseLocalScale.z);

            if (aim != null)
            {
                Vector3 emission = LabSealBenchMath.EmissionDirection(t.position, aim.position, spec.paperTiltDeg, -canvas.FaceNormal, out Vector3 upHint);
                // La cara visible mira al jugador (lado opuesto al blanco); con cara «arriba» la tinta queda del lado del blanco.
                t.rotation = LabSealBenchMath.FaceRotation(canvas.SurfaceNormalLocal, -emission, upHint);
            }
            // EmissionNormal (InkSide) = FaceNormal * InkSign: cara +1 -> sale hacia el blanco, -1 -> hacia el jugador.
            canvas.SetInkSign(-spec.paperFace);
        }

        private bool SuppressRecording(out bool previous)
        {
            previous = false;
            if (RecordSealsField == null || RecordSealsField.FieldType != typeof(bool))
            {
                if (!warnedRecording) Debug.LogWarning("[Laboratorio] No se pudo apagar recordSeals: los lanzamientos del banco se grabaran.", this);
                warnedRecording = true;
                return false;
            }
            previous = (bool)RecordSealsField.GetValue(caster);
            if (!previous) return false;
            RecordSealsField.SetValue(caster, false);
            return true;
        }

        private float PenWidth(int index)
        {
            if (penWidthsMeters == null || penWidthsMeters.Length == 0) return 0f;
            return penWidthsMeters[Mathf.Clamp(index, 0, penWidthsMeters.Length - 1)];
        }

        private List<List<Vector2>> TemplateOf(string id)
        {
            if (glyphs == null || string.IsNullOrEmpty(id)) return null;
            GlyphEntry entry = glyphs.Get(id);
            return entry.Kind == GlyphKind.Unknown ? null : entry.TemplateStrokes;
        }

        private string GlyphName(List<string> ids, int index)
        {
            if (index < 0 || index >= ids.Count) return "ninguno";
            GlyphEntry entry = glyphs != null ? glyphs.Get(ids[index]) : null;
            return entry != null && !string.IsNullOrEmpty(entry.DisplayName) ? entry.DisplayName : ids[index];
        }

        private void EnsureGlyphs()
        {
            if (glyphs != null || caster == null) return;
            glyphs = caster.Glyphs;
            if (glyphs == null) return;
            sigilIds.Clear();
            signIds.Clear();
            foreach (GlyphEntry entry in glyphs.Entries)
            {
                if (entry.Kind == GlyphKind.Sigil) sigilIds.Add(entry.Id);
                else if (entry.Kind == GlyphKind.Sign) signIds.Add(entry.Id);
            }
            GlyphDictionary dictionary = glyphs;
            Comparison<string> byClass = (a, b) => dictionary.IndexOf(a).CompareTo(dictionary.IndexOf(b));
            sigilIds.Sort(byClass);
            signIds.Sort(byClass);
        }

        /// <summary>Lista de recetas. Si todavia no habia catalogo (el grimorio lo carga despues), se reintenta en la proxima consulta.</summary>
        private void EnsureRecipes()
        {
            // Ya armada con recetas, o sin catalogo nuevo desde el ultimo intento (no rearmar en cada consulta).
            if (recipeIds.Count > 1 || (recipeIds.Count == 1 && ReferenceEquals(SpellCatalog.Shared, triedShared))) return;
            triedShared = SpellCatalog.Shared;
            recipeIds.Clear();
            recipeDefs.Clear();

            SpellCatalog catalog = null;
            if (spellFiles != null && spellFiles.Length > 0)
            {
                var texts = new List<string>(spellFiles.Length);
                for (int i = 0; i < spellFiles.Length; i++) if (spellFiles[i] != null) texts.Add(spellFiles[i].text);
                catalog = SpellCatalog.FromJsons(texts, msg => Debug.LogWarning("[Laboratorio] " + msg, this));
            }
            if (catalog == null || catalog.All.Count == 0) catalog = SpellCatalog.Shared;
#if UNITY_EDITOR
            if (catalog == null || catalog.All.Count == 0)
            {
                var texts = new List<string>();
                foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/_Project/Data/Spells" }))
                {
                    var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                    if (asset != null) texts.Add(asset.text);
                }
                catalog = SpellCatalog.FromJsons(texts, msg => Debug.LogWarning("[Laboratorio] " + msg, this));
            }
#endif
            if (catalog != null)
                foreach (SpellDef def in catalog.All)
                {
                    // Solo las recetas del proyecto: las del jugador no son reproducibles en otra maquina.
                    if (def == null || def.Origin == SpellLibrary.PlayerOrigin) continue;
                    recipeIds.Add(def.Id);
                    recipeDefs.Add(def);
                }
            recipeIds.Add(LabSealBenchMath.FreeRecipeId);
            recipeDefs.Add(null);
        }
    }
}
