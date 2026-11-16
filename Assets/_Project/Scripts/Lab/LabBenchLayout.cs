using UnityEngine;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Medidas puras de la consola frontal del Laboratorio v5 (Docs/LABORATORIO_V5_PLAN.md, punto A.3). El jugador se para en
    /// <see cref="StandZ"/> (marco del laboratorio: +Z hacia la pista, +X a su derecha) y mira siempre hacia la pista: una consola
    /// baja y abierta de tres paneles contiguos inclinados <see cref="Tilt"/>°, borde de abajo a <see cref="PanelBottom"/> m y de
    /// arriba ≤ <see cref="TopMax"/> m (se ve la pista por encima):
    /// - izquierda (<see cref="Left"/>): el sello, en 4 paginas con pestañas (Hechizo, Signos, Tinta, Afinado);
    /// - centro (<see cref="Center"/>): botones abajo (Borrar dibujo, LANZAR, Reiniciar sello), el atril de dibujo y arriba la
    ///   pantalla «Vas a lanzar»;
    /// - derecha (<see cref="Right"/>): objetos del campo y entorno, en 4 paginas (Objetos, Posición, Entorno, Fuego).
    /// Todo control queda a ≤ <see cref="ReachMax"/> m en horizontal del punto de pie y a ≤ 90° del frente (los laterales giran
    /// ±63°: con el alcance de 0,7 m y controles de 9 cm para VR no entran en un frente mas angosto).
    /// Marco de un panel: +X a lo ancho, +Z subiendo la pendiente (s), +Y la normal (hacia el jugador y arriba).
    /// </summary>
    public static class LabBenchLayout
    {
        /// <summary>Donde se para el jugador (Z del marco del laboratorio; X = 0).</summary>
        public const float StandZ = -0.2f;
        /// <summary>Distancia horizontal maxima (m) de cualquier control al punto donde se para el jugador.</summary>
        public const float ReachMax = 0.7f;
        /// <summary>Inclinacion de los paneles (° desde la horizontal).</summary>
        public const float Tilt = 40f;
        /// <summary>Altura del borde de abajo de los paneles y tope del borde de arriba (m sobre el piso).</summary>
        public const float PanelBottom = 0.8f, TopMax = 1.25f;
        /// <summary>Distancia horizontal (m) del jugador al borde de abajo de cada panel (medida sobre su eje).</summary>
        public const float PanelNear = 0.34f;
        /// <summary>Ancho de los paneles laterales y del central (m).</summary>
        public const float SideWidth = 0.44f, CenterWidth = 0.4f;
        /// <summary>Largo de la losa sobre la pendiente (m): laterales (controles + copete) y central (botones, atril, pantalla).</summary>
        public const float SideLength = 0.66f, CenterLength = 0.68f;
        /// <summary>Donde empieza el copete de los laterales (titulo y tira de ayuda, sin controles).</summary>
        public const float CrestFrom = 0.53f;
        /// <summary>Altura del ojo usada para medir la legibilidad (m).</summary>
        public const float EyeHeight = 1.6f;

        /// <summary>Paneles: 0 izquierda (sello), 1 centro (atril), 2 derecha (campo y entorno).</summary>
        public const int PanelCount = 3;
        public const int Left = 0, Center = 1, Right = 2;

        // ---------------------------------------------------------------- paneles laterales

        /// <summary>Centro de cada fila de controles sobre la pendiente (de arriba hacia abajo; paso <see cref="RowPitch"/>).</summary>
        public static readonly float[] RowS = { 0.45f, 0.29f, 0.13f };
        /// <summary>Paso entre filas (m): plaquita del rotulo arriba, control, ventanita de la lectura abajo.</summary>
        public const float RowPitch = 0.16f;
        /// <summary>Columnas de controles (paso = <see cref="LabPanelLayout.MinControlPitch"/>).</summary>
        public static readonly float[] ColumnX = { -LabPanelLayout.MinControlPitch * 0.5f, LabPanelLayout.MinControlPitch * 0.5f };
        /// <summary>Paginas de cada lateral y pestañas (fila de abajo, junto al borde cercano).</summary>
        public const int PagesPerPanel = 4;
        public const float TabS = 0.024f, TabPitch = 0.105f, TabWidth = 0.095f, TabDepth = 0.045f;
        /// <summary>Botones de una fila de tres (+ Caja, Borrar…): paso y tamaño (m).</summary>
        public const float ButtonPitch = 0.14f, ButtonWidth = 0.13f, ButtonDepth = 0.1f;
        /// <summary>Grillas de iconos (sigilos, signos, lista de objetos): columnas y paso de celda (m, colliders contiguos).</summary>
        public const int GridColumns = 5;
        public const float CellPitch = 0.085f;
        /// <summary>Centro de la grilla de sigilos (2 filas), de signos (3 filas) y de la lista de objetos del campo.</summary>
        public const float SigilGridS = 0.27f, SignGridS = 0.33f, SlotS = 0.29f;
        /// <summary>Fila de controles debajo de la grilla de signos.</summary>
        public const float SignRowS = 0.125f;

        // ---------------------------------------------------------------- panel central

        /// <summary>Fila de botones del centro: LANZAR al medio, Borrar dibujo y Reiniciar sello a los costados.</summary>
        public const float ButtonsS = 0.06f, LaunchWidth = 0.15f, FrontButtonX = 0.14f, FrontButtonWidth = 0.11f, CenterButtonDepth = 0.09f;
        /// <summary>Atril de dibujo: centro sobre la pendiente y lado del papel (m).</summary>
        public const float PadS = 0.275f, PadSize = 0.3f;
        /// <summary>Pantalla «Vas a lanzar»: desde/hasta sobre la pendiente y ancho (m; mas ancha que el panel, atras se abre lugar).</summary>
        public const float ScreenFrom = 0.455f, ScreenTo = 0.665f, ScreenWidth = 0.56f;
        /// <summary>Alto minimo de la letra de la pantalla (m) y distancia maxima de lectura desde el ojo.</summary>
        public const float ScreenTextMin = 0.022f, ScreenReadMax = 0.9f;

        /// <summary>Mitad del angulo (°) que ocupa un panel de ancho <paramref name="width"/> visto desde el jugador.</summary>
        public static float HalfAngle(float width) => Mathf.Atan2(width * 0.5f, PanelNear) * Mathf.Rad2Deg;

        /// <summary>Ancho de un panel.</summary>
        public static float PanelWidth(int panel) => panel == Center ? CenterWidth : SideWidth;

        /// <summary>Largo de la losa de un panel sobre la pendiente.</summary>
        public static float PanelLength(int panel) => panel == Center ? CenterLength : SideLength;

        /// <summary>Giro (° alrededor de Y, 0 = hacia la pista, + a la derecha) de un panel: los tres quedan contiguos.</summary>
        public static float PanelYaw(int panel)
        {
            if (panel == Center) return 0f;
            float yaw = HalfAngle(CenterWidth) + HalfAngle(SideWidth);
            return panel == Left ? -yaw : yaw;
        }

        /// <summary>Punto (x a lo ancho, s sobre la pendiente) de un panel, relativo al jugador (y = altura sobre el piso).</summary>
        public static Vector3 PanelPoint(int panel, float x, float s)
        {
            float t = Tilt * Mathf.Deg2Rad;
            var local = new Vector3(x, PanelBottom + s * Mathf.Sin(t), PanelNear + s * Mathf.Cos(t));
            return Quaternion.Euler(0f, PanelYaw(panel), 0f) * local;
        }

        /// <summary>Altura (m) del borde de arriba de una losa de largo <paramref name="length"/>.</summary>
        public static float TopHeight(float length) => PanelBottom + length * Mathf.Sin(Tilt * Mathf.Deg2Rad);

        /// <summary>X de la pestaña <paramref name="index"/> (0..<see cref="PagesPerPanel"/>-1), centradas.</summary>
        public static float TabX(int index) => (index - (PagesPerPanel - 1) * 0.5f) * TabPitch;

        /// <summary>Distancia horizontal (m) de un punto relativo al jugador.</summary>
        public static float Reach(Vector3 relative) => new Vector2(relative.x, relative.z).magnitude;

        /// <summary>Angulo horizontal (°, 0 = hacia la pista) de un punto relativo al jugador.</summary>
        public static float Bearing(Vector3 relative) => Mathf.Atan2(relative.x, relative.z) * Mathf.Rad2Deg;

        /// <summary>Distancia (m) del ojo (<see cref="EyeHeight"/>) a un punto relativo al jugador.</summary>
        public static float EyeDistance(Vector3 relative) => Vector3.Distance(new Vector3(0f, EyeHeight, 0f), relative);
    }
}
