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

        /// <summary>
        /// Se emite al terminar de reevaluar un lienzo (trazo terminado, borrado o deshacer). Lo escucha
        /// CanvasJoinSystem para mantener la lista de mitades candidatas.
        /// </summary>
        public event System.Action<DrawingCanvas> CanvasEdited;

        /// <summary>Un sello sin disparador empezo a cargar su retardo de lanzamiento.</summary>
        public event System.Action<SealChargeInfo> SealCharging;

        /// <summary>Termino la carga de un sello: fired = true si se lanzo, false si se cancelo.</summary>
        public event System.Action<DrawingCanvas, bool> SealChargeEnded;

        /// <summary>
        /// Se lanzo un sello compuesto (circulos dentro de un gran circulo): cuantos sub-hechizos salieron. Cada uno ademas
        /// publica su propio SpellCompiled.
        /// </summary>
        public event System.Action<DrawingCanvas, int> NestedCompiled;

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
            if (GetComponent<World.CanvasJoinSystem>() == null) gameObject.AddComponent<World.CanvasJoinSystem>();
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
                if (GameClock.Time - charge.startTime < charge.seconds) continue;

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

            // Sello compuesto: el retardo es la suma de los interiores (tope 4 s); el brillo usa el anillo EXTERIOR y el
            // elemento del interior mas grande.
            if (TryAnalyzeNested(canvas, out NestedReading nested))
            {
                var perInner = new float[nested.Subs.Count];
                for (int i = 0; i < perInner.Length; i++) perInner[i] = SealCharge.Duration(nested.Subs[i].Spell.sealRadius);
                NestedSubReading largest = nested.Subs[nested.LargestIndex()];
                Vector2 isoN = canvas.UvToIsotropic;
                Vector2 outerCenter = nested.Layout.Outer.Center;
                return StartCharge(canvas, SealCharge.NestedDuration(perInner), largest.Spell.element, largest.Reading.Ast.PrimarySigil?.Id,
                    new Vector2(outerCenter.x / Mathf.Max(isoN.x, 1e-6f), outerCenter.y / Mathf.Max(isoN.y, 1e-6f)),
                    nested.Layout.Outer.Radius * Mathf.Max(canvas.WorldSize.x, canvas.WorldSize.y));
            }

            SealReading reading = SealReader.Read(Clean(canvas, out float boost, out Vector2 pivot), classifier, dictionary.Dictionary, GameClock.Time);
            if (!reading.Spell.valid || !reading.Spell.active) return false;

            Vector2 iso = canvas.UvToIsotropic;
            Vector2 worldSize = canvas.WorldSize;
            RingInfo readRing = reading.Ast.Ring;
            float radiusWorld = readRing.Radius * Mathf.Max(worldSize.x, worldSize.y) / boost;
            Vector2 ringCenter = boost > 1f ? pivot + (readRing.Center - pivot) / boost : readRing.Center;
            return StartCharge(canvas, SealCharge.Duration(radiusWorld), reading.Spell.element, reading.Ast.PrimarySigil?.Id,
                new Vector2(ringCenter.x / Mathf.Max(iso.x, 1e-6f), ringCenter.y / Mathf.Max(iso.y, 1e-6f)), radiusWorld);
        }

        private bool StartCharge(DrawingCanvas canvas, float seconds, ElementType element, string sigilId, Vector2 ringCenterUV, float radiusWorld)
        {
            charges.Add(new ActiveCharge { canvas = canvas, startTime = GameClock.Time, seconds = seconds });
            SetPhase(canvas, SealPhase.Charging);
            SealCharging?.Invoke(new SealChargeInfo
            {
                canvas = canvas,
                element = element,
                sigilId = sigilId,
                seconds = seconds,
                ringCenterUV = ringCenterUV,
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
        public void OnStrokeFinished(DrawingCanvas canvas)
        {
            Reevaluate(canvas, castAllowed: true);
            CanvasEdited?.Invoke(canvas);
        }

        /// <summary>Llamar tras borrar o deshacer: actualiza la fase sin lanzar.</summary>
        public void OnSealEdited(DrawingCanvas canvas)
        {
            Reevaluate(canvas, castAllowed: false);
            CanvasEdited?.Invoke(canvas);
        }

        /// <summary>
        /// True si los trazos (UV [0,1] de un lienzo de worldSize metros) forman un anillo cerrado. Es la misma lectura
        /// que hace Reevaluate; la usa la union de mitades para saber si vale la pena lanzar.
        /// </summary>
        public bool IsRingClosed(DrawingSession session, Vector2 worldSize)
        {
            if (session == null || session.IsEmpty) return false;
            float largest = Mathf.Max(worldSize.x, worldSize.y, 1e-6f);
            var iso = new Vector2(worldSize.x / largest, worldSize.y / largest);
            RingInfo ring = RingDetector.Detect(Clean(session, iso, out _, out _));
            return ring.Found && ring.Complete;
        }

        /// <summary>
        /// Union de mitades: decide si la sesion fusionada forma un anillo cerrado, curando la costura si hace falta. Si
        /// <paramref name="halfACount"/> &gt;= 0 (los primeros trazos son de la mitad A), exige que cada mitad aporte al menos
        /// <see cref="CanvasJoinMath.MinHalfShare"/> de la circunferencia. Con cobertura &gt;= 0,85 pero sin cerrar, agrega trazos
        /// puente (<see cref="CanvasJoinMath.HealSeam"/>) y los conserva solo si el anillo queda cerrado. No toca el camino de un
        /// solo lienzo.
        /// </summary>
        public bool TryCloseRing(ref DrawingSession merged, Vector2 worldSize, int halfACount = -1)
        {
            if (merged == null || merged.IsEmpty) return false;
            float largest = Mathf.Max(worldSize.x, worldSize.y, 1e-6f);
            var iso = new Vector2(worldSize.x / largest, worldSize.y / largest);

            // Anillo en crudo (sin limpiar): sirve para medir el aporte de cada mitad y para saber donde esta la costura.
            DrawingSession scaled = merged.Scaled(iso);
            RingInfo ring = RingDetector.Detect(scaled);
            if (!ring.Found) { Debug.Log($"[Mitades] no se unen: no hay anillo en el conjunto (envolvente {worldSize.x:F2} x {worldSize.y:F2} m)."); return false; }
            if (halfACount >= 0 && !CanvasJoinMath.EachHalfContributes(scaled, halfACount, ring))
            {
                Debug.Log($"[Mitades] no se unen: una mitad aporta menos del 25 % del anillo (cobertura {ring.Completeness:P0}).");
                return false;
            }
            if (IsRingClosed(merged, worldSize)) return true;

            int before = merged.Strokes.Count;
            if (CanvasJoinMath.HealSeam(merged, ring, iso) == 0) { Debug.Log($"[Mitades] no se unen: la costura no se pudo cerrar (cobertura {ring.Completeness:P0}, completo={ring.Complete})."); return false; }
            if (IsRingClosed(merged, worldSize)) return true;
            merged.Strokes.RemoveRange(before, merged.Strokes.Count - before); // no cerro: se deshace el puente
            Debug.Log($"[Mitades] no se unen: el puente no cerro el anillo (cobertura {ring.Completeness:P0}).");
            return false;
        }

        private void Reevaluate(DrawingCanvas canvas, bool castAllowed)
        {
            if (canvas == null) return;

            // Cualquier edicion durante la carga la cancela; despues se reevalua normalmente (un trazo nuevo la reinicia).
            CancelCharge(canvas);

            // En un sello compuesto el anillo detectado es el EXTERIOR (el mas grande): la fase la decide su cierre, como
            // en el canon; la lectura por circulos la hacen TryStartCharge/Cast.
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
            => Clean(canvas.Session, canvas.UvToIsotropic, out boost, out pivot);

        private DrawingSession Clean(DrawingSession session, Vector2 isotropic, out float boost, out Vector2 pivot)
        {
            // Los trazos estan en UV; en un lienzo no cuadrado se pasan a un espacio isotropico.
            var scaled = session.Scaled(isotropic);
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

            // Sello compuesto (circulos dentro de un gran circulo): cada interior sale a la vez desde su centro. Por aca
            // pasan tambien Trigger (runas), la carga y la union de mitades (CanvasJoinSystem), que llaman a Cast.
            if (TryAnalyzeNested(canvas, out NestedReading nested)) return CastNested(canvas, nested);

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
            // Sello compuesto: devuelve el interior mas grande que se lanza, ya amplificado (v1: la fusion de runas usa
            // un solo hechizo por runa).
            if (TryAnalyzeNested(canvas, out NestedReading nested))
            {
                NestedSubReading largest = nested.Subs[nested.LargestIndex()];
                spell = largest.Spell;
                ringCenterUV = largest.CenterUV;
                return spell.valid && spell.active;
            }
            spell = Analyze(canvas, out _, out _, out _, out ringCenterUV);
            return spell.valid && spell.active;
        }

        /// <summary>
        /// Lee el sello del lienzo (compuesto o de un solo circulo) y lo reduce a ids y confianzas, sin lanzar nada ni publicar
        /// eventos. Es la misma lectura que usa <see cref="Cast"/>; la usan los portales para comparar con su receta.
        /// </summary>
        public bool ReadSeal(DrawingCanvas canvas, out PortalReading reading)
        {
            reading = null;
            if (canvas == null || canvas.Session.IsEmpty || classifier == null || dictionary == null) return false;
            if (TryAnalyzeNested(canvas, out NestedReading nested))
            {
                reading = PortalReading.FromNested(nested);
                return true;
            }
            reading = PortalReading.FromAst(SealReader.Read(Clean(canvas), classifier, dictionary.Dictionary, GameClock.Time).Ast);
            return true;
        }

        /// <summary>
        /// Lee el lienzo como sello compuesto (ver NestedSealReader). True si lo es; cada sub-lectura queda con su hechizo
        /// final: radio en metros, centro en UV, tinta de sus propios trazos y la amplificacion del exterior aplicada.
        /// Si devuelve false, el lienzo se lee por el camino de siempre (un solo circulo).
        /// </summary>
        private bool TryAnalyzeNested(DrawingCanvas canvas, out NestedReading nested)
        {
            nested = null;
            if (canvas == null || canvas.Session.IsEmpty || classifier == null || dictionary == null) return false;

            Vector2 iso = canvas.UvToIsotropic;
            if (!NestedSealReader.TryRead(canvas.Session.Scaled(iso), simplificationTolerance, splineResolution,
                    classifier, dictionary.Dictionary, GameClock.Time, out nested))
                return false;

            Vector2 worldSize = canvas.WorldSize;
            float worldScale = Mathf.Max(worldSize.x, worldSize.y);
            float amplification = nested.Amplification;
            var own = new List<Core.Stroke>();
            foreach (var sub in nested.Subs)
            {
                SpellIR spell = sub.Reading.Spell;
                spell.analysisBoost = sub.Boost;
                spell.analysisPivot = sub.Pivot;
                spell.sealRadius = sub.RadiusIso * worldScale;
                sub.CenterUV = new Vector2(sub.CenterIso.x / Mathf.Max(iso.x, 1e-6f), sub.CenterIso.y / Mathf.Max(iso.y, 1e-6f));

                own.Clear();
                foreach (int index in sub.Inner.StrokeIndices) own.Add(canvas.Session.Strokes[index]);
                InkKind inkKind = InkMath.Dominant(own);
                spell.inkPower = InkKindInfo.PowerMultiplier(inkKind) * amplification;
                spell.waterproof = InkKindInfo.IsWaterproof(inkKind);
                spell.strength *= amplification;
                spell.effectScale *= NestedMath.EffectScaleFactor(amplification);
                sub.Spell = spell;
            }
            return true;
        }

        /// <summary>
        /// Publica y lanza todos los interiores de un sello compuesto en el mismo instante, cada uno desde su centro con el
        /// mismo marco del lienzo. Devuelve el hechizo del interior mas grande que se lanzo (para la fase del sello).
        /// </summary>
        private SpellIR CastNested(DrawingCanvas canvas, NestedReading nested)
        {
            var summary = new StringBuilder();
            summary.AppendLine($"[Sello compuesto] {nested.Subs.Count} circulos, amplificacion x{nested.Amplification:F2}");
            int launched = 0;
            for (int i = 0; i < nested.Subs.Count; i++)
            {
                NestedSubReading sub = nested.Subs[i];
                summary.Append(PublishDiagnostics(sub.Reading.Ast, sub.Spell, sub.Reading.Predictions, dictionary.Dictionary));
                SealAnalyzed?.Invoke(sub.Reading.Ast, sub.Spell, canvas);
                // Solo los interiores que salen publican SpellCompiled: un interior fallido no debe marcar el sello como fallado.
                if (!sub.Launches) continue;
                launched++;
                SpellCompiled?.Invoke(sub.Spell, canvas);
                if (output != null) output.SpawnSpellEffect(sub.Spell, canvas, sub.CenterUV);
            }

            SpellIR primary = nested.Subs[nested.LargestIndex()].Spell;
            LastSpell = primary;
            string text = summary.ToString();
            DiagnosticBus.OnDiagnosticsUpdated?.Invoke($"<b>[Sello compuesto]</b> {launched} de {nested.Subs.Count} circulos lanzados (x{nested.Amplification:F2})");
            if (recordSeals) Record(canvas, canvas.UvToIsotropic, text);
            NestedCompiled?.Invoke(canvas, launched);
            return primary;
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
            SealReading reading = SealReader.Read(Clean(canvas, out float boost, out Vector2 pivot), classifier, dictionary.Dictionary, GameClock.Time);
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
