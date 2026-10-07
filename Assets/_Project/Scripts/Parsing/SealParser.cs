using System.Collections.Generic;
using SpellSimulator.Core;

namespace SpellSimulator.Parsing
{
    /// <summary>Geometria de un sello antes del reconocimiento: anillo + candidatos con metricas.</summary>
    public class ParsedSeal
    {
        public RingInfo Ring;
        /// <summary>Candidato central (sigilo). Null si no hay tinta en el centro.</summary>
        public SymbolCandidate SigilCandidate;
        public readonly List<SymbolCandidate> SignCandidates = new List<SymbolCandidate>();

        public IEnumerable<SymbolCandidate> AllCandidates()
        {
            if (SigilCandidate != null) yield return SigilCandidate;
            foreach (var c in SignCandidates) yield return c;
        }
    }

    /// <summary>
    /// Etapa de parseo: sesion limpia -> anillo + candidatos. Logica pura, sin red neuronal.
    /// </summary>
    public static class SealParser
    {
        /// <summary>
        /// Hipotesis alternativa: el sigilo absorbe todos los grupos de la capa media (sigilos grandes con partes sueltas,
        /// como viento o tierra). Comparte los candidatos de signo restantes con el sello original (mismas instancias).
        /// Null si no hay nada que fusionar.
        /// </summary>
        public static ParsedSeal MergeMiddleIntoSigil(ParsedSeal seal)
        {
            if (!seal.Ring.Found || seal.SigilCandidate == null) return null;

            var merged = new ParsedSeal { Ring = seal.Ring };
            var strokes = new DrawingSession();
            foreach (var s in seal.SigilCandidate.Strokes.Strokes) strokes.AddStroke(s);

            bool absorbed = false;
            foreach (var sign in seal.SignCandidates)
            {
                if (sign.RadiusNorm <= RingLayers.MiddleMax)
                {
                    foreach (var s in sign.Strokes.Strokes) strokes.AddStroke(s);
                    absorbed = true;
                }
                else merged.SignCandidates.Add(sign);
            }
            if (!absorbed) return null;

            merged.SigilCandidate = SymbolCandidate.Build(strokes, seal.Ring.Center, seal.Ring.Radius);
            return merged;
        }

        /// <summary>Dos trazos se tocan si pasan a menos de esto (en radios del anillo).</summary>
        public const float TouchDistance = 0.05f;
        /// <summary>Partes sueltas a menos de esto entre si forman un mismo signo (como el alcance de afinidad).</summary>
        public const float PeelClusterReach = 0.2f;

        /// <summary>
        /// Hipotesis inversa a la fusion: el sigilo se quedo con un signo cercano que no lo toca (prueba manual
        /// 2026-10-07: una columna pegada al fuego bajo la confianza del sigilo a 0.43). El nucleo es la parte conectada
        /// con mas tinta; las demas partes sueltas se agrupan entre si y pasan a ser signos. Null si el sigilo es una
        /// sola pieza.
        /// </summary>
        public static ParsedSeal PeelLooseFromSigil(ParsedSeal seal)
        {
            if (!seal.Ring.Found || seal.SigilCandidate == null) return null;
            var strokes = seal.SigilCandidate.Strokes.Strokes;
            if (strokes.Count < 2) return null;

            float touch = TouchDistance * seal.Ring.Radius;
            int[] part = Components(strokes, touch);

            // Nucleo = componente con mas tinta.
            var ink = new Dictionary<int, float>();
            for (int i = 0; i < strokes.Count; i++)
            {
                ink.TryGetValue(part[i], out float v);
                ink[part[i]] = v + GeometryUtils.StrokeLength(strokes[i]);
            }
            if (ink.Count < 2) return null;
            int core = -1;
            foreach (var kv in ink) if (core < 0 || kv.Value > ink[core]) core = kv.Key;

            var coreStrokes = new DrawingSession();
            var loose = new List<Stroke>();
            for (int i = 0; i < strokes.Count; i++)
                if (part[i] == core) coreStrokes.AddStroke(strokes[i]); else loose.Add(strokes[i]);

            var peeled = new ParsedSeal { Ring = seal.Ring };
            peeled.SigilCandidate = SymbolCandidate.Build(coreStrokes, seal.Ring.Center, seal.Ring.Radius);
            int[] cluster = Components(loose, PeelClusterReach * seal.Ring.Radius);
            var bySign = new Dictionary<int, DrawingSession>();
            for (int i = 0; i < loose.Count; i++)
            {
                if (!bySign.TryGetValue(cluster[i], out var session)) bySign[cluster[i]] = session = new DrawingSession();
                session.AddStroke(loose[i]);
            }
            foreach (var session in bySign.Values)
                peeled.SignCandidates.Add(SymbolCandidate.Build(session, seal.Ring.Center, seal.Ring.Radius));
            peeled.SignCandidates.AddRange(seal.SignCandidates);
            return peeled;
        }

