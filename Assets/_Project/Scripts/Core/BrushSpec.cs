using UnityEngine;

namespace SpellSimulator.Core
{
    /// <summary>Punta del pincel: define como entra y sale el trazo (solo estetica por ahora).</summary>
    public enum BrushTip { Fine = 0, Round = 1, Flat = 2 }

    /// <summary>Forma del trazo: uniforme o que se afina en los extremos (solo estetica por ahora).</summary>
    public enum BrushShape { Uniform = 0, Tapered = 1 }

    /// <summary>
    /// Un pincel (Docs/LIENZOS_PLAN.md, Bloque B): lapiz, pluma y pincel son lo mismo con otro grosor, punta y forma. El grosor NO entra al
    /// reconocimiento (TensorRasterizer dibuja siempre con 2 px) ni al hechizo: solo cambia como se ve la tinta y cuanto gasta.
    /// </summary>
    public struct BrushSpec
    {
        public string name;
        /// <summary>Ancho del trazo en metros.</summary>
        public float widthMeters;
        public BrushTip tip;
        public BrushShape shape;

        public BrushSpec(string name, float widthMeters, BrushTip tip = BrushTip.Round, BrushShape shape = BrushShape.Uniform)
        {
            this.name = name;
            this.widthMeters = widthMeters;
            this.tip = tip;
            this.shape = shape;
        }
    }

    /// <summary>Reglas puras de los pinceles (sin MonoBehaviour: se prueban en EditMode).</summary>
    public static class BrushRules
    {
        public const float MinWidth = 0.002f, MaxWidth = 0.1f;
        /// <summary>Ancho (m) y gasto de tinta (por metro) del lapiz de siempre: el resto escala desde aca.</summary>
        public const float LegacyPenWidth = 0.008f, LegacyPenInkPerMeter = 8f;
        /// <summary>Los ToolDefinition viejos guardaban un ancho base y el jugador elegia un multiplicador; «fino» (0,2) era el de arranque.</summary>
        public const float LegacyDefinitionScale = 0.2f;

        /// <summary>Pinceles del restocker, del mas fino al mas ancho.</summary>
        public static readonly BrushSpec[] Catalog =
        {
            new BrushSpec("Pluma fina", 0.004f, BrushTip.Fine),
            new BrushSpec("Lápiz", 0.008f, BrushTip.Round),
            new BrushSpec("Pincel", 0.02f, BrushTip.Round, BrushShape.Tapered),
            new BrushSpec("Pincel plano", 0.04f, BrushTip.Flat),
            new BrushSpec("Brocha", 0.08f, BrushTip.Flat, BrushShape.Tapered),
        };

        public static float ClampWidth(float meters) => Mathf.Clamp(meters, MinWidth, MaxWidth);

        /// <summary>Ancho efectivo de un ToolDefinition viejo (ancho base × multiplicador de arranque).</summary>
        public static float FromLegacyDefinition(float baseStrokeWidth) => ClampWidth(baseStrokeWidth * LegacyDefinitionScale);

        /// <summary>Tinta gastada por metro de trazo: crece con el ancho pero menos que proporcional (un pincel de 5× gasta ~3,6×).</summary>
        public static float InkPerMeter(float widthMeters) => LegacyPenInkPerMeter * Mathf.Pow(ClampWidth(widthMeters) / LegacyPenWidth, 0.8f);

        public static BrushSpec Sanitize(BrushSpec spec)
        {
            spec.widthMeters = ClampWidth(spec.widthMeters);
            if (string.IsNullOrEmpty(spec.name)) spec.name = "Pincel";
            return spec;
        }

        /// <summary>Indice del pincel del catalogo mas parecido en ancho (-1 si el catalogo esta vacio).</summary>
        public static int NearestInCatalog(float widthMeters)
        {
            int best = -1;
            float bestError = float.MaxValue;
            for (int i = 0; i < Catalog.Length; i++)
            {
                float e = Mathf.Abs(Mathf.Log(Mathf.Max(1e-4f, widthMeters) / Catalog[i].widthMeters));
                if (e < bestError) { bestError = e; best = i; }
            }
            return best;
        }
    }
}
