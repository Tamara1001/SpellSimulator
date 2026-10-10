using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SpellSimulator.Learning
{
    /// <summary>
    /// Hechizos propios del jugador (ADR-034, H1/H5). Guarda los TRAZOS (en unidades de anillo, ver <see cref="SpellRecording"/>) y no el SpellIR: el modelo y el
    /// diccionario pueden cambiar. Cada entrada es un <see cref="SpellDef"/> de origen "Player" con trazos explicitos, asi el grimorio y el sello los tratan
    /// igual que a los prearmados. Logica pura (el archivo lo maneja <see cref="SpellLibraryStore"/>).
    /// </summary>
    public sealed class SpellLibrary
    {
        public const int SchemaVersion = 1, MaxSpells = 30, MaxNameLength = 24;
        public const string PlayerOrigin = "Player";

        private readonly List<SpellDef> items = new List<SpellDef>();

        public IReadOnlyList<SpellDef> Items => items;
        public int Count => items.Count;
        public bool CanAdd => items.Count < MaxSpells;

        /// <summary>Nombre por defecto: «Fuego propio», «Fuego propio 2»... (el primero libre).</summary>
        public static string DefaultName(string element, IEnumerable<SpellDef> existing)
        {
            string baseName = SpellDef.ElementLabel(element) + " propio";
            var names = new HashSet<string>(existing.Select(s => s.Name));
            if (!names.Contains(baseName)) return baseName;
            for (int n = 2; n < 1000; n++) if (!names.Contains(baseName + " " + n)) return baseName + " " + n;
            return baseName;
        }

        /// <summary>Quita caracteres de control, recorta espacios y largo; null si queda vacio.</summary>
        public static string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var chars = name.Where(c => !char.IsControl(c) && c != '<' && c != '>').ToArray();
            string clean = new string(chars).Trim();
            if (clean.Length > MaxNameLength) clean = clean.Substring(0, MaxNameLength).TrimEnd();
            return clean.Length == 0 ? null : clean;
        }

        /// <summary>Agrega un hechizo propio. Null si no hay lugar o no hay trazos.</summary>
        public SpellDef Add(string name, string element, List<List<Vector2>> strokes, float gapDeg)
        {
            if (!CanAdd || strokes == null || strokes.Count == 0) return null;
            string clean = CleanName(name) ?? DefaultName(element, items);
            var def = new SpellDef
            {
                Id = "player-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                Name = clean,
                Description = "Un hechizo tuyo, guardado de lo que dibujaste.",
                HowTo = "Cargalo en un sello desde esta página y cerrá el anillo a mano.",
                Hint = "",
                Element = element,
                Origin = PlayerOrigin,
                Difficulty = 1,
                Book = SpellDef.InitialBook,
                GapDeg = gapDeg,
                Strokes = strokes,
            };
            items.Add(def);
            return def;
        }

        public bool Remove(string id)
        {
            int i = items.FindIndex(s => s.Id == id);
            if (i < 0) return false;
            items.RemoveAt(i);
            return true;
        }

        public bool Rename(string id, string name)
        {
            string clean = CleanName(name);
            SpellDef def = items.Find(s => s.Id == id);
            if (def == null || clean == null) return false;
            def.Name = clean;
            return true;
        }

        public string ToJson()
        {
            var root = new JObject
            {
                ["schemaVersion"] = SchemaVersion,
                ["spells"] = new JArray(items.Select(s => JObject.Parse(s.ToJson()))),
            };
            return root.ToString();
        }

        /// <summary>Lee la biblioteca. Una entrada invalida se saltea (y se avisa); un JSON roto lanza.</summary>
        public static SpellLibrary FromJson(string json, Action<string> onError = null)
        {
            var lib = new SpellLibrary();
            var root = JObject.Parse(json);
            if (!(root["spells"] is JArray list)) return lib;
            foreach (JToken token in list)
            {
                if (lib.items.Count >= MaxSpells) break;
                try
                {
                    SpellDef def = SpellDef.FromJson(token.ToString());
                    def.Origin = PlayerOrigin;
                    def.Book = SpellDef.InitialBook;
                    if (def.Strokes == null || def.Strokes.Count == 0 || lib.items.Any(s => s.Id == def.Id)) continue;
                    def.Name = CleanName(def.Name) ?? DefaultName(def.Element, lib.items);
                    lib.items.Add(def);
                }
                catch (Exception e) { onError?.Invoke("Hechizo propio invalido: " + e.Message); }
            }
            return lib;
        }
    }
}
