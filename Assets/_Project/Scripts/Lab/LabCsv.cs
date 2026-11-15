using System;
using System.Globalization;
using System.Text;

namespace SpellSimulator.Lab
{
    /// <summary>Una fila del registro CSV del laboratorio (ver <see cref="LabCsv.Header"/>). Columnas vacias: null o id &lt;= 0.</summary>
    public readonly struct LabLogRow
    {
        /// <summary>fecha_hora: momento del evento (hora local).</summary>
        public readonly DateTime Timestamp;
        /// <summary>t_sesion_s: segundos desde que empezo la sesion.</summary>
        public readonly float SessionSeconds;
        /// <summary>evento: "perilla", "materializar", "hechizo", "resultado", "reinicio"...</summary>
        public readonly string Event;
        /// <summary>variable: rotulo de la variable o del objeto.</summary>
        public readonly string Variable;
        /// <summary>anterior: valor previo (null = vacio).</summary>
        public readonly float? Previous;
        /// <summary>nuevo: valor nuevo (null = vacio).</summary>
        public readonly float? Current;
        /// <summary>resultado: descripcion del resultado medido.</summary>
        public readonly string Result;
        /// <summary>valor_medido: numero medido (null = vacio).</summary>
        public readonly float? MeasuredValue;
        /// <summary>unidad: unidad del valor medido.</summary>
        public readonly string Unit;
        /// <summary>id_experimento: experimento al que pertenece la fila (&lt;= 0 = vacio).</summary>
        public readonly int ExperimentId;
        /// <summary>notas: texto libre (p. ej. "superpuesto").</summary>
        public readonly string Notes;
        /// <summary>spec_sello: sello del banco en una linea (LabSealBenchMath.Serialize); vacio fuera del banco.</summary>
        public readonly string SealSpec;
        /// <summary>receta: receta base del banco de sellos.</summary>
        public readonly string Recipe;
        /// <summary>semilla: semilla del temblor del banco (null = vacio).</summary>
        public readonly int? Seed;

        public LabLogRow(DateTime timestamp, float sessionSeconds, string eventName, string variable = null,
            float? previous = null, float? current = null, string result = null, float? measuredValue = null,
            string unit = null, int experimentId = 0, string notes = null, string sealSpec = null, string recipe = null,
            int? seed = null)
        {
            SealSpec = sealSpec;
            Recipe = recipe;
            Seed = seed;
            Timestamp = timestamp;
            SessionSeconds = sessionSeconds;
            Event = eventName;
            Variable = variable;
            Previous = previous;
            Current = current;
            Result = result;
            MeasuredValue = measuredValue;
            Unit = unit;
            ExperimentId = experimentId;
            Notes = notes;
        }
    }

    /// <summary>
    /// Formato del registro CSV del laboratorio (logica pura, testeable en EditMode). Decision: separador ';' y coma decimal para
    /// que Excel en español lo abra en columnas sin importar nada; el formato numerico es fijo (no depende del idioma del sistema).
    /// Crea strings: usar solo en eventos, nunca por frame.
    /// </summary>
    public static class LabCsv
    {
        /// <summary>Separador de columnas. Si se cambia, cambiar tambien <see cref="Header"/>.</summary>
        public const char Separator = ';';
        /// <summary>Separador decimal de los numeros del registro.</summary>
        public const string DecimalSeparator = ",";
        /// <summary>Formato de fecha y hora (ISO, sin zona).</summary>
        public const string DateFormat = "yyyy-MM-dd HH:mm:ss";
        /// <summary>Formato de los valores numericos (hasta 3 decimales, sin ceros de mas).</summary>
        public const string ValueFormat = "0.###";
        /// <summary>Formato del tiempo de sesion en segundos.</summary>
        public const string SessionTimeFormat = "0.00";

        /// <summary>
        /// Encabezado del CSV (14 columnas, separadas por <see cref="Separator"/>). Las tres ultimas (banco de sellos del
        /// Laboratorio v2) se agregaron al final para no correr las columnas de los registros viejos.
        /// </summary>
        public const string Header = "fecha_hora;t_sesion_s;evento;variable;anterior;nuevo;resultado;valor_medido;unidad;id_experimento;notas;spec_sello;receta;semilla";

        /// <summary>Cantidad de columnas de <see cref="Header"/>.</summary>
        public const int ColumnCount = 14;

        /// <summary>Formato numerico con coma decimal y sin separador de miles, independiente de la cultura del sistema.</summary>
        public static readonly NumberFormatInfo NumberFormat = CreateNumberFormat();

        /// <summary>Arma una linea del CSV (sin salto de linea final) con las columnas de <see cref="Header"/>.</summary>
        public static string FormatRow(in LabLogRow row)
        {
            var sb = new StringBuilder(256);
            sb.Append(row.Timestamp.ToString(DateFormat, CultureInfo.InvariantCulture)).Append(Separator);
            sb.Append(FormatNumber(row.SessionSeconds, SessionTimeFormat)).Append(Separator);
            sb.Append(Escape(row.Event)).Append(Separator);
            sb.Append(Escape(row.Variable)).Append(Separator);
            sb.Append(FormatNumber(row.Previous, ValueFormat)).Append(Separator);
            sb.Append(FormatNumber(row.Current, ValueFormat)).Append(Separator);
            sb.Append(Escape(row.Result)).Append(Separator);
            sb.Append(FormatNumber(row.MeasuredValue, ValueFormat)).Append(Separator);
            sb.Append(Escape(row.Unit)).Append(Separator);
            if (row.ExperimentId > 0) sb.Append(row.ExperimentId.ToString(CultureInfo.InvariantCulture));
            sb.Append(Separator);
            sb.Append(Escape(row.Notes)).Append(Separator);
            sb.Append(Escape(row.SealSpec)).Append(Separator);
            sb.Append(Escape(row.Recipe)).Append(Separator);
            if (row.Seed.HasValue) sb.Append(row.Seed.Value.ToString(CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        /// <summary>
        /// Escapa un campo de texto: si contiene el separador, comillas dobles o saltos de linea, lo envuelve en comillas y duplica
        /// las comillas internas. null = campo vacio.
        /// </summary>
        public static string Escape(string field)
        {
            if (string.IsNullOrEmpty(field)) return string.Empty;
            bool needsQuotes = field.IndexOf(Separator) >= 0 || field.IndexOf('"') >= 0
                || field.IndexOf('\n') >= 0 || field.IndexOf('\r') >= 0;
            if (!needsQuotes) return field;
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }

        /// <summary>Numero con coma decimal; vacio si es null, NaN o infinito.</summary>
        public static string FormatNumber(float? value, string format = ValueFormat)
        {
            if (!value.HasValue) return string.Empty;
            float v = value.Value;
            if (float.IsNaN(v) || float.IsInfinity(v)) return string.Empty;
            return v.ToString(format, NumberFormat);
        }

        private static NumberFormatInfo CreateNumberFormat()
        {
            var nfi = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
            nfi.NumberDecimalSeparator = DecimalSeparator;
            nfi.NumberGroupSeparator = string.Empty;
            return NumberFormatInfo.ReadOnly(nfi);
        }
    }
}
