using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SpellSimulator.Learning
{
    /// <summary>Un libro de la biblioteca: que glifos ensena.</summary>
    public class BookInfo
    {
        public string Id;
        public string Title;
        public Color Color;
        public readonly List<string> Glyphs = new List<string>();
    }

    /// <summary>
    /// Textos del grimorio (Data/Lore/glifos.json): nombre y explicacion en español de cada glifo, glifos iniciales y
    /// libros de la biblioteca. Es data, no codigo (CLAUDE.md: el significado de los glifos es data).
    /// </summary>
    public class GlyphLore
    {
        private readonly Dictionary<string, (string name, string text)> glyphs = new Dictionary<string, (string, string)>();
        public readonly List<string> Starting = new List<string>();
        public readonly List<BookInfo> Books = new List<BookInfo>();

        public IEnumerable<string> Ids => glyphs.Keys;

        public static GlyphLore FromJson(string json)
        {
            var root = JObject.Parse(json);
            var lore = new GlyphLore();
            if (root["glyphs"] is JObject g)
                foreach (var p in g.Properties())
                    lore.glyphs[p.Name] = ((string)p.Value["name"] ?? p.Name, (string)p.Value["text"] ?? "");
            if (root["starting"] is JArray s) foreach (var id in s) lore.Starting.Add((string)id);
            if (root["books"] is JArray books)
                foreach (var b in books)
                {
                    var info = new BookInfo { Id = (string)b["id"], Title = (string)b["title"], Color = new Color(0.5f, 0.3f, 0.2f) };
                    if (b["color"] is JArray c && c.Count >= 3) info.Color = new Color((float)c[0], (float)c[1], (float)c[2]);
                    if (b["glyphs"] is JArray ids) foreach (var id in ids) info.Glyphs.Add((string)id);
                    lore.Books.Add(info);
                }
            return lore;
        }

        public bool Has(string id) => glyphs.ContainsKey(id);
        public string Name(string id) => glyphs.TryGetValue(id, out var g) ? g.name : id;
        public string Text(string id) => glyphs.TryGetValue(id, out var g) ? g.text : "";
        public BookInfo Book(string id) => Books.Find(b => b.Id == id);
    }
}
