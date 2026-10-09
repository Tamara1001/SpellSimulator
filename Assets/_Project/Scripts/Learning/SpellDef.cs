using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SpellSimulator.Learning
{
    /// <summary>Un signo de un hechizo: glifo, angulo en el anillo (grados, Y arriba) y giro extra sobre la pose canonica.</summary>
    public readonly struct SpellSign
    {
        public readonly string Id;
        public readonly float AngleDeg;
        /// <summary>0 = mira hacia el centro (pose canonica); 180 = hacia afuera; 90 = tangente (molinete).</summary>
        public readonly float TurnDeg;

        public SpellSign(string id, float angleDeg, float turnDeg)
        {
            Id = id;
            AngleDeg = angleDeg;
            TurnDeg = turnDeg;
        }
    }

    /// <summary>
    /// Un hechizo prearmado o de libro (Data/Spells/*.json, ADR-034). Describe el sello completo normalizado al anillo:
    /// sigilo al centro + signos sobre el anillo + circulo casi cerrado. Si trae "strokes" explicitos (trazos en unidades
    /// de anillo, radio 1) se usan tal cual; si no, <see cref="SpellStrokeBuilder"/> los arma con las plantillas del diccionario.
    /// Logica pura (sin MonoBehaviour).
    /// </summary>
    public class SpellDef
    {
        public const int SchemaVersion = 1;
        /// <summary>Valor de "book" para los hechizos desbloqueados desde el principio.</summary>
        public const string InitialBook = "inicial";

        public string Id;
        public string Name;
        public string Description;
        public string HowTo;
        /// <summary>Pista para encontrarlo sin conocerlo («Se dice que...»). El grimorio la muestra junto a la silueta.</summary>
        public string Hint;
        /// <summary>fire | water | wind | earth | light.</summary>
        public string Element;
        /// <summary>Preset | Book | Player.</summary>
        public string Origin;
        public int Difficulty = 1;
        /// <summary>Id del libro que lo ensena, o "inicial".</summary>
        public string Book = InitialBook;
        public float GapDeg = 34f;
        public string Sigil;
        public readonly List<SpellSign> Signs = new List<SpellSign>();
        /// <summary>Trazos explicitos opcionales (unidades de anillo). Null: se arman desde sigilo y signos.</summary>
        public List<List<Vector2>> Strokes;

        public bool IsInitial => string.IsNullOrEmpty(Book) || Book == InitialBook;

        public string ElementName => ElementLabel(Element);

        public static string ElementLabel(string element)
        {
            switch (element)
            {
                case "fire": return "Fuego";
                case "water": return "Agua";
                case "wind": return "Viento";
                case "earth": return "Tierra";
                case "light": return "Luz";
                default: return string.IsNullOrEmpty(element) ? "-" : element;
            }
        }

        /// <summary>Glifos que usa (sigilo y signos, sin repetidos).</summary>
        public IEnumerable<string> GlyphIds()
        {
            var seen = new HashSet<string>();
            if (!string.IsNullOrEmpty(Sigil) && seen.Add(Sigil)) yield return Sigil;
            for (int i = 0; i < Signs.Count; i++)
                if (seen.Add(Signs[i].Id)) yield return Signs[i].Id;
        }

        /// <summary>Lee un hechizo. Lanza si el JSON es invalido o falta id/nombre.</summary>
        public static SpellDef FromJson(string json)
        {
            var root = JObject.Parse(json);
            var def = new SpellDef
            {
                Id = (string)root["id"],
                Name = (string)root["name"],
                Description = (string)root["description"] ?? "",
                HowTo = (string)root["howTo"] ?? "",
                Hint = (string)root["hint"] ?? "",
                Element = (string)root["element"],
                Origin = (string)root["origin"] ?? "Preset",
                Difficulty = (int?)root["difficulty"] ?? 1,
                Book = (string)root["book"] ?? InitialBook,
                Sigil = (string)root["sigil"],
            };
            if (string.IsNullOrEmpty(def.Id) || string.IsNullOrEmpty(def.Name))
                throw new FormatException("El hechizo necesita 'id' y 'name'.");
            if (root["ring"] is JObject ring && ring["gapDeg"] != null) def.GapDeg = (float)ring["gapDeg"];
            if (root["signs"] is JArray signs)
                foreach (var s in signs)
                    def.Signs.Add(new SpellSign((string)s["id"], (float?)s["angle"] ?? 0f, (float?)s["turn"] ?? 0f));
            if (root["strokes"] is JArray strokes)
            {
                def.Strokes = new List<List<Vector2>>(strokes.Count);
                foreach (var st in strokes)
                {
                    var pts = new List<Vector2>();
                    foreach (var p in st) pts.Add(new Vector2((float)p[0], (float)p[1]));
                    def.Strokes.Add(pts);
                }
            }
            return def;
        }
    }
}
