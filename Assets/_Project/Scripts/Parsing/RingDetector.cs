using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Core;

namespace SpellSimulator.Parsing
{
    /// <summary>Resultado de la deteccion del anillo de un sello.</summary>
    public class RingInfo
    {
        public bool Found;
        /// <summary>Anillo sellado: el hechizo queda activo. Abierto = preparado.</summary>
        public bool Complete;
        /// <summary>1 si esta cerrado; si no, fraccion de la circunferencia cubierta por tinta.</summary>
        public float Completeness;
        public float Roundness;
        public float Neatness;
        public Vector2 Center;
        public float Radius;
        public readonly List<Stroke> Strokes = new List<Stroke>();

        public static RingInfo NotFound() => new RingInfo { Found = false };

        public bool Contains(Stroke stroke) => Strokes.Contains(stroke);
    }

    /// <summary>
    /// Detecta el anillo: ajusta un circulo a cada trazo candidato, suma los arcos que caen sobre el
    /// mismo circulo y mide cobertura angular. Porta la idea de parser/rings/candidates.ts de la
    /// referencia con heuristicas mas simples (sin analisis topologico).
    /// </summary>
    public static class RingDetector
    {
        // Constantes de parser/rings/constants.ts de la referencia.
        public const float FoundCompleteness = 0.52f;
        public const float MinRoundness = 0.36f;
        public const int BinCount = 96;
        public const float BandHalfWidthRatio = 0.055f;
        public const float JoinNearRatio = 0.45f;

        // Propios: tamanio minimo en unidades del lienzo y tolerancia de cierre.
        public const float MinRadius = 0.06f;
        public const int MinSeedPoints = 10;
        public const float ClosedCoverage = 0.985f;
        public const float NearlyClosedCoverage = 0.9f;
        public const float ClosureGapRatio = 0.2f;

        public static RingInfo Detect(DrawingSession session)
        {
            RingInfo best = RingInfo.NotFound();
            float bestScore = 0f;

            foreach (var seed in session.Strokes)
            {
                if (seed.Points.Count < MinSeedPoints) continue;
                if (!FitCircle(seed.Points, out Vector2 center, out float radius) || radius < MinRadius) continue;

                var ring = new RingInfo { Center = center, Radius = radius };
                ring.Strokes.Add(seed);
                foreach (var other in session.Strokes)
                {
                    if (other != seed && NearRatio(other, center, radius) >= JoinNearRatio)
                        ring.Strokes.Add(other);
                }

                Measure(ring);
                if (!ring.Found || IsGlyphCircle(ring, session)) continue;

                float score = ring.Radius * ring.Roundness * ring.Completeness;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = ring;
                }
            }

            return best;
        }

        private static void Measure(RingInfo ring)
        {
            float r = ring.Radius;
            float band = Mathf.Max(r * BandHalfWidthRatio, 1e-4f);

            var bins = new bool[BinCount];
            float inkLength = 0f, nearLength = 0f;
            float sumDev = 0f, sumDevSq = 0f;
            int pointCount = 0;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;

            foreach (var stroke in ring.Strokes)
            {
                for (int i = 0; i < stroke.Points.Count; i++)
                {
                    Vector2 p = stroke.Points[i];
                    float deviation = Vector2.Distance(p, ring.Center) / r - 1f;
                    sumDev += deviation;
                    sumDevSq += deviation * deviation;
                    pointCount++;
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);

                    if (i == 0) continue;
                    Vector2 a = stroke.Points[i - 1];
                    float segment = Vector2.Distance(a, p);
                    inkLength += segment;
                    if (Mathf.Abs(Vector2.Distance((a + p) * 0.5f, ring.Center) - r) <= band) nearLength += segment;
                    MarkBins(bins, ring.Center, a, p);
                }
            }

            int covered = 0;
            foreach (bool b in bins) if (b) covered++;
            float coverage = (float)covered / BinCount;

            float meanDev = sumDev / Mathf.Max(1, pointCount);
            float residual = Mathf.Sqrt(Mathf.Max(0f, sumDevSq / Mathf.Max(1, pointCount) - meanDev * meanDev));
            Vector2 size = max - min;
            float aspect = Mathf.Min(size.x, size.y) / Mathf.Max(1e-6f, Mathf.Max(size.x, size.y));
            ring.Roundness = Mathf.Clamp01((1f - residual * 3.1f) * 0.78f + aspect * 0.22f);

            ring.Complete = coverage >= ClosedCoverage ||
                            (coverage >= NearlyClosedCoverage && EndpointGap(ring) <= r * ClosureGapRatio);
            ring.Completeness = ring.Complete ? 1f : coverage;
            ring.Found = ring.Completeness >= FoundCompleteness && ring.Roundness >= MinRoundness;

