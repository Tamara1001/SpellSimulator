using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Core;
using SpellSimulator.Parsing;

namespace SpellSimulator.Compiler
{
    /// <summary>Suma ponderada de los deltas semanticos de todos los signos.</summary>
    public struct SemanticDeltas
    {
        public float force, focus, spread, range, lifetimeBias;
    }

    /// <summary>
    /// Como se combinan los signos reconocidos. Port fiel de compiler/semanticRules.ts de la referencia.
    /// El significado de cada signo NO vive aca: esta en el bloque semantic del diccionario.
    /// </summary>
    public static class SemanticRules
    {
        private const float InwardDirectionOffsetDeg = 180f;

        private static class Shape
        {
            public const float ForceImbalanceOffset = 0.08f, ForceImbalanceScale = 0.34f, ForceMax = 0.18f;
            public const float FocusElongationOffset = 0.12f, FocusElongationScale = 0.2f, FocusMax = 0.12f;
            public const float DirectionAxisOffset = 0.1f, DirectionAxisScale = 0.95f, DirectionMax = 0.58f;
        }

        private static class Influence
        {
            public const float SizeBase = 0.68f, SizeScale = 2.4f, SizeMin = 0.45f, SizeMax = 1.25f;
            public const float LengthBase = 0.72f, LengthScale = 1.8f, LengthMin = 0.45f, LengthMax = 1.22f;
            public const float LayerOuter = 1f, LayerMiddle = 0.88f, LayerOther = 0.62f;
            public const float DistanceBase = 0.76f, DistanceScale = 0.34f, DistanceMin = 0.58f, DistanceMax = 1.14f;
            public const float FeatureBoostBase = 1f, FeatureBoostMin = 0.35f, FeatureBoostMax = 1.85f;
            public const float MinimumDirectionMagnitude = 0.001f;
        }

        private static class Convergence
        {
            public const float PointScale = 0.42f, PointLimit = 0.5f;
            public const float RadiusBase = 0.3f, RadiusStrengthScale = 0.16f, RadiusSizeScale = 0.42f;
            public const float RadiusInnerBiasScale = 0.04f, RadiusMin = 0.06f, RadiusMax = 0.3f;
            public const float RigidityBase = 0.58f, RigiditySizeScale = 2.1f, RigidityRadiusScale = 0.18f;
        }

        public static float SignInfluence(GlyphRecognition sign)
        {
            SymbolCandidate c = sign.Candidate;
            float size = Mathf.Clamp(Influence.SizeBase + c.SizeNorm * Influence.SizeScale, Influence.SizeMin, Influence.SizeMax);
            float length = Mathf.Clamp(Influence.LengthBase + c.LengthNorm * Influence.LengthScale, Influence.LengthMin, Influence.LengthMax);
            float layer = c.Layer == RingLayer.Outer ? Influence.LayerOuter
                : c.Layer == RingLayer.Middle ? Influence.LayerMiddle
                : Influence.LayerOther;
            float distance = Mathf.Clamp(Influence.DistanceBase + c.RadiusNorm * Influence.DistanceScale, Influence.DistanceMin, Influence.DistanceMax);
            return Mathf.Clamp01(sign.Confidence * c.Neatness * size * length * layer * distance);
        }

        private static string ManifestationId(GlyphRecognition sign)
        {
            return string.IsNullOrEmpty(sign.Entry.Semantic.manifestation) ? sign.Id : sign.Entry.Semantic.manifestation;
        }

        private static Vector2 SignDirection(GlyphRecognition sign)
        {
            switch (sign.Entry.Semantic.directionMode)
            {
                case "orientation": return GeometryUtils.VectorFromAngleDeg(sign.Candidate.DirectedOrientationDeg);
                case "inward": return GeometryUtils.VectorFromAngleDeg(sign.Candidate.AngleDeg + InwardDirectionOffsetDeg);
                default: return GeometryUtils.VectorFromAngleDeg(sign.Candidate.AngleDeg);
            }
        }

        private static SemanticDeltas ShapeDeltas(GlyphRecognition sign, out float directionWeight)
        {
            SymbolCandidate c = sign.Candidate;
            directionWeight = Mathf.Clamp((c.AxisDominance - Shape.DirectionAxisOffset) * Shape.DirectionAxisScale, 0f, Shape.DirectionMax);
            return new SemanticDeltas
            {
                force = Mathf.Clamp((c.StrokeLengthImbalance - Shape.ForceImbalanceOffset) * Shape.ForceImbalanceScale, 0f, Shape.ForceMax),
                focus = Mathf.Clamp((c.ElongationNorm - Shape.FocusElongationOffset) * Shape.FocusElongationScale, 0f, Shape.FocusMax)
            };
        }

        private static float SignDirectionWeight(GlyphRecognition sign)
        {
            ShapeDeltas(sign, out float directionWeight);
            return SignInfluence(sign) * Mathf.Clamp(Influence.FeatureBoostBase + directionWeight, Influence.FeatureBoostMin, Influence.FeatureBoostMax);
        }

