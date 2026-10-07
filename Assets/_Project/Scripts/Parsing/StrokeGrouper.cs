using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Core;

namespace SpellSimulator.Parsing
{
    /// <summary>
    /// Agrupa los trazos que no son anillo en candidatos a glifo.
    /// Los trazos cuyo centro de masa cae en la zona central forman un unico candidato a sigilo
    /// (los sigilos suelen tener partes sueltas). El resto se agrupa por proximidad transitiva.
    /// La referencia hace busqueda de particiones guiada por reconocimiento; esto es la version barata.
    /// </summary>
    public static class StrokeGrouper
    {
        /// <summary>Radio normalizado hasta el cual un trazo se considera parte del sigilo central.</summary>
        public const float SigilRadiusMax = 0.40f;
        /// <summary>Separacion maxima entre bounding boxes (fraccion del radio) para fusionar trazos de un signo.</summary>
        public const float ClusterGapRatio = 0.12f;

        public struct Groups
        {
            public DrawingSession Sigil;
            public List<DrawingSession> Signs;
        }

        public static Groups Group(DrawingSession session, RingInfo ring)
        {
            var groups = new Groups { Sigil = new DrawingSession(), Signs = new List<DrawingSession>() };

            if (!ring.Found)
            {
                foreach (var s in session.Strokes) groups.Sigil.AddStroke(s);
                return groups;
            }

            var outer = new List<Stroke>();
            foreach (var stroke in session.Strokes)
            {
                if (ring.Contains(stroke) || stroke.Points.Count == 0) continue;

                float radiusNorm = Vector2.Distance(CenterOfMass(stroke), ring.Center) / ring.Radius;
                if (radiusNorm <= SigilRadiusMax) groups.Sigil.AddStroke(stroke);
                else outer.Add(stroke);
            }

            groups.Signs = Cluster(outer, ring.Radius * ClusterGapRatio);
            return groups;
        }

        /// <summary>Union-find sobre bounding boxes expandidos: si A toca B y B toca C, los tres son un signo.</summary>
        private static List<DrawingSession> Cluster(List<Stroke> strokes, float gap)
        {
            int n = strokes.Count;
            var parent = new int[n];
            var mins = new Vector2[n];
            var maxs = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                parent[i] = i;
                GeometryUtils.GetBounds(new[] { strokes[i] }, out mins[i], out maxs[i]);
            }

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    bool overlapX = mins[i].x <= maxs[j].x + gap && maxs[i].x >= mins[j].x - gap;
                    bool overlapY = mins[i].y <= maxs[j].y + gap && maxs[i].y >= mins[j].y - gap;
                    if (overlapX && overlapY) parent[Find(parent, i)] = Find(parent, j);
                }
            }

            var byRoot = new Dictionary<int, DrawingSession>();
            var result = new List<DrawingSession>();
            for (int i = 0; i < n; i++)
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

        private static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        private static Vector2 CenterOfMass(Stroke stroke)
        {
            Vector2 sum = Vector2.zero;
            foreach (var p in stroke.Points) sum += p;
            return sum / stroke.Points.Count;
        }
    }
}
