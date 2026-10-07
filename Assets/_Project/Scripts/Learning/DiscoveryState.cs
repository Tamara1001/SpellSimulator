using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SpellSimulator.Learning
{
    /// <summary>
    /// Progreso del jugador (SYSTEMS_DESIGN 7): glifos descubiertos y libros leidos. Logica pura + JSON; el guardado en
    /// disco lo hace Discovery. Descubrir no bloquea nada: un glifo sin descubrir igual funciona si se dibuja bien.
    /// </summary>
    public class DiscoveryState
    {
        private readonly HashSet<string> glyphs = new HashSet<string>();
        private readonly HashSet<string> books = new HashSet<string>();

        public int GlyphCount => glyphs.Count;
        public bool Knows(string glyph) => glyphs.Contains(glyph);
        public bool HasRead(string book) => books.Contains(book);
        public IEnumerable<string> Glyphs => glyphs;

        /// <summary>Devuelve true si era nuevo.</summary>
        public bool Discover(string glyph) => !string.IsNullOrEmpty(glyph) && glyphs.Add(glyph);

        public bool MarkRead(string book) => !string.IsNullOrEmpty(book) && books.Add(book);

        public static DiscoveryState Starting(IEnumerable<string> startingGlyphs)
        {
            var state = new DiscoveryState();
            foreach (var g in startingGlyphs) state.Discover(g);
            return state;
        }

        public string ToJson()
        {
            var root = new JObject
            {
                ["version"] = 1,
                ["glyphs"] = new JArray(glyphs.OrderBy(g => g)),
                ["books"] = new JArray(books.OrderBy(b => b))
            };
            return root.ToString();
        }

        public static DiscoveryState FromJson(string json)
        {
            var state = new DiscoveryState();
            var root = JObject.Parse(json);
            if (root["glyphs"] is JArray g) foreach (var id in g) state.Discover((string)id);
            if (root["books"] is JArray b) foreach (var id in b) state.MarkRead((string)id);
            return state;
        }
    }
}
