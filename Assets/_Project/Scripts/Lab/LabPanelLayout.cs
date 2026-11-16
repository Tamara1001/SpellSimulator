namespace SpellSimulator.Lab
{
    /// <summary>
    /// Medidas puras del texto de las consolas del laboratorio (Docs/LABORATORIO_V3_PLAN.md, punto (a)). Las usa MapLab al
    /// generar los paneles. Regla: ningun texto coplanar con la superficie que tiene debajo (TMP transparente con ZTest LEqual
    /// parpadea por z-fighting y el SDF no tiene Offset); la solucion es la altura, NO ZTest Always (se veria a traves de las
    /// manos y las perillas). Alturas en metros, en el marco del panel (+Y la normal).
    /// </summary>
    public static class LabPanelLayout
    {
        /// <summary>Inclinacion de las consolas (grados). Antes 25°: a 35° el texto acostado se lee mas de frente.</summary>
        public const float PanelTilt = 35f;

        /// <summary>Tope de la franja de color de cada fila sobre la losa del panel (la superficie donde apoyan los controles).</summary>
        public const float SurfaceTop = 0.004f;

        /// <summary>Alto de la plaquita crema (rotulo) y de la ventanita oscura (lectura) sobre la franja.</summary>
        public const float PlaqueHeight = 0.003f;

        /// <summary>Tope de la plaquita y de la ventanita.</summary>
        public const float PlaqueTop = SurfaceTop + PlaqueHeight;

        /// <summary>Separacion del texto sobre lo mas alto que tenga debajo (≥ 6 mm; el tablero vertical pide ≥ 4 mm).</summary>
        public const float TextLift = 0.006f;

        /// <summary>Separacion minima aceptable (la que verifica el test, con margen de 1 mm sobre TextLift).</summary>
        public const float MinClearance = 0.005f;

        /// <summary>Tamaños de fuente fijos (sin autosize): 0.16 ≈ 1,6 cm de alto en TMP 3D.</summary>
        public const float LabelFontSize = 0.16f, ReadoutFontSize = 0.20f, TitleFontSize = 0.22f;

        /// <summary>Metros de alto por unidad de fontSize en TextMeshPro 3D (fontSize 10 ≈ 1 m).</summary>
        public const float MetersPerFontUnit = 0.1f;

        /// <summary>Alto minimo de letra legible a 0,8 m (plan (c).9): 1,5 cm.</summary>
        public const float TextHeightMin = 0.015f;

        /// <summary>Largo maximo de un rotulo de control (una sola linea, sin ajuste).</summary>
        public const int MaxLabelChars = 10;

        /// <summary>
        /// Paso minimo entre controles de una fila (m): plaquita de 18 cm (rotulo ≤ 10 caracteres de 1,6 cm) + 4 cm de aire, con
        /// colliders de 12 cm que no se tocan (rayo de VR con temblor). v3: 0.30; v4: 0.22; v6: 0.19 con plaquita de 15 cm
        /// (rotulos ≤ 10 caracteres) para entrar 5 columnas en un ala de 0,96 m sin paginas.
        /// </summary>
        public const float MinControlPitch = 0.19f;

        /// <summary>Ancho de la plaquita del rotulo y de la ventanita de la lectura (m): deja ≥ 4 cm entre controles vecinos.</summary>
        public const float PlaqueWidth = 0.15f;

        /// <summary>Altura del texto que apoya sobre una superficie cuyo tope esta en <paramref name="surfaceTop"/>.</summary>
        public static float TextY(float surfaceTop) => surfaceTop + TextLift;

        /// <summary>Alto aproximado de la letra (m) para un fontSize de TMP 3D.</summary>
        public static float TextHeight(float fontSize) => fontSize * MetersPerFontUnit;

        /// <summary>True si el rotulo entra en una linea sin desbordar.</summary>
        public static bool LabelFits(string label) => label != null && label.Length <= MaxLabelChars;

        /// <summary>
        /// Cantidad de filas para <paramref name="controls"/> controles en un ancho util <paramref name="rowSpan"/> con paso
        /// ≥ <see cref="MinControlPitch"/> (nunca se quitan controles: si no entran, mas filas).
        /// </summary>
        public static int RowsNeeded(int controls, float rowSpan)
        {
            if (controls <= 0) return 0;
            int perRow = MaxPerRow(rowSpan);
            return (controls + perRow - 1) / perRow;
        }

        /// <summary>Controles que entran en una fila de ancho <paramref name="rowSpan"/> con paso ≥ <see cref="MinControlPitch"/> (≥ 1).</summary>
        public static int MaxPerRow(float rowSpan)
        {
            int n = (int)(rowSpan / MinControlPitch + 1e-4f);
            return n < 1 ? 1 : n;
        }
    }
}
