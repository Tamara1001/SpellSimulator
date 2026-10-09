using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Compiler;
using SpellSimulator.Core;
using SpellSimulator.DebugTools;
using SpellSimulator.Glyphs;
using SpellSimulator.Output;
using SpellSimulator.Parsing;
using SpellSimulator.Recognition;

namespace SpellSimulator.Casting
{
    /// <summary>
    /// Unico punto de entrada para lanzar un hechizo desde un lienzo. Orquesta el pipeline:
    /// limpiar -> parsear (anillo, candidatos) -> reconocer (ONNX) -> armar GlyphAST -> compilar -> salida.
    /// Cada etapa es logica pura salvo el reconocedor y la salida.
    /// </summary>
    public class SpellCaster : MonoBehaviour
    {
        [Header("Dependencias")]
        [SerializeField] private SentisGlyphClassifier classifier;
        [SerializeField] private GlyphDictionaryAsset dictionary;
        [SerializeField] private SpellOutputController output;

        [Header("Limpieza de trazos")]
        [SerializeField, Range(0.001f, 0.05f), Tooltip("Tolerancia de Douglas-Peucker. Mayor = mas simplificado.")]
        private float simplificationTolerance = StrokeCleaner.DefaultTolerance;
        [SerializeField, Range(1, 10), Tooltip("Resolucion del Catmull-Rom. Mayor = curvas mas suaves.")]
        private int splineResolution = StrokeCleaner.DefaultSplineResolution;

        [Header("Retardo de lanzamiento")]
        [SerializeField, Tooltip("Los sellos sin disparador cargan 0,5-2 s (segun el tamano del anillo) antes de lanzarse.")]
        private bool castDelayEnabled = true;

        [Header("Pruebas")]
        [SerializeField, Tooltip("Guarda cada sello lanzado en persistentDataPath/Sellos para reproducirlo como fixture.")]
        private bool recordSeals = true;

        /// <summary>Ultimo hechizo compilado (valido o no). Util para UI y tests.</summary>
        public SpellIR LastSpell { get; private set; }

        /// <summary>
        /// Se emite tras cada cast con la lectura completa (AST) ademas del hechizo: para overlays sobre el papel y
        /// herramientas de aprendizaje. Para reaccionar al hechizo en si, usar SpellCompiled.
        /// </summary>
        public event System.Action<GlyphAST, SpellIR, DrawingCanvas> SealAnalyzed;

        /// <summary>Se emite tras cada cast con el resultado compilado.</summary>
        public event System.Action<SpellIR, DrawingCanvas> SpellCompiled;

        /// <summary>Se emite cuando cambia la fase de un sello (para feedback en el lienzo, hapticos, sonido).</summary>
        public event System.Action<DrawingCanvas, SealPhase> SealPhaseChanged;

        /// <summary>Un sello sin disparador empezo a cargar su retardo de lanzamiento.</summary>
        public event System.Action<SealChargeInfo> SealCharging;

        /// <summary>Termino la carga de un sello: fired = true si se lanzo, false si se cancelo.</summary>
        public event System.Action<DrawingCanvas, bool> SealChargeEnded;

        private struct ActiveCharge
        {
            public DrawingCanvas canvas;
            public float startTime;
            public float seconds;
        }

        private const int MaxCharges = 8;
        private readonly List<ActiveCharge> charges = new List<ActiveCharge>(MaxCharges);

        private readonly Dictionary<DrawingCanvas, SealPhase> phases = new Dictionary<DrawingCanvas, SealPhase>();

        private void Start()
        {
            // La pose del modelo da la orientacion de cada signo; se calibra con las plantillas del diccionario.
            if (classifier != null && dictionary != null) classifier.CalibratePose(dictionary.Dictionary.TemplateSessions());
            if (GetComponent<SealGlowDirector>() == null) gameObject.AddComponent<SealGlowDirector>();
        }

