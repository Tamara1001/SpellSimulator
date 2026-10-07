using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Core;

namespace SpellSimulator.Parsing
{
    /// <summary>Como se agrupan los trazos en candidatos.</summary>
    public enum GroupingMode
    {
        /// <summary>Por afinidad (trazos que se tocan, encierran o comparten el centro). Default, como la referencia.</summary>
        Affinity,
        /// <summary>Por radio: todo lo cercano al centro es sigilo. Alternativa cuando la afinidad funde un signo con el sigilo.</summary>
        Radial
    }

    /// <summary>
    /// Agrupa los trazos que no son anillo en candidatos a glifo.
    /// Afinidad portada de parser/grouping/affinity.ts: 1 si la tinta se toca, decae a 0 a 0.2 radios; los trazos encerrados
    /// por otro y los que comparten la zona central se unen. Componentes conexas con afinidad >= 0.2.
    /// La referencia ademas hace busqueda de particiones guiada por reconocimiento; aca se aproxima probando ambos modos.
    /// </summary>
    public static class StrokeGrouper
    {
        // parser/grouping/constants.ts
        public const float AffinityReachNorm = 0.2f;
        public const float ComponentAffinityMin = 0.2f;
        public const float CenterAffinityDistanceNorm = 0.54f;
        public const float EnclosurePadNorm = 0.04f;

        /// <summary>Radio normalizado maximo del centro de un grupo para ser el sigilo.</summary>
        public const float SigilRadiusMax = 0.45f;
        /// <summary>Modo radial: radio del centro de masa de un trazo para pertenecer al sigilo.</summary>
        public const float RadialSigilRadiusMax = 0.40f;
        /// <summary>Modo radial: separacion entre bounding boxes (fraccion del radio) para fusionar trazos de un signo.</summary>
        public const float ClusterGapRatio = 0.12f;

        public struct Groups
        {
            public DrawingSession Sigil;
            public List<DrawingSession> Signs;
        }

        public static Groups Group(DrawingSession session, RingInfo ring, GroupingMode mode = GroupingMode.Affinity)
        {
            var groups = new Groups { Sigil = new DrawingSession(), Signs = new List<DrawingSession>() };
            if (!ring.Found)
            {
                foreach (var s in session.Strokes) groups.Sigil.AddStroke(s);
                return groups;
            }

            var strokes = new List<Stroke>();
            foreach (var s in session.Strokes)
                if (!ring.Contains(s) && s.Points.Count > 0) strokes.Add(s);

            return mode == GroupingMode.Affinity ? GroupByAffinity(strokes, ring) : GroupRadially(strokes, ring);
        }

        private static Groups GroupByAffinity(List<Stroke> strokes, RingInfo ring)
        {
            int n = strokes.Count;
            var bounds = new (Vector2 min, Vector2 max)[n];
            for (int i = 0; i < n; i++)
            {
                GeometryUtils.GetBounds(new[] { strokes[i] }, out var min, out var max);
                bounds[i] = (min, max);
            }

            var parent = NewParents(n);
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if (Affinity(strokes[i], bounds[i], strokes[j], bounds[j], ring) >= ComponentAffinityMin)
                        Union(parent, i, j);

            var components = Collect(strokes, parent);

            // El sigilo es el grupo mas cercano al centro (si cae en la zona central); el resto son signos.
            int sigilIndex = -1;
            float best = SigilRadiusMax;
            for (int c = 0; c < components.Count; c++)
            {
                float radiusNorm = Vector2.Distance(GeometryUtils.GetSessionCenter(components[c]), ring.Center) / ring.Radius;
                if (radiusNorm <= best)
                {
                    best = radiusNorm;
                    sigilIndex = c;
                }
            }

            var groups = new Groups { Sigil = new DrawingSession(), Signs = new List<DrawingSession>() };
            for (int c = 0; c < components.Count; c++)
            {
                if (c == sigilIndex) groups.Sigil = components[c];
                else groups.Signs.Add(components[c]);
            }
            AbsorbIntoSigil(groups, ring);
            return groups;
        }

        /// <summary>Holgura (fraccion del radio) de la huella del sigilo al absorber marcas sueltas.</summary>
        public const float SigilFootprintPadNorm = 0.08f;

        /// <summary>
        /// Sigilos grandes tienen marcas sueltas (puntos, rayas) que caen fuera de la zona central. Toda marca cuyo centro
        /// cae dentro de la huella del sigilo es parte de el. Se repite porque la huella crece al absorber.
        /// </summary>
        private static void AbsorbIntoSigil(Groups groups, RingInfo ring)
        {
            if (groups.Sigil.IsEmpty) return;
            float pad = ring.Radius * SigilFootprintPadNorm;
            bool absorbed = true;
            while (absorbed)
            {
                absorbed = false;
                groups.Sigil.GetBounds(out Vector2 min, out Vector2 max);
                min -= Vector2.one * pad;
                max += Vector2.one * pad;
                for (int i = groups.Signs.Count - 1; i >= 0; i--)
                {
                    Vector2 c = GeometryUtils.GetSessionCenter(groups.Signs[i]);
                    if (c.x < min.x || c.x > max.x || c.y < min.y || c.y > max.y) continue;
                    foreach (var s in groups.Signs[i].Strokes) groups.Sigil.AddStroke(s);
                    groups.Signs.RemoveAt(i);
                    absorbed = true;
                }
            }
        }

