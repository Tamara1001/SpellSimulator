using UnityEngine;
using SpellSimulator.Core;

namespace SpellSimulator.Canvas
{
    /// <summary>Que lienzo / libreta tiene elegido el restocker (tipo, tamaño, forma, hojas). Se convierte en un <see cref="CanvasSpec"/> valido.</summary>
    public struct RestockSelection
    {
        public CanvasKind kind;
        public CanvasSize size;
        public CanvasShape shape;
        public int pages;

        public static RestockSelection Default => new RestockSelection { kind = CanvasKind.Sheet, size = CanvasSize.M, shape = CanvasShape.Square, pages = CanvasRules.DefaultPages };

        public CanvasSpec ToSpec() => CanvasRules.Sanitize(new CanvasSpec(kind, size, shape, pages));

        /// <summary>Vuelve a un estado valido (la libreta fija su forma; la hoja suelta no es redonda).</summary>
        public RestockSelection Sanitized()
        {
            CanvasSpec s = ToSpec();
            return new RestockSelection { kind = s.kind, size = s.size, shape = s.shape, pages = kind == CanvasKind.Notebook ? s.pages : Mathf.Clamp(pages, 1, CanvasRules.MaxPages) };
        }
    }

    /// <summary>Reglas puras del mueble «restocker» (Docs/LIENZOS_PLAN.md, Bloque C): selector de lienzo y pincel, y tope de lo sacado.</summary>
    public static class RestockRules
    {
        /// <summary>Cuantos objetos de cada clase puede haber sacados del mueble a la vez: al pasarse se borra el mas viejo.</summary>
        public const int MaxSpawnedPerKind = 8;

        public static RestockSelection ToggleKind(RestockSelection s)
        {
            s.kind = s.kind == CanvasKind.Sheet ? CanvasKind.Notebook : CanvasKind.Sheet;
            return s.Sanitized();
        }

        public static RestockSelection CycleSize(RestockSelection s)
        {
            s.size = (CanvasSize)(((int)s.size + 1) % 3);
            return s.Sanitized();
        }

        /// <summary>Cuadrado ↔ rectangular (solo hojas sueltas: la libreta tiene la forma de su tamaño).</summary>
        public static RestockSelection CycleShape(RestockSelection s)
        {
            if (s.kind == CanvasKind.Sheet) s.shape = s.shape == CanvasShape.Square ? CanvasShape.Rect : CanvasShape.Square;
            return s.Sanitized();
        }

        /// <summary>Suma o resta hojas de la libreta (1..8); en una hoja suelta no hace nada.</summary>
        public static RestockSelection AddPages(RestockSelection s, int delta)
        {
            if (s.kind == CanvasKind.Notebook) s.pages = Mathf.Clamp(s.pages + delta, 1, CanvasRules.MaxPages);
            return s.Sanitized();
        }

        /// <summary>Texto corto del selector: «Libreta M · 8 hojas» / «Lienzo L rectangular».</summary>
        public static string Describe(RestockSelection s)
        {
            CanvasSpec spec = s.ToSpec();
            Vector2 d = CanvasRules.Dimensions(spec);
            string size = spec.size.ToString();
            string shape = spec.shape == CanvasShape.Round ? "circular" : spec.shape == CanvasShape.Rect ? "rectangular" : "cuadrado";
            string cm = $"{Mathf.RoundToInt(d.x * 100f)}×{Mathf.RoundToInt(d.y * 100f)} cm";
            if (spec.kind == CanvasKind.Notebook) return $"Libreta {size} {shape} · {spec.pages} hoja{(spec.pages == 1 ? "" : "s")}\n{cm}";
            return $"Lienzo {size} {shape}\n{cm}";
        }

        /// <summary>Texto del pincel elegido: nombre y grosor.</summary>
        public static string Describe(BrushSpec brush) => $"{brush.name} · {Mathf.RoundToInt(brush.widthMeters * 1000f)} mm";
    }
}