        private void Update()
        {
            for (int i = charges.Count - 1; i >= 0; i--)
            {
                ActiveCharge charge = charges[i];
                if (charge.canvas == null || !charge.canvas.isActiveAndEnabled)
                {
                    charges.RemoveAt(i);
                    if (charge.canvas != null) SetPhase(charge.canvas, SealPhase.Prepared);
                    else phases.Remove(charge.canvas);
                    SealChargeEnded?.Invoke(charge.canvas, false);
                    continue;
                }
                if (Time.time - charge.startTime < charge.seconds) continue;

                charges.RemoveAt(i);
                SpellIR spell = Cast(charge.canvas);
                SetPhase(charge.canvas, SealActivation.AfterCast(spell.valid && spell.active));
                SealChargeEnded?.Invoke(charge.canvas, true);
            }
        }

        public bool IsCharging(DrawingCanvas canvas) => IndexOfCharge(canvas) >= 0;

        private int IndexOfCharge(DrawingCanvas canvas)
        {
            for (int i = 0; i < charges.Count; i++)
                if (charges[i].canvas == canvas) return i;
            return -1;
        }

        /// <summary>Cancela la carga de un lienzo (si la hay) y devuelve su fase a Prepared.</summary>
        private void CancelCharge(DrawingCanvas canvas)
        {
            int index = IndexOfCharge(canvas);
            if (index < 0) return;
            charges.RemoveAt(index);
            SetPhase(canvas, SealPhase.Prepared);
            SealChargeEnded?.Invoke(canvas, false);
        }

        /// <summary>
        /// Compila la lectura del sello; si es valida y activa y hay lugar, empieza la carga y devuelve true.
        /// Si no, devuelve false y el llamador lanza (o falla) de inmediato como siempre.
        /// </summary>
        private bool TryStartCharge(DrawingCanvas canvas)
        {
            if (!castDelayEnabled || charges.Count >= MaxCharges || classifier == null || dictionary == null) return false;

            SealReading reading = SealReader.Read(Clean(canvas, out float boost, out Vector2 pivot), classifier, dictionary.Dictionary, Time.time);
            if (!reading.Spell.valid || !reading.Spell.active) return false;

            Vector2 iso = canvas.UvToIsotropic;
            Vector2 worldSize = canvas.WorldSize;
            RingInfo readRing = reading.Ast.Ring;
            float radiusWorld = readRing.Radius * Mathf.Max(worldSize.x, worldSize.y) / boost;
            Vector2 ringCenter = boost > 1f ? pivot + (readRing.Center - pivot) / boost : readRing.Center;
            float seconds = SealCharge.Duration(radiusWorld);

            charges.Add(new ActiveCharge { canvas = canvas, startTime = Time.time, seconds = seconds });
            SetPhase(canvas, SealPhase.Charging);
            SealCharging?.Invoke(new SealChargeInfo
            {
                canvas = canvas,
                element = reading.Spell.element,
                sigilId = reading.Ast.PrimarySigil?.Id,
                seconds = seconds,
                ringCenterUV = new Vector2(ringCenter.x / Mathf.Max(iso.x, 1e-6f), ringCenter.y / Mathf.Max(iso.y, 1e-6f)),
                ringRadiusWorld = radiusWorld,
                ink = InkMath.Dominant(canvas.Session.Strokes)
            });
            return true;
        }

        public SealPhase GetPhase(DrawingCanvas canvas)
        {
            return canvas != null && phases.TryGetValue(canvas, out var phase) ? phase : SealPhase.Drawing;
        }

        /// <summary>Llamar al terminar un trazo de tinta: si el anillo quedo cerrado, el hechizo se lanza (canon).</summary>
        public void OnStrokeFinished(DrawingCanvas canvas) => Reevaluate(canvas, castAllowed: true);

        /// <summary>Llamar tras borrar o deshacer: actualiza la fase sin lanzar.</summary>
        public void OnSealEdited(DrawingCanvas canvas) => Reevaluate(canvas, castAllowed: false);

