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
    /// Banco de sellos del Laboratorio (v2–v4). Guarda un <see cref="LabSealSpec"/>, lo arma sobre el papel de prueba del campo
    /// (Clear + AddStroke + Redraw) y lo lanza con <see cref="SpellCaster.Cast"/> (nunca OnStrokeFinished: sin auto-lanzamiento
    /// ni gasto de tinta). v4: el papel es un objeto del campo con posicion libre (X, distancia, giro, altura, inclinacion,
    /// tamaño; LabSealBenchMath.PaperPose) y si el atril de dibujo
    /// (<see cref="LabSketchPad"/>) tiene un dibujo a mano, LANZAR usa ese dibujo en vez del sello sintetizado por los controles.
    /// v5: sin dibujo a mano, el sello de los controles se ve como vista previa en el atril y en el papel del campo.
    /// Riesgo 2: el papel de prueba no se agarra ni se dibuja (colliders y PhysicalCanvas apagados), no se graba en recordSeals
    /// y en red el banco no hace nada. Sin allocations por frame: la vista previa se rehace solo cuando cambio algo (≤ 10 Hz).
    /// </summary>
    public class LabSealBench : MonoBehaviour
    {
        [Header("Dependencias")]
        [SerializeField, Tooltip("Papel de prueba del campo (DrawingCanvas con emissionSide = InkSide, sin PhysicalCanvas).")]
        private DrawingCanvas canvas;
        [SerializeField, Tooltip("SpellCaster de la escena: lee y lanza el sello (su diccionario da las plantillas de los glifos).")]
        private SpellCaster caster;
        [SerializeField, Tooltip("Marco de la pista: +Z a lo largo, +Y arriba; X/distancia del papel se miden en este marco.")]
        private Transform trackOrigin;
        [SerializeField, Tooltip("Z (m) del inicio de la pista en el marco de Track Origin (la distancia del papel se mide desde ahi).")]
        private float trackStart;
        [SerializeField, Tooltip("Atril de dibujo del banco (opcional). Con trazos, LANZAR usa el dibujo.")]
        private LabSketchPad sketchPad;
        [SerializeField, Tooltip("Poste del papel (pivote abajo, alto 1 m en Y): se estira del piso al papel. Opcional.")]
        private Transform paperStand;

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
        [SerializeField, Tooltip("Redibuja el sello en el papel al cambiar una variable (sin lanzar).")]
        private bool autoPreview = true;

        /// <summary>Tope de redibujos de la vista previa por segundo (cada redibujo arma listas: no por frame).</summary>
        public const float PreviewHz = 10f;
        /// <summary>Alto del rayo que busca el piso bajo el papel (m).</summary>
        private const float GroundProbeUp = 20f, GroundProbeLength = 40f;

        /// <summary>Banco activo (null si no hay laboratorio en la escena).</summary>
        public static LabSealBench Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = null;

        // Provisional: SpellCaster no expone recordSeals. Se apaga solo durante el Cast del banco. TODO: reemplazar por una API publica.
        private static readonly FieldInfo RecordSealsField =
            typeof(SpellCaster).GetField("recordSeals", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly RaycastHit[] GroundHits = new RaycastHit[8];

        /// <summary>Cambio de una variable: (id, valor anterior, valor nuevo). Solo si cambio. Al cargar una receta o un atajo se emite una por variable que cambie.</summary>
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
        private Vector3 paperEmission = Vector3.forward;
        private Vector3 paperGround;

        // ---------------------------------------------------------------- API

        /// <summary>Transform del papel de prueba (null sin lienzo).</summary>
        public Transform PaperTransform => canvas != null ? canvas.transform : null;
        /// <summary>Direccion de emision del hechizo en el mundo, segun la ultima pose aplicada (para la flecha de emision).</summary>
        public Vector3 PaperEmissionWorld => paperEmission;
        /// <summary>Punto del piso bajo el centro del papel (lo usa el anillo de seleccion del campo).</summary>
        public Vector3 PaperGroundWorld => paperGround;
        /// <summary>Marco de la pista (lo comparten el campo y las cajas).</summary>
        public Transform TrackOrigin => trackOrigin;
        /// <summary>Z (m) del inicio de la pista en el marco de <see cref="TrackOrigin"/>.</summary>
        public float TrackStart => trackStart;
        /// <summary>Atril de dibujo (puede ser null).</summary>
        public LabSketchPad SketchPad => sketchPad;
        /// <summary>True si el atril tiene un dibujo a mano (LANZAR usara el dibujo; la vista previa de los controles no cuenta).</summary>
        public bool UsesDrawing => sketchPad != null && sketchPad.HasDrawing;
        /// <summary>Resultado del ultimo lanzamiento (HasResult = false antes del primero): valido, elemento, calidad en % y motivo (spell.status).</summary>
        public LabSealResult UltimoResultado =>
            new LabSealResult(LaunchCount > 0, LastCastValid, LastElement, LastQuality * 100f, LaunchCount > 0 ? LastSpell.status : null);

        /// <summary>Sello actual (ya recortado a rango).</summary>
        public LabSealSpec Spec => spec;
        /// <summary>Papel de prueba (lo usa el registro para reconocer los casts del banco).</summary>
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
        /// <summary>True si el ultimo lanzamiento uso el dibujo del atril (false = sello sintetizado).</summary>
        public bool LastLaunchFromDrawing { get; private set; }
        /// <summary>Lanzamientos hechos en esta sesion (lo usa el smoke).</summary>
        public int LaunchCount { get; private set; }

        /// <summary>Valor actual de una variable (listas: indice; sigilo/signo -1 = ninguno; papel: indice en PaperSizes).</summary>
        public float GetVar(LabSealVarId id)
        {
            EnsureRecipes();
            EnsureGlyphs();
            return LabSealBenchMath.Get(spec, id, recipeIds, sigilIds, signIds);
        }

        /// <summary>
        /// Fija una variable (recortada a rango; la receta base carga la receta). Emite <see cref="VarChanged"/> por cada variable
        /// que cambie: mover la posicion del papel tambien pasa el atajo a «Personalizada», y elegir un atajo mueve varias.
        /// </summary>
        public void SetVar(LabSealVarId id, float value)
        {
            if (id == LabSealVarId.BaseRecipe)
            {
                EnsureRecipes();
                if (recipeIds.Count == 0) return;
                ApplyRecipe(recipeIds[Mathf.Clamp(Mathf.RoundToInt(value), 0, recipeIds.Count - 1)]);
                return;
            }
            EnsureRecipes();
            EnsureGlyphs();
            Snapshot();
            spec = LabSealBenchMath.With(spec, id, value, recipeIds, sigilIds, signIds);
            EmitChanges(false);
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

        /// <summary>Plantilla de trazos de un glifo (para los iconos de las grillas); null si no existe.</summary>
        public List<List<Vector2>> GlyphTemplate(string id)
        {
            EnsureGlyphs();
            return TemplateOf(id);
        }

        /// <summary>Nombre visible de un glifo (sigilo o signo); el id si no tiene nombre.</summary>
        public string GlyphDisplayName(string id)
        {
            EnsureGlyphs();
            if (glyphs == null || string.IsNullOrEmpty(id)) return id;
            GlyphEntry entry = glyphs.Get(id);
            return entry != null && !string.IsNullOrEmpty(entry.DisplayName) ? entry.DisplayName : id;
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
            Snapshot();
            spec = LabSealBenchMath.FromRecipe(spec, recipeDefs[index], out bool exact);
            RecipeExact = exact;
            if (!exact) DiagnosticBus.OnDiagnosticsUpdated?.Invoke($"<b>[Laboratorio]</b> La receta «{FormatVar(LabSealVarId.BaseRecipe, index)}» se aproximo: el banco usa un solo tipo de signo repartido parejo.");
            EmitChanges();
        }

        /// <summary>
        /// Trazos (cuadrado unidad, sin temblor) de una receta de Data/Spells, sin tocar el estado del banco. Null si el id no existe.
        /// Lo usa la plantilla fantasma del tutorial del piso 1.
        /// </summary>
        public List<List<Vector2>> RecipeStrokes(string recipeId)
        {
            EnsureRecipes();
            EnsureGlyphs();
            int index = recipeIds.IndexOf(recipeId);
            if (index < 0 || templateOf == null) return null;
            LabSealSpec s = LabSealBenchMath.FromRecipe(LabSealSpec.Default(), recipeDefs[index], out _);
            s.tremor = 0f;
            return LabSealSynth.Build(s, templateOf);
        }

        /// <summary>
        /// «Reiniciar sello» (v4): vuelve todos los controles del sello (grupos 1–4) a la receta elegida en su forma base (sigilo,
        /// signos y geometria del canon; tinta, pluma, sentido, temblor y semilla por defecto). La posicion y el tamaño del papel
        /// del campo (grupo 5) no cambian. Un evento por variable que cambie.
        /// </summary>
        public void ResetSpec()
        {
            EnsureRecipes();
            EnsureGlyphs();
            Snapshot();
            LabSealSpec baseline = LabSealSpec.Default();
            baseline.paperSize = spec.paperSize;
            baseline.paperPose = spec.paperPose;
            baseline.paperX = spec.paperX;
            baseline.paperDistance = spec.paperDistance;
            baseline.paperYawDeg = spec.paperYawDeg;
            baseline.paperHeight = spec.paperHeight;
            baseline.paperTiltDeg = spec.paperTiltDeg;
            int index = recipeIds.IndexOf(spec.recipeId);
            if (index < 0) index = recipeIds.IndexOf(string.IsNullOrEmpty(initialRecipe) ? baseline.recipeId : initialRecipe);
            bool exact = true;
            spec = index >= 0 ? LabSealBenchMath.FromRecipe(baseline, recipeDefs[index], out exact) : baseline;
            RecipeExact = exact;
            EmitChanges();
        }

        /// <summary>
        /// Devuelve las variables dadas a su valor de arranque (el sello por defecto con la receta inicial); el resto no cambia.
        /// Si la lista incluye la receta base, se recarga la receta inicial (sigilo y signos) y se emite como receta. Un
        /// <see cref="VarChanged"/> por variable que cambie.
        /// </summary>
        public void ResetVars(IReadOnlyList<LabSealVarId> ids)
        {
            if (ids == null || ids.Count == 0) return;
            EnsureRecipes();
            EnsureGlyphs();
            Snapshot();

            bool recipe = false;
            for (int i = 0; i < ids.Count; i++) if (ids[i] == LabSealVarId.BaseRecipe) recipe = true;

            LabSealSpec baseline = LabSealSpec.Default();
            int index = recipeIds.IndexOf(string.IsNullOrEmpty(initialRecipe) ? baseline.recipeId : initialRecipe);
            if (index >= 0) baseline = LabSealBenchMath.FromRecipe(baseline, recipeDefs[index], out _);

            if (recipe && index >= 0)
            {
                spec = LabSealBenchMath.FromRecipe(spec, recipeDefs[index], out bool exact);
                RecipeExact = exact;
            }
            spec = LabSealBenchMath.ResetVars(spec, baseline, ids, recipeIds, sigilIds, signIds);
            EmitChanges(recipe);
        }

        /// <summary>Reset de un grupo (1..<see cref="LabSealCatalog.GroupCount"/>): <see cref="ResetVars"/> con sus variables.</summary>
        public void ResetGroup(int group) => ResetVars(LabSealCatalog.IdsOfGroup(group));

        /// <summary>Semilla del temblor (misma semilla + mismo sello = mismos trazos).</summary>
        public int Seed => spec.seed;

        /// <summary>Cambia la semilla del temblor (no es una variable de la tabla: no emite VarChanged).</summary>
        public void SetSeed(int seed)
        {
            if (spec.seed == seed) return;
            spec.seed = seed;
            MarkPreview();
        }

        /// <summary>Pide redibujar la vista previa (lo llama el atril de dibujo al cambiar).</summary>
        public void MarkPreview()
        {
            if (autoPreview) previewDirty = true;
        }

        /// <summary>
        /// Ubica el papel y dibuja en el lo que se va a lanzar, sin lanzarlo: el dibujo a mano del atril si lo hay; si no, el sello
        /// sintetizado (tinta y grosor de los controles), que tambien se ve como vista previa en el atril.
        /// </summary>
        public void Rebuild()
        {
            previewDirty = false;
            if (NetSession.Exists || !EnsureReady()) return;
            ApplyPaper();

            canvas.Clear();
            if (UsesDrawing)
            {
                CopyDrawing();
                canvas.Redraw();
                return;
            }
            List<List<Vector2>> strokes = LabSealSynth.Build(spec, templateOf);
            Vector2 iso = canvas.UvToIsotropic;
            float width = PenWidth(spec.penWidthIndex);
            var ink = (InkKind)(int)spec.ink;
            // v5: la misma vista previa en el atril cercano (ancho proporcional a su tamaño); antes de convertir los puntos.
            if (sketchPad != null && sketchPad.Canvas != null)
            {
                float paperSide = Mathf.Max(1e-3f, Mathf.Min(canvas.WorldSize.x, canvas.WorldSize.y));
                float padSide = Mathf.Min(sketchPad.Canvas.WorldSize.x, sketchPad.Canvas.WorldSize.y);
                sketchPad.ShowPreview(strokes, width * padSide / paperSide, ink);
            }
            for (int k = 0; k < strokes.Count; k++)
            {
                List<Vector2> points = strokes[k];
                for (int i = 0; i < points.Count; i++) points[i] = LabSealBenchMath.SquareToCanvasUv(points[i], iso);
                canvas.Session.AddStroke(new Stroke(points) { Width = width, Ink = ink });
            }
            canvas.Redraw();
        }

        /// <summary>
        /// Arma en el papel del campo el dibujo del atril (si tiene trazos) o el sello de los controles y lo lanza con
        /// <see cref="SpellCaster.Cast"/>. Siempre por boton: nunca automatico al terminar un trazo. En red no hace nada.
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
            LastLaunchFromDrawing = UsesDrawing;

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

        private void OnEnable()
        {
            if (sketchPad != null) sketchPad.Changed += MarkPreview;
        }

        private void OnDisable()
        {
            if (sketchPad != null) sketchPad.Changed -= MarkPreview;
        }

        private void Start()
        {
            if (canvas != null)
            {
                baseLocalScale = canvas.transform.localScale;
                baseWorldSize = canvas.WorldSize;
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

        private void Snapshot()
        {
            for (int i = 0; i < scratchValues.Length; i++) scratchValues[i] = LabSealBenchMath.Get(spec, (LabSealVarId)i, recipeIds, sigilIds, signIds);
        }

        /// <param name="asRecipe">True: el registro escribe solo la fila de la receta (cargar receta / reiniciar sello). False: una fila por variable.</param>
        private void EmitChanges(bool asRecipe = true)
        {
            MarkPreview();
            if (VarChanged == null) return;
            ApplyingRecipe = asRecipe;
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

        /// <summary>Riesgo 2: sin colliders el papel no se agarra ni se dibuja (las herramientas y el interactor usan raycasts).</summary>
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

        /// <summary>Copia los trazos del atril al papel (UV igual: los dos son cuadrados), con el ancho escalado al tamaño del papel.</summary>
        private void CopyDrawing()
        {
            DrawingCanvas pad = sketchPad.Canvas;
            List<Stroke> source = pad.Session.Strokes;
            float padSide = Mathf.Max(1e-3f, Mathf.Min(pad.WorldSize.x, pad.WorldSize.y));
            float paperSide = Mathf.Min(canvas.WorldSize.x, canvas.WorldSize.y);
            float scale = paperSide / padSide;
            for (int i = 0; i < source.Count; i++)
            {
                Stroke s = source[i];
                if (s == null || s.Count == 0) continue;
                canvas.Session.AddStroke(new Stroke(new List<Vector2>(s.Points)) { Width = s.Width * scale, Ink = s.Ink });
            }
        }

        /// <summary>
        /// Papel cuadrado del tamaño elegido, en su lugar del campo: (X, distancia) sobre la pista, centro a la altura pedida sobre
        /// el piso real (rayo hacia abajo, sin chocarse con cajas ni triggers), girado e inclinado, con la tinta del lado por donde
        /// sale el hechizo. Sin marco de pista queda donde esta (solo cambia tamaño y lado de la tinta).
        /// </summary>
        private void ApplyPaper()
        {
            Transform t = canvas.transform;
            if (baseWorldSize.x > 1e-4f && baseWorldSize.y > 1e-4f)
                t.localScale = new Vector3(baseLocalScale.x * spec.paperSize / baseWorldSize.x,
                    baseLocalScale.y * spec.paperSize / baseWorldSize.y, baseLocalScale.z);

            if (trackOrigin == null)
            {
                canvas.SetInkSign(-spec.paperFace);
                paperEmission = canvas.FaceNormal * canvas.InkSign;
                return;
            }

            Vector3 groundWorld = GroundBelow(trackOrigin.TransformPoint(new Vector3(spec.paperX, 0f, trackStart + spec.paperDistance)));
            paperGround = groundWorld;
            float groundLocalY = trackOrigin.InverseTransformPoint(groundWorld).y;
            float lift = LabSealBenchMath.PaperCenterHeight(spec.paperHeight, spec.paperTiltDeg, spec.paperSize);
            var centerLocal = new Vector3(spec.paperX, groundLocalY + lift, trackStart + spec.paperDistance);
            LabPaperPose pose = LabSealBenchMath.PaperPose(spec, centerLocal, LabSealBenchMath.DefaultViewer);

            Quaternion rotation = LabSealBenchMath.FaceRotation(canvas.SurfaceNormalLocal,
                trackOrigin.TransformDirection(pose.ViewNormal), trackOrigin.TransformDirection(pose.Up));
            Vector3 centerWorld = trackOrigin.TransformPoint(centerLocal);
            t.rotation = rotation;
            // El pivote del lienzo puede no estar en el centro de la malla: se corre para que el centro quede en su lugar.
            t.position = centerWorld - rotation * canvas.CenterOffsetLocalScaled;
            paperEmission = trackOrigin.TransformDirection(pose.Emission);
            canvas.SetInkSign(pose.InkSign);

            if (paperStand != null)
            {
                // Poste del piso al centro del papel; con el papel en el piso (o casi) se oculta.
                float height = centerWorld.y - groundWorld.y;
                bool show = height > 0.08f;
                if (paperStand.gameObject.activeSelf != show) paperStand.gameObject.SetActive(show);
                if (show)
                {
                    paperStand.SetPositionAndRotation(groundWorld, trackOrigin.rotation);
                    paperStand.localScale = new Vector3(1f, height, 1f);
                }
            }
        }

        /// <summary>Piso bajo un punto: el impacto mas cercano hacia abajo que no sea trigger, el papel ni algo materializado.</summary>
        private Vector3 GroundBelow(Vector3 point)
        {
            Vector3 origin = new Vector3(point.x, point.y + GroundProbeUp, point.z);
            int n = Physics.RaycastNonAlloc(origin, Vector3.down, GroundHits, GroundProbeLength, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 ground = point;
            LabSpawner spawner = LabSpawner.Current;
            Transform paper = canvas != null ? canvas.transform : null;
            for (int i = 0; i < n; i++)
            {
                RaycastHit hit = GroundHits[i];
                Transform h = hit.collider.transform;
                if (paper != null && h.IsChildOf(paper)) continue;
                if (paperStand != null && h.IsChildOf(paperStand)) continue;
                if (spawner != null && spawner.Owns(h)) continue;
                if (hit.collider.attachedRigidbody != null && !hit.collider.attachedRigidbody.isKinematic) continue;
                if (hit.distance < best)
                {
                    best = hit.distance;
                    ground = hit.point;
                }
            }
            return ground;
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
            return entry == null || entry.Kind == GlyphKind.Unknown ? null : entry.TemplateStrokes;
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