        /// <summary>Agrupa signos por familia de manifestacion. Sin signos: aura con fuerza 1.</summary>
        public static ManifestationStrength[] AggregateManifestations(IReadOnlyList<GlyphRecognition> signs)
        {
            if (signs.Count == 0)
                return new[] { new ManifestationStrength { Id = Manifestations.Aura, Strength = 1f, Influence = 0f } };

            var order = new List<string>();
            var groups = new Dictionary<string, List<(GlyphRecognition sign, float influence)>>();
            foreach (var sign in signs)
            {
                string id = ManifestationId(sign);
                if (!groups.TryGetValue(id, out var list))
                {
                    list = new List<(GlyphRecognition, float)>();
                    groups[id] = list;
                    order.Add(id);
                }
                list.Add((sign, SignInfluence(sign)));
            }

            var result = new List<ManifestationStrength>();
            foreach (string id in order)
            {
                float total = 0f;
                foreach (var entry in groups[id]) total += entry.influence;
                var m = new ManifestationStrength { Id = id, Influence = total, Strength = Mathf.Clamp01(total) };
                if (id == Manifestations.Convergence) ApplyConvergenceProfile(ref m, groups[id]);
                result.Add(m);
            }
            // Orden estable (como Array.sort de JS) por influencia descendente: el primero es la primaria.
            return System.Linq.Enumerable.ToArray(System.Linq.Enumerable.OrderByDescending(result, m => m.Influence));
        }

        private static void ApplyConvergenceProfile(ref ManifestationStrength m, List<(GlyphRecognition sign, float influence)> signs)
        {
            float totalInfluence = 0f;
            foreach (var s in signs) totalInfluence += s.influence;
            if (totalInfluence <= 0f)
            {
                m.Radius = Convergence.RadiusMax;
                return;
            }

            Vector2 weighted = Vector2.zero;
            float placementWeight = 0f, size = 0f, radius = 0f;
            foreach (var (sign, influence) in signs)
            {
                float radial = Mathf.Clamp01(sign.Candidate.RadiusNorm);
                float w = influence * (0.7f + radial * 0.45f);
                weighted += GeometryUtils.VectorFromAngleDeg(sign.Candidate.AngleDeg) * radial * w;
                placementWeight += w;
                size += sign.Candidate.SizeNorm * influence;
                radius += radial * influence;
            }

            float averageSize = size / totalInfluence;
            float averageRadius = radius / totalInfluence;
            Vector2 point = placementWeight > 0f ? weighted / placementWeight * Convergence.PointScale : Vector2.zero;
            m.Point = new Vector2(
                Mathf.Clamp(point.x, -Convergence.PointLimit, Convergence.PointLimit),
                Mathf.Clamp(point.y, -Convergence.PointLimit, Convergence.PointLimit));
            m.Radius = Mathf.Clamp(
                Convergence.RadiusBase - m.Strength * Convergence.RadiusStrengthScale -
                averageSize * Convergence.RadiusSizeScale + (1f - averageRadius) * Convergence.RadiusInnerBiasScale,
                Convergence.RadiusMin, Convergence.RadiusMax);
            m.Rigidity = Mathf.Clamp01(m.Strength * (Convergence.RigidityBase +
                averageSize * Convergence.RigiditySizeScale + averageRadius * Convergence.RigidityRadiusScale));
        }

        /// <summary>Direccion de superficie combinada. strength = coherencia (0 si los signos se cancelan).</summary>
        public static Vector2 CombineSignDirection(IReadOnlyList<GlyphRecognition> signs, out float strength)
        {
            Vector2 sum = Vector2.zero;
            float weight = 0f;
            foreach (var sign in signs)
            {
                float w = SignDirectionWeight(sign);
                sum += SignDirection(sign) * w;
                weight += w;
            }

            float magnitude = sum.magnitude;
            if (magnitude < Influence.MinimumDirectionMagnitude)
            {
                strength = 0f;
                return Vector2.zero;
            }
            strength = Mathf.Clamp01(magnitude / Mathf.Max(Influence.MinimumDirectionMagnitude, weight));
            return sum / magnitude;
        }

        public static SemanticDeltas AggregateSemanticDeltas(IReadOnlyList<GlyphRecognition> signs)
        {
            var total = new SemanticDeltas();
            foreach (var sign in signs)
            {
                float influence = SignInfluence(sign);
                var semantic = sign.Entry.Semantic;
                var shape = ShapeDeltas(sign, out _);
                total.force += (semantic.force + shape.force) * influence;
                total.focus += (semantic.focus + shape.focus) * influence;
                total.spread += (semantic.spread + shape.spread) * influence;
                total.range += (semantic.range + shape.range) * influence;
                total.lifetimeBias += (semantic.lifetimeBias + shape.lifetimeBias) * influence;
            }
            return total;
        }
    }
}