        private void Reevaluate(DrawingCanvas canvas, bool castAllowed)
        {
            if (canvas == null) return;

            // Cualquier edicion durante la carga la cancela; despues se reevalua normalmente (un trazo nuevo la reinicia).
            CancelCharge(canvas);

            RingInfo ring = canvas.Session.IsEmpty
                ? RingInfo.NotFound()
                : RingDetector.Detect(Clean(canvas));
            SealPhase previous = GetPhase(canvas);
            bool deferred = canvas.GetComponentInParent<Interfaces.ISealTrigger>() != null;
            SealPhase next = SealActivation.Evaluate(ring, previous, castAllowed, deferred, out bool shouldCast);
            // Runa agotada: ni se arma ni se lanza; un anillo cerrado queda como gastado.
            if (IsBlocked(canvas))
            {
                shouldCast = false;
                if (ring.Found && ring.Complete) next = SealPhase.Spent;
            }

            if (shouldCast)
            {
                if (TryStartCharge(canvas)) return;
                SpellIR spell = Cast(canvas);
                next = SealActivation.AfterCast(spell.valid && spell.active);
            }
            else if (next == SealPhase.Prepared && previous != SealPhase.Prepared)
            {
                DiagnosticBus.OnDiagnosticsUpdated?.Invoke(
                    $"<b>[Sello] Preparado</b>\nAnillo abierto ({ring.Completeness:P0}). Cerralo para lanzar.");
            }

            if (next == SealPhase.Armed && previous != SealPhase.Armed)
                DiagnosticBus.OnDiagnosticsUpdated?.Invoke(
                    "<b>[Sello] Armado</b>\nEl sello se lanza cuando se active su disparador (impacto o presion).");

            SetPhase(canvas, next);
        }

        /// <summary>Dispara un sello armado (lo llaman los disparadores). Devuelve si se lanzo.</summary>
        public bool Trigger(DrawingCanvas canvas)
        {
            if (canvas == null || !SealActivation.ShouldFire(GetPhase(canvas)) || IsBlocked(canvas)) return false;
            SpellIR spell = Cast(canvas);
            bool launched = spell.valid && spell.active;
            SetPhase(canvas, launched ? SealPhase.Spent : SealPhase.Armed);
            return launched;
        }

        /// <summary>Vuelve a armar un sello gastado (sellos permanentes). El anillo sigue cerrado en el papel.</summary>
        public void Rearm(DrawingCanvas canvas)
        {
            if (canvas == null || IsBlocked(canvas)) return;
            SetPhase(canvas, SealActivation.Rearm(GetPhase(canvas)));
        }

        /// <summary>Marca un sello como gastado (lo usan las fusiones de runas, que lanzan sin pasar por Trigger).</summary>
        public void MarkSpent(DrawingCanvas canvas)
        {
            if (canvas != null) SetPhase(canvas, SealPhase.Spent);
        }

        private static bool IsBlocked(DrawingCanvas canvas)
        {
            var gate = canvas.GetComponentInParent<Interfaces.ISealGate>();
            return gate != null && gate.SealBlocked;
        }

        private void SetPhase(DrawingCanvas canvas, SealPhase phase)
        {
            if (GetPhase(canvas) == phase) return;
            phases[canvas] = phase;
            SealPhaseChanged?.Invoke(canvas, phase);
        }

        /// <summary>Extension (espacio isotropico) bajo la cual un dibujo se considera chico y se agranda para leerlo.</summary>
        private const float SmallDrawingExtent = 0.3f;
        private const float BoostedExtent = 0.42f;
        private const float MaxBoost = 8f;

        private DrawingSession Clean(DrawingCanvas canvas) => Clean(canvas, out _, out _);

        private DrawingSession Clean(DrawingCanvas canvas, out float boost, out Vector2 pivot)
        {
            // Los trazos estan en UV; en un lienzo no cuadrado se pasan a un espacio isotropico.
            var scaled = canvas.Session.Scaled(canvas.UvToIsotropic);
            boost = 1f;
            pivot = Vector2.zero;
            if (scaled.GetBounds(out Vector2 min, out Vector2 max))
            {
                float extent = Mathf.Max(max.x - min.x, max.y - min.y);
                if (extent > 1e-4f && extent < SmallDrawingExtent)
                {
                    boost = Mathf.Min(BoostedExtent / extent, MaxBoost);
                    pivot = (min + max) * 0.5f;
                    foreach (var stroke in scaled.Strokes)
                        for (int i = 0; i < stroke.Points.Count; i++)
                            stroke.Points[i] = pivot + (stroke.Points[i] - pivot) * boost;
                }
            }
            return StrokeCleaner.CleanSession(scaled, simplificationTolerance, splineResolution);
        }

