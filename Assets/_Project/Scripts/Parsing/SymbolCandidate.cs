using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Core;

namespace SpellSimulator.Parsing
{
    /// <summary>Capas radiales del sello (config.layers de la referencia).</summary>
    public enum RingLayer { Center, Middle, Outer, RingBoundary, Outside }

    public static class RingLayers
    {
        public const float CenterMax = 0.32f;
        public const float MiddleMax = 0.66f;
        public const float OuterMax = 0.94f;
        public const float BoundaryMax = 1.06f;
        public const float BoundaryTolerance = 0.055f;

        public static RingLayer FromRadius(float radiusNorm, out bool nearBoundary)
        {
            RingLayer layer = radiusNorm <= CenterMax ? RingLayer.Center
                : radiusNorm <= MiddleMax ? RingLayer.Middle
                : radiusNorm <= OuterMax ? RingLayer.Outer
                : radiusNorm <= BoundaryMax ? RingLayer.RingBoundary
                : RingLayer.Outside;

            float nearest = Mathf.Min(
                Mathf.Abs(radiusNorm - CenterMax),
                Mathf.Min(Mathf.Abs(radiusNorm - MiddleMax), Mathf.Abs(radiusNorm - OuterMax)));
            nearBoundary = Mathf.Clamp01(nearest / BoundaryTolerance) < 0.55f;
            return layer;
        }
    }

    /// <summary>
    /// Grupo de trazos que se cree un unico glifo, con sus metricas relativas al anillo.
    /// Porta parser/grouping/candidate.ts y candidateFeatures() de la referencia.
    /// </summary>
    public class SymbolCandidate
    {
        public readonly DrawingSession Strokes;
        public Vector2 Center;
        /// <summary>Distancia al centro del anillo / radio del anillo.</summary>
        public float RadiusNorm;
        /// <summary>Posicion angular alrededor del anillo (grados, Y arriba).</summary>
        public float AngleDeg;
        public RingLayer Layer;
        public bool NearBoundary;
        /// <summary>Tamanio (lado mayor del bbox) / diametro del anillo.</summary>
        public float SizeNorm;
        /// <summary>Longitud de tinta / circunferencia del anillo.</summary>
        public float LengthNorm;
        public float OrientationDeg;
        public float DirectedOrientationDeg;
        public float Neatness;
        public float ElongationNorm;
        public float StrokeLengthImbalance;
        public float AxisDominance;

        public SymbolCandidate(DrawingSession strokes)
        {
            Strokes = strokes;
        }

        public static SymbolCandidate Build(DrawingSession strokes, Vector2 ringCenter, float ringRadius)
        {
            var c = new SymbolCandidate(strokes);
            strokes.GetBounds(out Vector2 min, out Vector2 max);
            Vector2 size = max - min;
            c.Center = (min + max) * 0.5f;

            float safeRadius = Mathf.Max(ringRadius, 1e-6f);
            Vector2 offset = c.Center - ringCenter;
            c.RadiusNorm = offset.magnitude / safeRadius;
            c.AngleDeg = GeometryUtils.AngleDeg(offset);
            c.Layer = RingLayers.FromRadius(c.RadiusNorm, out c.NearBoundary);

            var lengths = new List<float>(strokes.Strokes.Count);
            float totalLength = 0f;
            foreach (var s in strokes.Strokes)
            {
                float l = GeometryUtils.StrokeLength(s);
                lengths.Add(l);
                totalLength += l;
            }
            lengths.Sort((a, b) => b.CompareTo(a));

            float largest = Mathf.Max(size.x, size.y);
            c.SizeNorm = largest / (2f * safeRadius);
            c.LengthNorm = totalLength / (2f * Mathf.PI * safeRadius);
            c.OrientationDeg = GeometryUtils.DominantAxisOrientationDeg(strokes.Strokes);
            c.DirectedOrientationDeg = GeometryUtils.DirectedStrokeAngleDeg(strokes.Strokes);

            float compactPerimeter = Mathf.Max(1e-6f, (size.x + size.y) * 2f);
            float overdraw = Mathf.Clamp01(totalLength / compactPerimeter - 0.72f);
            c.Neatness = Mathf.Clamp01(0.92f - overdraw * 0.28f - Mathf.Max(0, strokes.Strokes.Count - 4) * 0.035f);

            float shortest = Mathf.Max(1e-6f, Mathf.Min(size.x, size.y));
            c.ElongationNorm = Mathf.Clamp01((largest / shortest - 1f) / 3f);
            c.StrokeLengthImbalance = lengths.Count > 1
                ? (lengths[0] - lengths[1]) / Mathf.Max(1e-6f, totalLength)
                : 0f;
            c.AxisDominance = Mathf.Clamp01(c.StrokeLengthImbalance * 1.35f + c.ElongationNorm * 0.35f);
            return c;
        }
    }
}
