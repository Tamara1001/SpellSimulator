using System.Collections.Generic;
using SpellSimulator.Glyphs;
using SpellSimulator.Parsing;

namespace SpellSimulator.Compiler
{
    /// <summary>Claves de advertencia (parser/glyphWarnings.ts de la referencia) con texto para la UI.</summary>
    public static class GlyphWarnings
    {
        public const string NoRingDetected = "no_ring_detected";
        public const string RingIncomplete = "ring_incomplete";
        public const string UnsupportedMultipleSigils = "unsupported_multiple_sigils";
        public const string MissingPrimarySigil = "missing_primary_sigil";
        public const string PrimarySigilConfidenceLow = "primary_sigil_confidence_low";
        public const string PrimarySigilAmbiguous = "primary_sigil_ambiguous";
        public const string PrimaryElementMissing = "primary_element_missing";
        public const string PrimaryElementUnsupported = "primary_element_unsupported";
        public const string CenterUnknownContamination = "center_unknown_contamination";
        public const string SymbolNearLayerBoundary = "symbol_near_layer_boundary";
        public const string SymbolAmbiguous = "symbol_ambiguous";
        public const string SymbolMessy = "symbol_messy";
        public const string SymbolWrongLayer = "symbol_wrong_layer";

        public static string Describe(string key)
        {
            switch (key)
            {
                case NoRingDetected: return "No se detecto el anillo.";
                case RingIncomplete: return "Anillo abierto: el hechizo queda preparado.";
                case UnsupportedMultipleSigils: return "Hay mas de un sigilo; solo se admite uno.";
                case MissingPrimarySigil: return "Falta el sigilo central.";
                case PrimarySigilConfidenceLow: return "El sigilo no se reconoce con suficiente confianza.";
                case PrimarySigilAmbiguous: return "El sigilo es ambiguo entre dos elementos.";
                case PrimaryElementMissing: return "El sigilo no define un elemento.";
                case PrimaryElementUnsupported: return "Elemento no soportado.";
                case CenterUnknownContamination: return "Hay tinta desconocida en el centro.";
                case SymbolNearLayerBoundary: return "Un simbolo esta en el borde entre capas.";
                case SymbolAmbiguous: return "Un simbolo es ambiguo.";
                case SymbolMessy: return "Un simbolo esta desprolijo.";
                case SymbolWrongLayer: return "Un simbolo esta en una capa donde no tiene efecto.";
                default: return key;
            }
        }
    }

    /// <summary>Un candidato ya reconocido (equivale a Recognition de la referencia).</summary>
    public class GlyphRecognition
    {
        public SymbolCandidate Candidate;
        public GlyphEntry Entry;
        public float Confidence;
        /// <summary>Mejor confianza de otra clase del mismo rol (sigilo vs sigilo). Mide ambiguedad.</summary>
        public float SameKindAlternateConfidence;
        /// <summary>Mejor confianza de cualquier otra clase.</summary>
        public float CompetitorConfidence;
        public bool Recognized;
        /// <summary>Rotacion respecto de la plantilla (grados antihorarios), de la pose del modelo. NaN si no hay.</summary>
        public float RotationDeg = float.NaN;
        /// <summary>Motivo si no fue reconocido: low_confidence, unknown_class, wrong_layer.</summary>
        public string RejectReason;

        public string Id => Entry.Id;
        public GlyphKind Kind => Entry.Kind;
        public bool IsAmbiguous => Confidence - CompetitorConfidence < GlyphAstBuilder.AmbiguityMargin;
    }

    /// <summary>Metricas globales del sello (calculateGlobalMetrics de la referencia).</summary>
    public struct GlobalMetrics
    {
        public float Neatness;
        public float RadialSymmetry;
        public float Instability;
    }

    /// <summary>
    /// Arbol del sello: anillo, sigilo primario, signos, desconocidos y advertencias.
    /// Entrada del compilador.
    /// </summary>
    public class GlyphAST
    {
        public RingInfo Ring;
        public GlyphRecognition PrimarySigil;
        public readonly List<GlyphRecognition> ExtraSigils = new List<GlyphRecognition>();
        public readonly List<GlyphRecognition> Signs = new List<GlyphRecognition>();
        public readonly List<GlyphRecognition> Unknowns = new List<GlyphRecognition>();
        public readonly List<string> Warnings = new List<string>();
        public GlobalMetrics Metrics;

        public void Warn(string key)
        {
            if (!Warnings.Contains(key)) Warnings.Add(key);
        }
    }
}