        /// <summary>Afinidad en [0,1] entre dos trazos bajo un anillo (strokeAffinity de la referencia).</summary>
        public static float Affinity(Stroke a, (Vector2 min, Vector2 max) ba, Stroke b, (Vector2 min, Vector2 max) bb, RingInfo ring)
        {
            float reach = ring.Radius * AffinityReachNorm;
            float affinity = 0f;
            if (BoundsGap(ba, bb) <= reach)
            {
                float t = Mathf.Clamp01(1f - PolylineDistance(a, b) / reach);
                affinity = t * t;
            }

            // Tinta dibujada dentro del contorno de otro trazo pertenece a el.
            float extentA = Mathf.Max(ba.max.x - ba.min.x, ba.max.y - ba.min.y);
            float extentB = Mathf.Max(bb.max.x - bb.min.x, bb.max.y - bb.min.y);
            var (small, large) = extentA <= extentB ? (ba, bb) : (bb, ba);
            float pad = ring.Radius * EnclosurePadNorm;
            if (small.min.x >= large.min.x - pad && small.max.x <= large.max.x + pad &&
                small.min.y >= large.min.y - pad && small.max.y <= large.max.y + pad)
                affinity = Mathf.Max(affinity, 0.85f);

            // Tinta en la zona central pertenece al unico sigilo que vive ahi.
            Vector2 ca = (ba.min + ba.max) * 0.5f, cb = (bb.min + bb.max) * 0.5f;
            float centerReach = RingLayers.CenterMax + RingLayers.BoundaryTolerance * 1.5f;
            if (Vector2.Distance(ca, ring.Center) / ring.Radius <= centerReach &&
                Vector2.Distance(cb, ring.Center) / ring.Radius <= centerReach &&
                Vector2.Distance(ca, cb) <= ring.Radius * CenterAffinityDistanceNorm)
                affinity = Mathf.Max(affinity, 0.5f);

            return affinity;
        }

        private static float BoundsGap((Vector2 min, Vector2 max) a, (Vector2 min, Vector2 max) b)
        {
            float dx = Mathf.Max(0f, Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x));
            float dy = Mathf.Max(0f, Mathf.Max(a.min.y - b.max.y, b.min.y - a.max.y));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Distancia minima entre dos polilineas (punto contra segmento en ambos sentidos).</summary>
        public static float PolylineDistance(Stroke a, Stroke b)
        {
            float best = float.MaxValue;
            PointsToSegments(a, b, ref best);
            if (best > 0f) PointsToSegments(b, a, ref best);
            return best;
        }

        private static void PointsToSegments(Stroke points, Stroke segments, ref float best)
        {
            var seg = segments.Points;
            foreach (var p in points.Points)
            {
                if (seg.Count == 1)
                {
                    best = Mathf.Min(best, Vector2.Distance(p, seg[0]));
                    continue;
                }
                for (int i = 1; i < seg.Count; i++)
                {
                    Vector2 a = seg[i - 1], ab = seg[i] - a;
                    float lengthSq = ab.sqrMagnitude;
                    float t = lengthSq > 1e-12f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq) : 0f;
                    best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
                    if (best <= 0f) return;
                }
            }
        }

        private static Groups GroupRadially(List<Stroke> strokes, RingInfo ring)
        {
            var groups = new Groups { Sigil = new DrawingSession(), Signs = new List<DrawingSession>() };
            var outer = new List<Stroke>();
            foreach (var stroke in strokes)
            {
                float radiusNorm = Vector2.Distance(CenterOfMass(stroke), ring.Center) / ring.Radius;
                if (radiusNorm <= RadialSigilRadiusMax) groups.Sigil.AddStroke(stroke);
                else outer.Add(stroke);
            }

            float gap = ring.Radius * ClusterGapRatio;
            var parent = NewParents(outer.Count);
            var mins = new Vector2[outer.Count];
            var maxs = new Vector2[outer.Count];
            for (int i = 0; i < outer.Count; i++) GeometryUtils.GetBounds(new[] { outer[i] }, out mins[i], out maxs[i]);
            for (int i = 0; i < outer.Count; i++)
                for (int j = i + 1; j < outer.Count; j++)
                {
                    bool overlapX = mins[i].x <= maxs[j].x + gap && maxs[i].x >= mins[j].x - gap;
                    bool overlapY = mins[i].y <= maxs[j].y + gap && maxs[i].y >= mins[j].y - gap;
                    if (overlapX && overlapY) Union(parent, i, j);
                }
            groups.Signs = Collect(outer, parent);
            return groups;
        }

        private static int[] NewParents(int n)
        {
            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;
            return parent;
        }

        private static void Union(int[] parent, int a, int b) => parent[Find(parent, a)] = Find(parent, b);

        private static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        /// <summary>Agrupa por raiz preservando el orden de aparicion.</summary>
        private static List<DrawingSession> Collect(List<Stroke> strokes, int[] parent)
        {
            var byRoot = new Dictionary<int, DrawingSession>();
            var result = new List<DrawingSession>();
            for (int i = 0; i < strokes.Count; i++)
            {
                int root = Find(parent, i);
                if (!byRoot.TryGetValue(root, out var cluster))
                {
                    cluster = new DrawingSession();
                    byRoot[root] = cluster;
                    result.Add(cluster);
                }
                cluster.AddStroke(strokes[i]);
            }
            return result;
        }

        private static Vector2 CenterOfMass(Stroke stroke)
        {
            Vector2 sum = Vector2.zero;
            foreach (var p in stroke.Points) sum += p;
            return sum / stroke.Points.Count;
        }
    }
}