        /// <summary>Compila y lanza lo que haya en el lienzo, sin mirar la fase. Lo usan la auto-activacion y los tests.</summary>
        public SpellIR Cast(DrawingCanvas canvas)
        {
            if (canvas == null || canvas.Session.IsEmpty)
            {
                Debug.LogWarning("[Cast] El lienzo esta vacio.");
                return SpellIR.Invalid("Lienzo vacio", null);
            }
            if (classifier == null || dictionary == null)
            {
                Debug.LogError("[Cast] Falta asignar el clasificador o el diccionario en SpellCaster.", this);
                return SpellIR.Invalid("Sin reconocedor", null);
            }

            SpellIR spell = Analyze(canvas, out GlyphAST ast, out GlyphPrediction[] predictions, out Vector2 iso, out Vector2 ringCenterUV);

            LastSpell = spell;
            string summary = PublishDiagnostics(ast, spell, predictions, dictionary.Dictionary);
            if (recordSeals) Record(canvas, iso, summary);
            SealAnalyzed?.Invoke(ast, spell, canvas);
            SpellCompiled?.Invoke(spell, canvas);

            if (spell.valid && spell.active && output != null)
                output.SpawnSpellEffect(spell, canvas, ringCenterUV);
            return spell;
        }

        /// <summary>
        /// Lee y compila el lienzo sin lanzar nada ni publicar eventos (para fusionar runas). Devuelve true si el
        /// hechizo es valido y activo. Es la misma lectura que usa <see cref="Cast"/>.
        /// </summary>
        public bool Compile(DrawingCanvas canvas, out SpellIR spell, out Vector2 ringCenterUV)
        {
            ringCenterUV = new Vector2(0.5f, 0.5f);
            if (canvas == null || canvas.Session.IsEmpty || classifier == null || dictionary == null)
            {
                spell = SpellIR.Invalid("Sin lectura", null);
                return false;
            }
            spell = Analyze(canvas, out _, out _, out _, out ringCenterUV);
            return spell.valid && spell.active;
        }

        /// <summary>Publica un hechizo ya compilado (eventos) y lo instancia en el mundo desde el lienzo dado.</summary>
        public void Emit(SpellIR spell, DrawingCanvas canvas, Vector2 ringCenterUV)
        {
            LastSpell = spell;
            SpellCompiled?.Invoke(spell, canvas);
            if (spell.valid && spell.active && output != null)
                output.SpawnSpellEffect(spell, canvas, ringCenterUV);
        }

        private SpellIR Analyze(DrawingCanvas canvas, out GlyphAST ast, out GlyphPrediction[] predictions, out Vector2 iso, out Vector2 ringCenterUV)
        {
            SealReading reading = SealReader.Read(Clean(canvas, out float boost, out Vector2 pivot), classifier, dictionary.Dictionary, Time.time);
            ast = reading.Ast;
            SpellIR spell = reading.Spell;

            // Tamanio real del sello: el pipeline trabaja en espacio isotropico (lado mayor del lienzo = 1).
            iso = canvas.UvToIsotropic;
            Vector2 worldSize = canvas.WorldSize;
            spell.analysisBoost = boost;
            spell.analysisPivot = pivot;
            spell.sealRadius = ast.Ring.Radius * Mathf.Max(worldSize.x, worldSize.y) / boost;
            // Centro del anillo en el espacio original (deshace el agrandado alrededor del pivote).
            Vector2 ringCenter = boost > 1f ? pivot + (ast.Ring.Center - pivot) / boost : ast.Ring.Center;
            ringCenterUV = new Vector2(ringCenter.x / Mathf.Max(iso.x, 1e-6f), ringCenter.y / Mathf.Max(iso.y, 1e-6f));
            predictions = reading.Predictions;

            InkKind inkKind = InkMath.Dominant(canvas.Session.Strokes);
            spell.inkPower = InkKindInfo.PowerMultiplier(inkKind);
            spell.waterproof = InkKindInfo.IsWaterproof(inkKind);
            return spell;
        }

