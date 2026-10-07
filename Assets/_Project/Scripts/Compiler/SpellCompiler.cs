using UnityEngine;

namespace SpellSimulator.Compiler
{
    /// <summary>
    /// GlyphAST -> SpellIR. Port fiel de compiler/spellBuilder.ts (compileSpell) de wha-spell-simulator.
    /// Logica pura: gates de validez, agregacion de signos y parametros escalares.
    /// La capa Plan de la referencia (resolvePlan) todavia no esta portada (ver ROADMAP F4).
    /// </summary>
    public static class SpellCompiler
    {
        public const float MinimumPrimarySigilConfidence = 0.62f;
        public const float PrimarySigilAmbiguityGap = 0.05f;

        // SPELL_PARAMETER_TUNING
        private const float FocusBase = 0.46f, FocusQuality = 0.2f;
        private const float SpreadBase = 0.32f, SpreadInverseFocus = 0.28f;
        private const float ForceBase = 0.34f, ForceSignPower = 0.34f, ForceQuality = 0.18f;
        private const float RangeBase = 0.42f, RangeSignPower = 0.18f;
        private const float DurationMinSeconds = 0.65f, DurationMaxSeconds = 8.5f, DurationSecondsScale = 6.4f;
        private const float DurationQualityWeight = 0.35f, DurationNeatnessWeight = 0.65f, DurationCurve = 1.45f;

        // PHYSICS_TUNING
        private const float LevitationGravityScale = 0.42f;

        // config.renderer.effectSize
        private const float DefaultReferenceSizeNorm = 0.3f;
        private const float NeutralScale = 1.91f, SizeRatioInfluence = 0.63f, StrengthFullSizeRatio = 2f;
        private const float MinScale = 1f, MaxScale = 2.35f;

        public static SpellIR Compile(GlyphAST ast, float time)
        {
            string[] warnings = ast.Warnings.ToArray();
            float neatness = ast.Metrics.Neatness;

            if (!ast.Ring.Found) return SpellIR.Invalid("Sin anillo", warnings, neatness);
            if (ast.ExtraSigils.Count > 0) return SpellIR.Invalid("Varios sigilos", warnings, neatness);

            var primary = ast.PrimarySigil;
            if (primary == null) return SpellIR.Invalid("Hechizo invalido", warnings, neatness);
            if (primary.Confidence < MinimumPrimarySigilConfidence)
                return SpellIR.Invalid("Hechizo invalido", With(warnings, GlyphWarnings.PrimarySigilConfidenceLow), neatness);
            if (primary.Confidence - primary.SameKindAlternateConfidence < PrimarySigilAmbiguityGap)
                return SpellIR.Invalid("Sigilo ambiguo", With(warnings, GlyphWarnings.PrimarySigilAmbiguous), neatness);
            if (string.IsNullOrEmpty(primary.Entry.Element))
                return SpellIR.Invalid("Elemento no soportado", With(warnings, GlyphWarnings.PrimaryElementMissing), neatness);

            ElementType element = SpellIR.ParseElement(primary.Entry.Element);
            if (element == ElementType.None)
                return SpellIR.Invalid("Elemento no soportado", With(warnings, GlyphWarnings.PrimaryElementUnsupported), neatness);

            var signs = ast.Signs;
            float quality = SpellQuality.Quality(ast);
            float stability = SpellQuality.Stability(ast);
            if (neatness <= 0f) neatness = quality;

            var manifestations = SemanticRules.AggregateManifestations(signs);
            var deltas = SemanticRules.AggregateSemanticDeltas(signs);
            float coherence = 0f;
            Vector2 surface = signs.Count > 0 ? SemanticRules.CombineSignDirection(signs, out coherence) : Vector2.zero;

            float signPower = 0f;
            foreach (var s in signs) signPower += SemanticRules.SignInfluence(s);

            var semantic = primary.Entry.Semantic;
            float referenceSize = primary.Entry.ReferenceSizeNorm > 0f ? primary.Entry.ReferenceSizeNorm : DefaultReferenceSizeNorm;
            float sizeRatio = primary.Candidate.SizeNorm / Mathf.Max(referenceSize, 1e-6f);
            float strength = Mathf.Clamp01(sizeRatio / StrengthFullSizeRatio);
            float effectScale = Mathf.Clamp(NeutralScale + (sizeRatio - 1f) * SizeRatioInfluence, MinScale, MaxScale);

            float focus = Mathf.Clamp01(FocusBase + semantic.focus + deltas.focus + quality * FocusQuality);
            float spread = Mathf.Clamp01(SpreadBase + semantic.spread + deltas.spread + (1f - focus) * SpreadInverseFocus);
            float force = Mathf.Clamp01(ForceBase + semantic.force + signPower * ForceSignPower + deltas.force + quality * ForceQuality);
            float range = Mathf.Clamp01(RangeBase + semantic.range + deltas.range + signPower * RangeSignPower);

            float durationScore = Mathf.Clamp01(
                quality * DurationQualityWeight + neatness * DurationNeatnessWeight + semantic.lifetimeBias + deltas.lifetimeBias);
            float duration = Mathf.Clamp(
                DurationMinSeconds + Mathf.Pow(durationScore, DurationCurve) * DurationSecondsScale,
                DurationMinSeconds, DurationMaxSeconds);

            float levitation = 0f;
            foreach (var m in manifestations) if (m.Id == Manifestations.Levitation) levitation = m.Influence;
            float gravity = Mathf.Clamp01(1f - levitation * LevitationGravityScale);

            Vector3 direction = SpellDirection.FromSurface(surface, force, out float xTilt, out float yTilt);

            // La activacion es el cierre del anillo y nada mas (invariante de la referencia).
            bool active = ast.Ring.Complete;
            SpellPlan plan = PlanResolver.Resolve(PlanResolver.ReadSigns(ast), primary.Id, primary.Entry.Element, quality);
            return new SpellIR
            {
                valid = true,
                active = active,
                prepared = !active,
                status = active ? "Hechizo activo" : "Hechizo preparado",
                warnings = warnings,
                element = element,
                sigilId = primary.Id,
                elementConfidence = primary.Confidence,
                primarySizeNorm = primary.Candidate.SizeNorm,
                sizeRatio = sizeRatio,
                strength = strength,
                effectScale = effectScale,
                primaryManifestation = manifestations[0].Id,
                manifestations = manifestations,
                force = force,
                spread = spread,
                focus = focus,
                range = range,
                gravity = gravity,
                duration = duration,
                stability = stability,
                quality = quality,
                neatness = neatness,
                direction = direction,
                xTiltDeg = xTilt,
                yTiltDeg = yTilt,
                directionCoherence = coherence,
                activatedAt = active ? time : -1f,
                plan = plan
            };
        }

        private static string[] With(string[] warnings, string extra)
        {
            if (System.Array.IndexOf(warnings, extra) >= 0) return warnings;
            var result = new string[warnings.Length + 1];
            warnings.CopyTo(result, 0);
            result[warnings.Length] = extra;
            return result;
        }
    }
}
