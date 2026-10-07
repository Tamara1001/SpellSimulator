using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.Glyphs
{
    public enum GlyphKind { Unknown, Sigil, Sign }

    /// <summary>
    /// Bloque "semantic" de una entrada del diccionario (ver docs/dictionary-authoring.md de la referencia).
    /// Los deltas se suman a los parametros base del hechizo, ponderados por la influencia del signo.
    /// </summary>
    [Serializable]
    public class GlyphSemantic
    {
        public float force;
        public float focus;
        public float spread;
        public float range;
        public float lifetimeBias;
        /// <summary>Familia de manifestacion (column, pull, levitation...). Null en sigilos.</summary>
        public string manifestation;
        /// <summary>position | inward | orientation. Como se deriva la direccion del signo.</summary>
        public string directionMode;
    }

    /// <summary>Un glifo del diccionario: sigilo (define elemento) o signo (modifica el hechizo).</summary>
    public class GlyphEntry
    {
        public string Id;
        public string DisplayName;
        public GlyphKind Kind;
        /// <summary>fire | water | wind | earth | light. Solo sigilos.</summary>
        public string Element;
        /// <summary>Tamanio "regular" del glifo como fraccion del diametro del anillo.</summary>
        public float ReferenceSizeNorm;
        public string[] AllowedLayers = Array.Empty<string>();
        public GlyphSemantic Semantic = new GlyphSemantic();
        /// <summary>Trazos de ejemplo en [0,1], Y hacia arriba. Para tests, grimorio y sellos.</summary>
        public List<List<Vector2>> TemplateStrokes = new List<List<Vector2>>();

        public static GlyphEntry Unknown(string id) => new GlyphEntry { Id = id, DisplayName = id, Kind = GlyphKind.Unknown };
    }
}