        private static void Record(DrawingCanvas canvas, Vector2 iso, string summary)
        {
            var record = new SealRecord
            {
                Iso = iso,
                Session = canvas.Session.Scaled(Vector2.one),
                Summary = System.Text.RegularExpressions.Regex.Replace(summary, "<[^>]+>", "")
            };
            string path = SealRecord.Save(record);
            if (path != null) Debug.Log($"[Cast] Sello guardado: {path}");
        }

        private static string PublishDiagnostics(GlyphAST ast, SpellIR spell, GlyphPrediction[] predictions, GlyphDictionary dictionary)
        {
            var predictionsLog = new StringBuilder("<b>IA Top 3 (sigilo):</b>\n");
            // El primer candidato es el central (sigilo) cuando hay tinta en el centro.
            if (predictions.Length > 0) AppendTop3(predictionsLog, predictions[0], dictionary);
            DiagnosticBus.OnPredictionUpdated?.Invoke(predictionsLog.ToString());

            var log = new StringBuilder();
            log.AppendLine($"<b>[Hechizo] {spell.status}</b>");
            log.AppendLine($"- Anillo: {(ast.Ring.Found ? (ast.Ring.Complete ? "cerrado" : $"abierto ({ast.Ring.Completeness:P0})") : "no")}");
            log.AppendLine($"- Sigilo: {Describe(ast.PrimarySigil)}");
            log.Append($"- Signos ({ast.Signs.Count}):");
            foreach (var s in ast.Signs) log.Append($" {Describe(s)}");
            log.AppendLine();
            if (ast.Unknowns.Count > 0)
            {
                log.Append($"- Desconocidos ({ast.Unknowns.Count}):");
                foreach (var u in ast.Unknowns) log.Append($" {u.Id}?{u.Confidence:F2}({u.RejectReason})");
                log.AppendLine();
            }

            if (spell.valid)
            {
                log.AppendLine($"\n<b>[Parametros]</b> {spell.element} / {spell.primaryManifestation}");
                log.AppendLine($"Escala {spell.effectScale:F2} | Dur {spell.duration:F1}s | Fuerza {spell.force:F2}");
                log.AppendLine($"Foco {spell.focus:F2} | Disp {spell.spread:F2} | Alcance {spell.range:F2}");
                log.AppendLine($"Grav {spell.gravity:F2} | Calidad {spell.quality:F2} | Estab {spell.stability:F2}");
                var plan = spell.plan;
                if (plan != null)
                {
                    log.AppendLine($"<b>[Plan]</b> {plan.Primitives}");
                    log.AppendLine($"Chorro ({plan.Aim.x:F1}, {plan.Aim.y:F1}, {plan.Aim.z:F1}) | Abanico {plan.Dispersion:F1} | Giro {plan.Circulation:F1} | Lente {plan.Focus:F1}");
                    foreach (var reading in plan.Signs) log.AppendLine($"- {PlanResolver.DescribeSign(reading)}");
                    if (plan.Notes.Count > 0) log.AppendLine($"Notas: {string.Join(", ", plan.Notes)}");
                }
            }

            foreach (var w in spell.warnings) log.AppendLine($"<color=#e0a040>! {GlyphWarnings.Describe(w)}</color>");

            string text = log.ToString();
            Debug.Log(text);
            DiagnosticBus.OnDiagnosticsUpdated?.Invoke(text);
            return text;
        }

        private static string Describe(GlyphRecognition r)
        {
            return r == null ? "ninguno" : $"{r.Id}({r.Confidence:F2})";
        }

        private static void AppendTop3(StringBuilder sb, GlyphPrediction prediction, GlyphDictionary dictionary)
        {
            float[] p = prediction.Probabilities;
            int first = -1, second = -1, third = -1;
            for (int i = 0; i < p.Length; i++)
            {
                if (first < 0 || p[i] > p[first]) { third = second; second = first; first = i; }
                else if (second < 0 || p[i] > p[second]) { third = second; second = i; }
                else if (third < 0 || p[i] > p[third]) third = i;
            }
            foreach (int i in new[] { first, second, third })
                if (i >= 0) sb.AppendLine($"- {dictionary.Get(i).Id} ({p[i]:F2})");
        }
    }
}
