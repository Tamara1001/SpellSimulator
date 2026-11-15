using System;
using System.Text;
using UnityEngine;
using SpellSimulator.Casting;
using SpellSimulator.Compiler;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Resultado del ultimo lanzamiento del banco, para la banda de resultado de la pizarra (Laboratorio v3, (c).3).
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
    /// Resumen en español del sello armado («Vas a lanzar»): receta, elemento, signos, tinta, posicion del papel y estado previsto.
    /// Logica pura: reutiliza el StringBuilder que le pasan (no lo crea) y se llama solo cuando cambia una variable, no por frame.
    /// </summary>
    public static class LabSealSummary
    {
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

        /// <summary>Resumen con los ids tal cual (receta y tipo de signo sin traducir). Limpia <paramref name="sb"/> y lo deja con una linea por dato.</summary>
        public static void Build(in LabSealSpec spec, StringBuilder sb) => Build(spec, sb, null);

        /// <summary>
        /// Igual que <see cref="Build(in LabSealSpec, StringBuilder)"/> pero con <paramref name="nameOf"/> para mostrar el nombre de
        /// la receta y del signo (recibe el id, devuelve el nombre; null o vacio = usar el id).
        /// </summary>
        public static void Build(in LabSealSpec spec, StringBuilder sb, Func<string, string> nameOf)
        {
            if (sb == null) return;
            sb.Clear();

            sb.Append("Receta: ").Append(Name(spec.recipeId, nameOf, "libre"));
            sb.Append("\nElemento: ").Append(ElementLabel(spec.sigilId));

            sb.Append("\nSignos: ");
            if (spec.signCount <= 0 || string.IsNullOrEmpty(spec.signId)) sb.Append("ninguno");
            else
            {
                sb.Append(spec.signCount).Append(" × ").Append(Name(spec.signId, nameOf, "-"));
                sb.Append(spec.turnDeg >= LabSealSpec.TurnOutDeg * 0.5f ? " (afuera)" : " (adentro)");
            }

            sb.Append("\nTinta: ").Append(LabSealBenchMath.InkNames[Mathf.Clamp((int)spec.ink, 0, LabSealBenchMath.InkNames.Length - 1)]);

            sb.Append("\nPapel: ").Append(spec.paperSize.ToString("0.##", LabCsv.NumberFormat)).Append(" m, ");
            sb.Append(LabSealBenchMath.PaperPoseName(spec.paperPose));
            if (spec.paperFace < 0) sb.Append(" (cara abajo)");
            else if (spec.paperPose == LabSealSpec.FreePaperPose && spec.paperTiltDeg > 0f)
                sb.Append(" (").Append(spec.paperTiltDeg.ToString("0", LabCsv.NumberFormat)).Append("°)");

            sb.Append("\nEstado previsto: ").Append(spec.ringGapDeg >= StampPattern.RingGapDeg ? "preparado" : "cerrado");
        }

        private static string Name(string id, Func<string, string> nameOf, string whenEmpty)
        {
            if (string.IsNullOrEmpty(id)) return whenEmpty;
            string name = nameOf != null ? nameOf(id) : null;
            return string.IsNullOrEmpty(name) ? id : name;
        }
    }
}
