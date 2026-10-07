using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SpellSimulator.Glyphs
{
    /// <summary>
    /// Diccionario de glifos: indice de clase del modelo -> entrada con rol, elemento y semantica.
    /// Se construye a partir de los JSON copiados de wha-spell-simulator (src/lib/dictionary).
    /// Las clases del modelo que no tienen entrada quedan como GlyphKind.Unknown.
    /// </summary>
    public class GlyphDictionary
    {
        private readonly Dictionary<string, GlyphEntry> byId = new Dictionary<string, GlyphEntry>();
        private readonly string[] idByIndex;

        public int ClassCount => idByIndex.Length;
        public IEnumerable<GlyphEntry> Entries => byId.Values;

        private GlyphDictionary(string[] idByIndex)
        {
            this.idByIndex = idByIndex;
        }

        public GlyphEntry Get(int classIndex)
        {
            if (classIndex < 0 || classIndex >= idByIndex.Length) return GlyphEntry.Unknown($"class:{classIndex}");
            return Get(idByIndex[classIndex]);
        }

        public GlyphEntry Get(string id)
        {
            return byId.TryGetValue(id, out var entry) ? entry : GlyphEntry.Unknown(id);
        }

        public int IndexOf(string id) => Array.IndexOf(idByIndex, id);

        /// <param name="classMapJson">glyph-class-to-idx.json: { "id": indice }.</param>
        /// <param name="entryJsons">Un JSON por glifo, con el rol que le da su carpeta.</param>
        public static GlyphDictionary FromJson(string classMapJson, IEnumerable<(string json, GlyphKind kind)> entryJsons)
        {
            var map = JObject.Parse(classMapJson);
            var ids = new string[map.Count];
            foreach (var pair in map)
            {
                int index = pair.Value.Value<int>();
                if (index < 0 || index >= ids.Length) throw new FormatException($"Indice fuera de rango en class map: {pair.Key}={index}");
                ids[index] = pair.Key;
            }

            var dictionary = new GlyphDictionary(ids);
            foreach (var (json, kind) in entryJsons)
            {
                var entry = ParseEntry(json, kind);
                dictionary.byId[entry.Id] = entry;
            }
            return dictionary;
        }

        private static GlyphEntry ParseEntry(string json, GlyphKind kind)
        {
            var o = JObject.Parse(json);
            var entry = new GlyphEntry
            {
                Id = (string)o["id"],
                DisplayName = (string)o["displayName"] ?? (string)o["id"],
                Kind = kind,
                Element = (string)o["element"],
                ReferenceSizeNorm = o["referenceSizeNorm"]?.Value<float>() ?? 0f,
                AllowedLayers = o["allowedLayers"]?.ToObject<string[]>() ?? Array.Empty<string>(),
                Semantic = o["semantic"]?.ToObject<GlyphSemantic>() ?? new GlyphSemantic()
            };

            // Plantillas en coordenadas de canvas (Y hacia abajo): se invierte Y para el espacio del lienzo.
            if (o["strokeTemplate"]?["strokes"] is JArray strokes)
            {
                foreach (var stroke in strokes)
                {
                    var points = new List<Vector2>();
                    foreach (var p in stroke)
                        points.Add(new Vector2(p.Value<float>("x"), 1f - p.Value<float>("y")));
                    entry.TemplateStrokes.Add(points);
                }
            }
            return entry;
        }
    }
}
