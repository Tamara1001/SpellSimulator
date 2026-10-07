using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Glyphs;
using SpellSimulator.Parsing;
using SpellSimulator.Recognition;

namespace SpellSimulator.Compiler
{
    /// <summary>
    /// Une geometria (ParsedSeal) + predicciones del modelo + diccionario en un GlyphAST.
    /// Porta parser/classifier/assembly.ts y metrics.ts de la referencia.
    /// </summary>
    public static class GlyphAstBuilder
    {
        /// <summary>Piso de confianza para aceptar un glifo (recognition.minConfidence).</summary>
        public const float MinConfidence = 0.48f;
        /// <summary>Diferencia minima contra la segunda clase para no marcar ambiguedad.</summary>
        public const float AmbiguityMargin = 0.12f;
        public const float MessyNeatness = 0.5f;

        /// <param name="predictions">Una por candidato, en el orden de seal.AllCandidates().</param>
        public static GlyphAST Build(ParsedSeal seal, IReadOnlyList<GlyphPrediction> predictions, GlyphDictionary dictionary)
        {
            var ast = new GlyphAST { Ring = seal.Ring };
            if (!seal.Ring.Found) ast.Warn(GlyphWarnings.NoRingDetected);
            else if (!seal.Ring.Complete) ast.Warn(GlyphWarnings.RingIncomplete);

            var sigils = new List<GlyphRecognition>();
            int index = 0;
            foreach (var candidate in seal.AllCandidates())
            {
                var recognition = Recognize(candidate, predictions[index++], dictionary, seal.Ring.Found);
                if (!recognition.Recognized) ast.Unknowns.Add(recognition);
                else if (recognition.Kind == GlyphKind.Sigil) sigils.Add(recognition);
                else ast.Signs.Add(recognition);
            }

            sigils.Sort((a, b) => PrimaryScore(b).CompareTo(PrimaryScore(a)));
            if (sigils.Count > 0) ast.PrimarySigil = sigils[0];
            for (int i = 1; i < sigils.Count; i++) ast.ExtraSigils.Add(sigils[i]);

            CollectWarnings(ast);
            ast.Metrics = CalculateMetrics(ast);
            return ast;
        }

        private static GlyphRecognition Recognize(SymbolCandidate candidate, GlyphPrediction prediction, GlyphDictionary dictionary, bool hasRing)
        {
            var entry = dictionary.Get(prediction.BestIndex);
            var r = new GlyphRecognition
            {
                Candidate = candidate,
                Entry = entry,
                Confidence = prediction.Confidence,
                RotationDeg = prediction.RotationDeg
            };

            float[] p = prediction.Probabilities;
            for (int k = 0; k < p.Length; k++)
            {
                if (k == prediction.BestIndex) continue;
                r.CompetitorConfidence = Mathf.Max(r.CompetitorConfidence, p[k]);
                if (dictionary.Get(k).Kind == entry.Kind) r.SameKindAlternateConfidence = Mathf.Max(r.SameKindAlternateConfidence, p[k]);
            }

            if (entry.Kind == GlyphKind.Unknown) r.RejectReason = "unknown_class";
            else if (r.Confidence < MinConfidence) r.RejectReason = "low_confidence";
            else if (hasRing && !LayerAllowed(entry, candidate.Layer)) r.RejectReason = "wrong_layer";
            r.Recognized = r.RejectReason == null;
            return r;
        }

        private static bool LayerAllowed(GlyphEntry entry, RingLayer layer)
        {
            if (entry.AllowedLayers.Length == 0) return true;
            string name = layer switch
            {
                RingLayer.Center => "center",
                RingLayer.Middle => "middle",
                RingLayer.Outer => "outer",
                _ => "boundary"
            };
            return System.Array.IndexOf(entry.AllowedLayers, name) >= 0;
        }

        private static float PrimaryScore(GlyphRecognition sigil)
        {
            float bonus = sigil.Candidate.Layer == RingLayer.Center ? 0.12f : sigil.Candidate.RadiusNorm <= 0.45f ? 0.06f : 0f;
            return sigil.Confidence + bonus;
        }

        private static void CollectWarnings(GlyphAST ast)
        {
            if (ast.ExtraSigils.Count > 0) ast.Warn(GlyphWarnings.UnsupportedMultipleSigils);
            if (ast.PrimarySigil == null) ast.Warn(GlyphWarnings.MissingPrimarySigil);

            foreach (var u in ast.Unknowns)
            {
                if (u.Candidate.RadiusNorm <= 0.36f) ast.Warn(GlyphWarnings.CenterUnknownContamination);
                if (u.RejectReason == "wrong_layer") ast.Warn(GlyphWarnings.SymbolWrongLayer);
            }

            foreach (var r in Recognized(ast))
            {
                if (r.Candidate.NearBoundary) ast.Warn(GlyphWarnings.SymbolNearLayerBoundary);
                if (r.IsAmbiguous) ast.Warn(GlyphWarnings.SymbolAmbiguous);
                if (r.Candidate.Neatness < MessyNeatness) ast.Warn(GlyphWarnings.SymbolMessy);
            }
        }

        private static IEnumerable<GlyphRecognition> Recognized(GlyphAST ast)
        {
            if (ast.PrimarySigil != null) yield return ast.PrimarySigil;
            foreach (var s in ast.ExtraSigils) yield return s;
            foreach (var s in ast.Signs) yield return s;
        }

        private static GlobalMetrics CalculateMetrics(GlyphAST ast)
        {
            float neatnessSum = ast.Ring.Neatness;
            int neatnessCount = ast.Ring.Neatness > 0f ? 1 : 0;
            int ambiguous = 0, messy = 0;
            foreach (var r in Recognized(ast))
            {
                if (r.Candidate.Neatness > 0f) { neatnessSum += r.Candidate.Neatness; neatnessCount++; }
                if (r.IsAmbiguous) ambiguous++;
                if (r.Candidate.Neatness < MessyNeatness) messy++;
            }

            Vector2 bias = Vector2.zero;
            foreach (var s in ast.Signs)
            {
                float weight = s.Confidence * s.Candidate.Neatness * Mathf.Max(0.3f, s.Candidate.SizeNorm + s.Candidate.LengthNorm);
                bias += Core.GeometryUtils.VectorFromAngleDeg(s.Candidate.AngleDeg) * weight;
            }
            float biasMagnitude = bias.magnitude < 0.001f ? 0f : 1f;

            float ringNeatness = ast.Ring.Found ? ast.Ring.Neatness : 0.4f;
            return new GlobalMetrics
            {
                Neatness = neatnessCount > 0 ? Mathf.Clamp01(neatnessSum / neatnessCount) : 0f,
                RadialSymmetry = Mathf.Clamp01(1f - biasMagnitude * 0.35f),
                Instability = Mathf.Clamp01(
                    0.22f +
                    Mathf.Clamp01(ast.Unknowns.Count / 6f) * 0.34f +
                    Mathf.Clamp01(ambiguous / 5f) * 0.12f +
                    Mathf.Clamp01(messy / 8f) * 0.08f +
                    (1f - ringNeatness) * 0.36f)
            };
        }
    }
}
