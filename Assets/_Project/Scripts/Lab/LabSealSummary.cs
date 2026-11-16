using System;
using System.Text;
using UnityEngine;
using SpellSimulator.Casting;
using SpellSimulator.Compiler;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Resultado del ultimo lanzamiento del banco, para la banda de resultado (solo con lentes, Laboratorio v4 punto 10).
    /// <see cref="HasResult"/> es false hasta el primer lanzamiento.
    /// </summary>
    public readonly struct LabSealResult
    {
        /// <summary>True si ya se lanzo al menos un sello.</summary>
        public readonly bool HasResult;
        /// <summary>True si el sello compilo un hechizo valido.</summary>
        public readonly bool Valid;
        /// <summary>Elemento del hechizo (None si no fue valido).</summary>
        public readonly ElementType Element;
        /// <summary>Calidad en porcentaje (0..100).</summary>
        public readonly float QualityPercent;
        /// <summary>Motivo: <c>spell.status</c> del compilador (puede ser vacio).</summary>
        public readonly string Reason;

        public LabSealResult(bool hasResult, bool valid, ElementType element, float qualityPercent, string reason)
        {
            HasResult = hasResult; Valid = valid; Element = element; QualityPercent = qualityPercent; Reason = reason ?? string.Empty;
        }
    }

    /// <summary>
    /// Textos del cartel «Vas a lanzar» (Laboratorio v4, punto 5): una fila por dato, cada una con su rotulo y el color de su
    /// grupo, mas el estado previsto (que solo se ve con lentes). Logica pura: reutiliza el StringBuilder que le pasan (no lo
    /// crea) y se llama solo cuando cambia una variable, no por frame.
    /// </summary>
    public static class LabSealSummary
    {
        /// <summary>Filas del cartel: hechizo, sigilo, signos, tinta, papel y de donde sale el lanzamiento.</summary>
        public const int RowCount = 6;
        public const int RowRecipe = 0, RowSigil = 1, RowSigns = 2, RowInk = 3, RowPaper = 4, RowSource = 5;

        private static readonly string[] RowLabels = { "Hechizo", "Sigilo", "Signos", "Tinta", "Papel", "Se lanza" };
        // Grupo (color) de cada fila: 1 hechizo, 2 signos, 3 papel y tinta, 5 papel de prueba; 0 = neutro.
        private static readonly int[] RowGroups = { 1, 1, 2, 3, 5, 0 };

        /// <summary>Rotulo de una fila (vacio fuera de rango).</summary>
        public static string RowLabel(int row) => row >= 0 && row < RowCount ? RowLabels[row] : string.Empty;

        /// <summary>Grupo de una fila (para el color): 1..5, o 0 = neutro.</summary>
        public static int RowGroup(int row) => row >= 0 && row < RowCount ? RowGroups[row] : 0;

        /// <summary>Etiqueta en español del elemento de un sigilo por su id (best effort: «fire», «water», «wind…», «earth»…); si no lo reconoce, devuelve el id.</summary>
        public static string ElementLabel(string sigilId)
        {
            if (string.IsNullOrEmpty(sigilId)) return "sin sigilo";
            string id = sigilId.ToLowerInvariant();
            if (id.Contains("fire")) return "fuego";
            if (id.Contains("water")) return "agua";
            if (id.Contains("wind") || id.Contains("air")) return "viento";
            if (id.Contains("earth")) return "tierra";
            if (id.Contains("light")) return "luz";
            return sigilId;
        }

        /// <summary>
        /// Valor de una fila (sin el rotulo). Limpia <paramref name="sb"/>. <paramref name="nameOf"/> traduce ids de receta, sigilo
        /// y signo a nombres (null o vacio = usar el id); <paramref name="fromDrawing"/> = el atril de dibujo tiene trazos.
        /// </summary>
        public static void BuildRow(in LabSealSpec spec, int row, StringBuilder sb, Func<string, string> nameOf, bool fromDrawing)
        {
            if (sb == null) return;
            sb.Clear();
            switch (row)
            {
                case RowRecipe:
                    sb.Append(Name(spec.recipeId, nameOf, "libre"));
                    break;
                case RowSigil:
                    sb.Append(string.IsNullOrEmpty(spec.sigilId) ? "ninguno" : Name(spec.sigilId, nameOf, "-"));
                    break;
                case RowSigns:
                    if (spec.signCount <= 0 || string.IsNullOrEmpty(spec.signId)) sb.Append("ninguno");
                    else
                    {
                        sb.Append(spec.signCount).Append(" × ").Append(Name(spec.signId, nameOf, "-"));
                        sb.Append(spec.turnDeg >= LabSealSpec.TurnOutDeg * 0.5f ? ", hacia afuera" : ", hacia adentro");
                    }
                    break;
                case RowInk:
                    sb.Append(LabSealBenchMath.InkNames[Mathf.Clamp((int)spec.ink, 0, LabSealBenchMath.InkNames.Length - 1)]);
                    if (spec.paperFace < 0) sb.Append(", disparo invertido");
                    break;
                case RowPaper:
                    sb.Append(spec.paperSize.ToString("0.##", LabCsv.NumberFormat)).Append(" m · ");
                    sb.Append(LabSealBenchMath.PaperPoseName(spec.paperPose));
                    sb.Append(" · a ").Append(spec.paperDistance.ToString("0.#", LabCsv.NumberFormat)).Append(" m");
                    break;
                case RowSource:
                    sb.Append(fromDrawing ? "tu dibujo del atril" : "el sello de los controles");
                    break;
            }
        }

        /// <summary>Resumen completo («Rotulo: valor» por linea), para registros y pruebas. Limpia <paramref name="sb"/>.</summary>
        public static void Build(in LabSealSpec spec, StringBuilder sb) => Build(spec, sb, null);

        /// <summary>Igual que <see cref="Build(in LabSealSpec, StringBuilder)"/> pero con nombres (<paramref name="nameOf"/>).</summary>
        public static void Build(in LabSealSpec spec, StringBuilder sb, Func<string, string> nameOf, bool fromDrawing = false)
        {
            if (sb == null) return;
            var row = new StringBuilder(64);
            sb.Clear();
            for (int i = 0; i < RowCount; i++)
            {
                BuildRow(spec, i, row, nameOf, fromDrawing);
                if (i > 0) sb.Append('\n');
                sb.Append(RowLabels[i]).Append(": ").Append(row);
            }
        }

        /// <summary>Estado previsto del sello sintetizado: «preparado» si el hueco del anillo llega a StampPattern.RingGapDeg; si no, «cerrado» (se lanza).</summary>
        public static string Prediction(in LabSealSpec spec)
            => spec.ringGapDeg >= StampPattern.RingGapDeg ? "preparado" : "cerrado";

        private static string Name(string id, Func<string, string> nameOf, string whenEmpty)
        {
            if (string.IsNullOrEmpty(id)) return whenEmpty;
            string name = nameOf != null ? nameOf(id) : null;
            return string.IsNullOrEmpty(name) ? id : name;
        }
    }
}
