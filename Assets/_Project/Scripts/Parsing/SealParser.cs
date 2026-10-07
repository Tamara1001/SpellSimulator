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