            float circumference = 2f * Mathf.PI * r;
            float overdraw = Mathf.Max(0f, inkLength / circumference - 1.08f);
            float nearInkRatio = inkLength > 0f ? nearLength / inkLength : 0f;
            float closureQuality = ring.Complete ? ring.Roundness : coverage;
            float lineSmoothness = Mathf.Clamp01(nearInkRatio * 0.72f + (1f - residual) * 0.28f - overdraw * 0.12f);
            ring.Neatness = Mathf.Clamp01(ring.Roundness * 0.42f + lineSmoothness * 0.36f + closureQuality * 0.22f);
        }

        // parser/rings/constants.ts (GLYPH_CIRCLE_*): un circulo con colas o atravesado por trazos es parte de un glifo.
        public const float GlyphBandHalfWidthRatio = 0.13f;
        public const float GlyphMaxTailRatio = 0.24f;
        public const float GlyphTailWithCrossingRatio = 0.12f;
        public const float GlyphCrossInner = 0.85f;
        public const float GlyphCrossOuter = 1.15f;
        public const int GlyphMinCrossingStrokes = 2;

        /// <summary>
        /// Rechaza circulos que forman parte de un glifo (glyphCircle.ts): mucha tinta del propio trazo fuera de la linea
        /// del circulo (colas), o trazos ajenos que lo cruzan de adentro hacia afuera.
        /// </summary>
        public static bool IsGlyphCircle(RingInfo ring, DrawingSession session)
        {
            float band = ring.Radius * GlyphBandHalfWidthRatio;
            float far = 0f;
            foreach (var stroke in ring.Strokes)
            {
                for (int i = 1; i < stroke.Points.Count; i++)
                {
                    Vector2 a = stroke.Points[i - 1], b = stroke.Points[i];
                    if (Mathf.Abs(Vector2.Distance((a + b) * 0.5f, ring.Center) - ring.Radius) > band)
                        far += Vector2.Distance(a, b);
                }
            }
            float tailRatio = far / (2f * Mathf.PI * ring.Radius);

            int crossing = 0;
            foreach (var stroke in session.Strokes)
            {
                if (ring.Contains(stroke)) continue;
                bool inside = false, outside = false;
                foreach (var p in stroke.Points)
                {
                    float r = Vector2.Distance(p, ring.Center) / ring.Radius;
                    inside |= r <= GlyphCrossInner;
                    outside |= r >= GlyphCrossOuter;
                }
                if (inside && outside) crossing++;
            }

            return tailRatio > GlyphMaxTailRatio ||
                   crossing >= GlyphMinCrossingStrokes ||
                   (crossing >= 1 && tailRatio > GlyphTailWithCrossingRatio);
        }

        private static void MarkBins(bool[] bins, Vector2 center, Vector2 a, Vector2 b)
        {
            int from = AngleBin(center, a);
            int to = AngleBin(center, b);
            int forward = (to - from + BinCount) % BinCount;
            // Un segmento corto nunca cubre mas de media vuelta: recorrer por el lado mas corto.
            if (forward <= BinCount / 2)
                for (int k = 0; k <= forward; k++) bins[(from + k) % BinCount] = true;
            else
                for (int k = 0; k <= BinCount - forward; k++) bins[(to + k) % BinCount] = true;
        }

        private static int AngleBin(Vector2 center, Vector2 p)
        {
            return Mathf.Min(BinCount - 1, (int)(GeometryUtils.AngleDeg(p - center) / 360f * BinCount));
        }

        private static float EndpointGap(RingInfo ring)
        {
            // Menor distancia entre extremos de trazos distintos o del mismo trazo.
            var ends = new List<Vector2>();
            foreach (var s in ring.Strokes)
            {
                if (s.Points.Count < 2) continue;
                ends.Add(s.Points[0]);
                ends.Add(s.Points[s.Points.Count - 1]);
            }
            if (ring.Strokes.Count == 1 && ends.Count == 2) return Vector2.Distance(ends[0], ends[1]);

            // Varios arcos: el hueco es el mayor de los saltos minimos entre cada extremo y el mas cercano.
            float worst = 0f;
            for (int i = 0; i < ends.Count; i++)
            {
                float nearest = float.MaxValue;
                for (int j = 0; j < ends.Count; j++)
                {
                    if (j == i || j == (i ^ 1)) continue;
                    nearest = Mathf.Min(nearest, Vector2.Distance(ends[i], ends[j]));
                }
                if (nearest < float.MaxValue) worst = Mathf.Max(worst, nearest);
            }
            return worst;
        }

        private static float NearRatio(Stroke stroke, Vector2 center, float radius)
        {
            if (stroke.Points.Count == 0) return 0f;
            float band = Mathf.Max(radius * BandHalfWidthRatio, 1e-4f);
            int near = 0;
            foreach (var p in stroke.Points)
                if (Mathf.Abs(Vector2.Distance(p, center) - radius) <= band) near++;
            return (float)near / stroke.Points.Count;
        }

        /// <summary>
        /// Ajuste algebraico de circulo (Kasa): minimiza x^2 + y^2 + Dx + Ey + F. Funciona con arcos abiertos,
        /// a diferencia del centro del bounding box.
        /// </summary>
        public static bool FitCircle(IReadOnlyList<Vector2> points, out Vector2 center, out float radius)
        {
            center = Vector2.zero;
            radius = 0f;
            int n = points.Count;
            if (n < 3) return false;

            // Centrar para estabilidad numerica.
            Vector2 mean = Vector2.zero;
            foreach (var p in points) mean += p;
            mean /= n;

            double suu = 0, suv = 0, svv = 0, suuu = 0, svvv = 0, suvv = 0, svuu = 0;
            foreach (var p in points)
            {
                double u = p.x - mean.x, v = p.y - mean.y;
                suu += u * u; suv += u * v; svv += v * v;
                suuu += u * u * u; svvv += v * v * v;
                suvv += u * v * v; svuu += v * u * u;
            }

            double det = suu * svv - suv * suv;
            if (System.Math.Abs(det) < 1e-12) return false;

            double bu = 0.5 * (suuu + suvv);
            double bv = 0.5 * (svvv + svuu);
            double uc = (bu * svv - bv * suv) / det;
            double vc = (bv * suu - bu * suv) / det;

            center = mean + new Vector2((float)uc, (float)vc);
            radius = (float)System.Math.Sqrt(uc * uc + vc * vc + (suu + svv) / n);
            return radius > 0f && !float.IsNaN(radius);
        }
    }
}
