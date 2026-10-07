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

        /// <summary>Ultimo hechizo compilado (valido o no). Util para UI y tests.</summary>
        public SpellIR LastSpell { get; private set; }

        /// <summary>Se emite tras cada cast con el resultado compilado.</summary>
        public event System.Action<SpellIR, DrawingCanvas> SpellCompiled;

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

            // Los trazos estan en UV; en un lienzo no cuadrado se pasan a un espacio isotropico.
            DrawingSession isotropic = canvas.Session.Scaled(canvas.UvToIsotropic);
            DrawingSession cleaned = StrokeCleaner.CleanSession(isotropic, simplificationTolerance, splineResolution);

            ParsedSeal seal = SealParser.Parse(cleaned);
            var candidates = new List<DrawingSession>();
            foreach (var c in seal.AllCandidates()) candidates.Add(c.Strokes);

            GlyphPrediction[] predictions = classifier.Classify(candidates);
            GlyphAST ast = GlyphAstBuilder.Build(seal, predictions, dictionary.Dictionary);
            SpellIR spell = SpellCompiler.Compile(ast, Time.time);

            LastSpell = spell;
            PublishDiagnostics(ast, spell, predictions, dictionary.Dictionary);
            SpellCompiled?.Invoke(spell, canvas);

            if (spell.valid && spell.active && output != null)
                output.SpawnSpellEffect(spell, canvas.transform, canvas.SurfaceNormalLocal);
            return spell;
        }

        private static void PublishDiagnostics(GlyphAST ast, SpellIR spell, GlyphPrediction[] predictions, GlyphDictionary dictionary)
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
                log.AppendLine($"Inclinacion x{spell.xTiltDeg:F0} y{spell.yTiltDeg:F0} (coh {spell.directionCoherence:F2})");
            }

            foreach (var w in spell.warnings) log.AppendLine($"<color=#e0a040>! {GlyphWarnings.Describe(w)}</color>");

            string text = log.ToString();
            Debug.Log(text);
            DiagnosticBus.OnDiagnosticsUpdated?.Invoke(text);
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
