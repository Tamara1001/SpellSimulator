using UnityEngine;

namespace SpellSimulator.Compiler
{
    /// <summary>Calidad y estabilidad del sello. Port fiel de compiler/spellQuality.ts de la referencia.</summary>
    public static class SpellQuality
    {
        private const float MaxUnknownsBeforeInstability = 4f;

        public static float Quality(GlyphAST ast)
        {
            float ring = ast.Ring.Found ? ast.Ring.Neatness : 0f;
            float primary = ast.PrimarySigil?.Confidence ?? 0f;
            float signConfidence = 0f;
            foreach (var s in ast.Signs) signConfidence += s.Confidence;
            if (ast.Signs.Count > 0) signConfidence /= ast.Signs.Count;
            float insideScore = 1f - Mathf.Min(1f, ast.Unknowns.Count / 7f);

            return Mathf.Clamp01(
                ring * 0.25f +
                primary * 0.25f +
                (signConfidence > 0f ? signConfidence : primary * 0.7f) * 0.2f +
                ast.Metrics.Neatness * 0.15f +
                ast.Metrics.RadialSymmetry * 0.1f +
                insideScore * 0.05f);
        }

        public static float Stability(GlyphAST ast)
        {
            float ringNeatness = ast.Ring.Found ? ast.Ring.Neatness : 0f;

            float symbolSum = 0f;
            int symbolCount = 0;
            if (ast.PrimarySigil != null && ast.PrimarySigil.Candidate.Neatness > 0f)
            {
                symbolSum += ast.PrimarySigil.Candidate.Neatness;
                symbolCount++;
            }
            foreach (var s in ast.Signs)
            {
                if (s.Candidate.Neatness <= 0f) continue;
                symbolSum += s.Candidate.Neatness;
                symbolCount++;
            }
            float symbolNeatness = symbolCount > 0 ? symbolSum / symbolCount : 0.35f;

            float unknownPenalty = Mathf.Min(0.34f, ast.Unknowns.Count / MaxUnknownsBeforeInstability * 0.24f);
            float ambiguityPenalty = ast.PrimarySigil == null
                ? 0.14f
                : Mathf.Max(0f, ast.PrimarySigil.CompetitorConfidence - ast.PrimarySigil.Confidence + 0.14f);
            float boundaryPenalty = ast.Warnings.Contains(GlyphWarnings.SymbolNearLayerBoundary) ? 0.08f : 0f;
            float centerPenalty = ast.Warnings.Contains(GlyphWarnings.CenterUnknownContamination) ? 0.16f : 0f;

            return Mathf.Clamp01(
                ringNeatness * 0.36f +
                symbolNeatness * 0.34f +
                ast.Metrics.RadialSymmetry * 0.12f +
                (1f - ast.Metrics.Instability) * 0.18f -
                unknownPenalty - ambiguityPenalty - boundaryPenalty - centerPenalty);
        }
    }
}
