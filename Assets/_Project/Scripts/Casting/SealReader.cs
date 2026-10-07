using System.Collections.Generic;
using SpellSimulator.Compiler;
using SpellSimulator.Core;
using SpellSimulator.Glyphs;
using SpellSimulator.Parsing;
using SpellSimulator.Recognition;

namespace SpellSimulator.Casting
{
    /// <summary>Una lectura completa de un sello: AST, hechizo compilado y predicciones de sus candidatos.</summary>
    public struct SealReading
    {
        public GlyphAST Ast;
        public SpellIR Spell;
        public GlyphPrediction[] Predictions;

        /// <summary>Valido gana; si empatan, gana el sigilo mas confiable.</summary>
        public bool IsBetterThan(SealReading other)
        {
            if (Spell.valid != other.Spell.valid) return Spell.valid;
            return (Ast.PrimarySigil?.Confidence ?? 0f) > (other.Ast.PrimarySigil?.Confidence ?? 0f);
        }
    }

    /// <summary>
    /// Lectura guiada por reconocimiento (aproximacion barata a la busqueda de particiones de la referencia):
    /// prueba varias agrupaciones del mismo dibujo y se queda con la que el modelo entiende mejor.
    /// 1) afinidad, 2) afinidad con la capa media fusionada al sigilo (en el mismo batch), 3) radial si nada fue valido.
    /// </summary>
    public static class SealReader
    {
        public static SealReading Read(DrawingSession cleaned, IGlyphClassifier classifier, GlyphDictionary dictionary, float time)
        {
            SealReading best = ReadWithMergeHypothesis(SealParser.Parse(cleaned, GroupingMode.Affinity), classifier, dictionary, time);
            if (best.Spell.valid || !best.Ast.Ring.Found) return best;

            SealReading radial = ReadWithMergeHypothesis(SealParser.Parse(cleaned, GroupingMode.Radial), classifier, dictionary, time);
            return radial.IsBetterThan(best) ? radial : best;
        }

        private static SealReading ReadWithMergeHypothesis(ParsedSeal seal, IGlyphClassifier classifier, GlyphDictionary dictionary, float time)
        {
            ParsedSeal merged = SealParser.MergeMiddleIntoSigil(seal);

            var candidates = new List<SymbolCandidate>(seal.AllCandidates());
            var sessions = new List<DrawingSession>();
            foreach (var c in candidates) sessions.Add(c.Strokes);
            if (merged != null) sessions.Add(merged.SigilCandidate.Strokes);

            GlyphPrediction[] all = classifier.Classify(sessions);
            var byCandidate = new Dictionary<SymbolCandidate, GlyphPrediction>();
            for (int i = 0; i < candidates.Count; i++) byCandidate[candidates[i]] = all[i];

            SealReading split = Build(seal, byCandidate, dictionary, time);
            if (merged == null) return split;

            byCandidate[merged.SigilCandidate] = all[all.Length - 1];
            SealReading fused = Build(merged, byCandidate, dictionary, time);
            return fused.IsBetterThan(split) ? fused : split;
        }

        private static SealReading Build(ParsedSeal seal, Dictionary<SymbolCandidate, GlyphPrediction> byCandidate, GlyphDictionary dictionary, float time)
        {
            var predictions = new List<GlyphPrediction>();
            foreach (var c in seal.AllCandidates()) predictions.Add(byCandidate[c]);

            var reading = new SealReading { Predictions = predictions.ToArray() };
            reading.Ast = GlyphAstBuilder.Build(seal, reading.Predictions, dictionary);
            reading.Spell = SpellCompiler.Compile(reading.Ast, time);
            return reading;
        }
    }
}
