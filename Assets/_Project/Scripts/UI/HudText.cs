using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SpellSimulator.UI
{
    /// <summary>
    /// Textos de la HUD sin dependencias de escena: parte las pistas de teclas ("Clic: dibujar | R: tinta") en pares
    /// tecla/accion para dibujarlas como keycaps, arma las pistas de lo que se arrastra segun sus capacidades y limpia los
    /// mensajes de diagnostico que pasan a ser avisos. Todo se llama al cambiar el inventario, nunca por frame.
    /// </summary>
    public static class HudText
    {
        /// <summary>Una pista: tecla (va en el keycap) y lo que hace.</summary>
        public readonly struct Hint
        {
            public readonly string Key;
            public readonly string Action;
            public Hint(string key, string action) { Key = key; Action = action; }
        }

        public const int MaxDiagnosticLength = 200;
        public const string SpellReportTag = "<b>[Hechizo]";

        private static readonly Regex RichTextTag = new Regex("<[^>]+>", RegexOptions.Compiled);
        private static readonly Dictionary<int, string> secondaryCache = new Dictionary<int, string>();

        /// <summary>
        /// Parte "Tecla: accion | Tecla: accion". La seleccion de grosor por numeros ("5-0") se muestra como rueda, que es
        /// como se cambia ahora. Las partes sin ":" quedan como accion sin tecla.
        /// </summary>
        public static List<Hint> ParseHints(string hints, List<Hint> into = null)
        {
            into ??= new List<Hint>();
            into.Clear();
            if (string.IsNullOrWhiteSpace(hints)) return into;
            foreach (string raw in hints.Split('|'))
            {
                string part = raw.Trim();
                if (part.Length == 0) continue;
                int colon = part.IndexOf(':');
                if (colon <= 0) { into.Add(new Hint("", Capitalize(part))); continue; }
                string key = part.Substring(0, colon).Trim();
                string action = Capitalize(part.Substring(colon + 1).Trim());
                if (key == "5-0" || key == "1-0") key = "Rueda";
                into.Add(new Hint(key, action));
            }
            return into;
        }

        /// <summary>
        /// Con algo en la mano secundaria, Q arroja eso y la rueda lo acerca o aleja: se quitan esas pistas de la mano principal
        /// para no repetirlas ni contradecirse ("Q: arrojar" y "Rueda: grosor" solo valen sin nada agarrado).
        /// </summary>
        public static string DropHeldConflicts(string hints)
        {
            if (string.IsNullOrWhiteSpace(hints)) return hints;
            var kept = new List<string>();
            foreach (string raw in hints.Split('|'))
            {
                string part = raw.Trim();
                int colon = part.IndexOf(':');
                string key = colon > 0 ? part.Substring(0, colon).Trim() : "";
                if (key == "Q" || key == "Rueda" || key == "5-0" || key == "1-0") continue;
                if (part.Length > 0) kept.Add(part);
            }
            return string.Join(" | ", kept);
        }

        /// <summary>Primera letra en mayuscula ("dibujar" -> "Dibujar"). Solo se llama al armar la HUD, nunca por frame.</summary>
        public static string Capitalize(string text)
        {
            if (string.IsNullOrEmpty(text) || !char.IsLower(text[0])) return text;
            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        /// <summary>
        /// Pistas de lo que se arrastra en la mano secundaria, solo con las teclas que aplican al objeto. Cacheadas por
        /// combinacion (no se arman strings nuevos cada vez).
        /// </summary>
        public static string SecondaryHints(bool pageable, bool flippable, bool focusable, bool pinnable)
        {
            int mask = (pageable ? 1 : 0) | (flippable ? 2 : 0) | (focusable ? 4 : 0) | (pinnable ? 8 : 0);
            if (secondaryCache.TryGetValue(mask, out string cached)) return cached;
            var parts = new List<string>(8);
            if (pageable) parts.Add("Z/X: página");
            parts.Add("Rueda: distancia");
            parts.Add("Clic der.+mouse: girar");
            if (flippable) parts.Add("F: dar vuelta");
            if (focusable) parts.Add("Tab: a pantalla");
            parts.Add("E: dejar flotando");
            parts.Add("Q: arrojar");
            string text = string.Join(" | ", parts);
            secondaryCache[mask] = text;
            return text;
        }

        /// <summary>
        /// Mensaje de DiagnosticBus convertido en aviso: sin etiquetas de texto enriquecido y en una sola linea. Devuelve
        /// null si no es para el jugador (el informe del hechizo, mensajes largos o vacios).
        /// </summary>
        public static string CleanDiagnostic(string log)
        {
            if (string.IsNullOrEmpty(log)) return null;
            if (log.StartsWith(SpellReportTag, StringComparison.Ordinal) || log.Length >= MaxDiagnosticLength) return null;
            string clean = RichTextTag.Replace(log, "").Replace("\r", "").Trim().Replace("\n", " · ");
            return clean.Length == 0 ? null : clean;
        }

        /// <summary>Nombre visible de un objeto sin IHudInfo: "Caja_Madera (1)" -> "Caja Madera".</summary>
        public static string PrettyName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Objeto";
            int paren = name.IndexOf(" (", StringComparison.Ordinal);
            if (paren > 0) name = name.Substring(0, paren);
            name = name.Replace('_', ' ').Trim();
            return name.Length == 0 ? "Objeto" : name;
        }
    }
}