        /// <summary>Componentes conexas (union-find) de trazos que pasan a menos de reach entre si.</summary>
        private static int[] Components(IReadOnlyList<Stroke> strokes, float reach)
        {
            int[] parent = new int[strokes.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }

            for (int i = 0; i < strokes.Count; i++)
                for (int j = i + 1; j < strokes.Count; j++)
                    if (StrokeDistance(strokes[i], strokes[j]) <= reach) parent[Find(i)] = Find(j);

            for (int i = 0; i < parent.Length; i++) parent[i] = Find(i);
            return parent;
        }

        /// <summary>Distancia minima entre dos polilineas (punto contra segmento, en ambos sentidos).</summary>
        public static float StrokeDistance(Stroke a, Stroke b)
        {
            float best = float.MaxValue;
            best = UnityEngine.Mathf.Min(best, PointsToPolyline(a.Points, b.Points));
            best = UnityEngine.Mathf.Min(best, PointsToPolyline(b.Points, a.Points));
            return best;
        }

        private static float PointsToPolyline(List<UnityEngine.Vector2> points, List<UnityEngine.Vector2> line)
        {
            float best = float.MaxValue;
            foreach (var p in points)
            {
                if (line.Count == 1) { best = UnityEngine.Mathf.Min(best, (p - line[0]).magnitude); continue; }
                for (int k = 0; k + 1 < line.Count; k++)
                {
                    UnityEngine.Vector2 a = line[k], ab = line[k + 1] - a;
                    float t = ab.sqrMagnitude > 1e-12f ? UnityEngine.Mathf.Clamp01(UnityEngine.Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                    best = UnityEngine.Mathf.Min(best, (p - (a + ab * t)).magnitude);
                }
            }
            return best;
        }

        public static ParsedSeal Parse(DrawingSession cleaned, GroupingMode mode = GroupingMode.Affinity)
        {
            var seal = new ParsedSeal { Ring = RingDetector.Detect(cleaned) };
            var groups = StrokeGrouper.Group(cleaned, seal.Ring, mode);

            if (!seal.Ring.Found)
            {
                // Sin anillo no hay marco de referencia: un candidato con todo, en coordenadas propias.
                if (!groups.Sigil.IsEmpty)
                {
                    groups.Sigil.GetBounds(out var min, out var max);
                    float pseudoRadius = UnityEngine.Mathf.Max(max.x - min.x, max.y - min.y);
                    seal.SigilCandidate = SymbolCandidate.Build(groups.Sigil, (min + max) * 0.5f, pseudoRadius);
                }
                return seal;
            }

            if (!groups.Sigil.IsEmpty)
                seal.SigilCandidate = SymbolCandidate.Build(groups.Sigil, seal.Ring.Center, seal.Ring.Radius);

            foreach (var sign in groups.Signs)
                seal.SignCandidates.Add(SymbolCandidate.Build(sign, seal.Ring.Center, seal.Ring.Radius));

            return seal;
        }
    }
}
