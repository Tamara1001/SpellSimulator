using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using SpellSimulator.Core;

namespace SpellSimulator.Casting
{
    /// <summary>
    /// Grabacion de un sello lanzado: trazos crudos en UV + escala isotropica del lienzo + resumen del resultado.
    /// Sirve para reproducir pruebas manuales como fixtures (Tests/EditMode/Fixtures). Formato JSON simple:
    /// { "iso": [x, y], "strokes": [[[u, v], ...], ...], "summary": "...", "expect": { "sigil": "...", "primitives": "..." } }
    /// "expect" es opcional y se agrega a mano al convertir una grabacion en fixture.
    /// </summary>
    public class SealRecord
    {
        public Vector2 Iso = Vector2.one;
        public DrawingSession Session = new DrawingSession();
        public string Summary = "";
        public string ExpectSigil;
        public string ExpectPrimitives;

        /// <summary>Sesion en el espacio isotropico que usa el pipeline (como SpellCaster.Clean antes de limpiar).</summary>
        public DrawingSession IsotropicSession() => Session.Scaled(Iso);

        public string ToJson()
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("{\n  \"iso\": [").Append(Iso.x.ToString("R", ci)).Append(", ").Append(Iso.y.ToString("R", ci)).Append("],\n");
            sb.Append("  \"summary\": ").Append(JValue.CreateString(Summary ?? "").ToString(Newtonsoft.Json.Formatting.None)).Append(",\n");
            sb.Append("  \"strokes\": [\n");
            for (int i = 0; i < Session.Strokes.Count; i++)
            {
                sb.Append("    [");
                var pts = Session.Strokes[i].Points;
                for (int j = 0; j < pts.Count; j++)
                {
                    if (j > 0) sb.Append(", ");
                    sb.Append('[').Append(pts[j].x.ToString("0.#####", ci)).Append(", ").Append(pts[j].y.ToString("0.#####", ci)).Append(']');
                }
                sb.Append(i < Session.Strokes.Count - 1 ? "],\n" : "]\n");
            }
            sb.Append("  ]\n}\n");
            return sb.ToString();
        }

        public static SealRecord FromJson(string json)
        {
            var root = JObject.Parse(json);
            var record = new SealRecord();
            if (root["iso"] is JArray iso && iso.Count == 2) record.Iso = new Vector2((float)iso[0], (float)iso[1]);
            record.Summary = (string)root["summary"] ?? "";
            if (root["strokes"] is JArray strokes)
            {
                foreach (var s in strokes)
                {
                    var points = new List<Vector2>();
                    foreach (var p in s) points.Add(new Vector2((float)p[0], (float)p[1]));
                    record.Session.AddStroke(new Stroke(points));
                }
            }
            if (root["expect"] is JObject expect)
            {
                record.ExpectSigil = (string)expect["sigil"];
                record.ExpectPrimitives = (string)expect["primitives"];
            }
            return record;
        }

        /// <summary>Carpeta donde la build/editor guarda las grabaciones.</summary>
        public static string Folder => Path.Combine(Application.persistentDataPath, "Sellos");

        /// <summary>Guarda la grabacion con fecha en el nombre. Nunca lanza: una falla de disco no debe cortar el cast.</summary>
        public static string Save(SealRecord record)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                string path = Path.Combine(Folder, $"sello_{DateTime.Now:yyyyMMdd_HHmmss_fff}.json");
                File.WriteAllText(path, record.ToJson());
                return path;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Cast] No se pudo guardar la grabacion del sello: {e.Message}");
                return null;
            }
        }
    }
}
