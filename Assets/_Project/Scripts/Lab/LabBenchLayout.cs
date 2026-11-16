using UnityEngine;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Medidas puras de la consola frontal del Laboratorio v6 (Docs/LABORATORIO_V6_PLAN.md, B.1 y B.2). El jugador se para en
    /// <see cref="StandZ"/> (marco del laboratorio: +Z hacia la pista, +X a su derecha) y mira hacia la pista. Tres paneles:
    /// - <see cref="Left"/> y <see cref="Right"/>: dos alas grandes (~0,96 × 1 m) a los costados, casi verticales
    ///   (<see cref="SideTilt"/>°), borde de abajo a <see cref="SideBottom"/> m y de arriba a ≤ <see cref="TopMax"/> m, a ~0,9 m del
    ///   punto de pie y giradas <see cref="SideYaw"/>° hacia adentro. Sin paginas ni pestañas: todo a la vez, en filas de hasta
    ///   <see cref="Columns"/> controles con titulo de grupo. Izquierda el sello, derecha el campo y el entorno;
    /// - <see cref="Center"/>: el atril, de arriba hacia abajo el lienzo de dibujo, los botones (LANZAR, Borrar dibujo, Reiniciar
    ///   sello) y la pantalla «Vas a lanzar», inclinado <see cref="CenterTilt"/>°.
    /// Alcance: el centro (lapicera y botones) a ≤ <see cref="ReachMax"/> m en horizontal del punto de pie; las alas, mas grandes que
    /// un brazo, a ≤ <see cref="SideReachMax"/> m (se usa el rayo del control en VR y el mouse en PC; el jugador gira, nunca mas de 90°).
    /// Marco de un panel: +X a lo ancho, +Z subiendo la pendiente (s), +Y la normal (hacia el jugador y arriba).
    /// </summary>
    public static class LabBenchLayout
    {
        /// <summary>Donde se para el jugador (Z del marco del laboratorio; X = 0).</summary>
        public const float StandZ = -0.2f;
        /// <summary>Distancia horizontal maxima (m) al punto de pie: panel central (lapicera y botones) y alas.</summary>
        public const float ReachMax = 0.9f, SideReachMax = 1.5f;
        /// <summary>Inclinacion de las alas y del atril (° desde la horizontal).</summary>
        public const float SideTilt = 65f, CenterTilt = 60f;
        /// <summary>Altura del borde de abajo de las alas y del atril, y tope del borde de arriba (m sobre el piso).</summary>
        public const float SideBottom = 0.94f, CenterBottom = 0.95f, TopMax = 1.86f;
        /// <summary>Ancho de las alas y del atril (m).</summary>
        public const float SideWidth = 0.96f, CenterWidth = 0.4f;
        /// <summary>Largo de la losa sobre la pendiente (m): alas y atril.</summary>
        public const float SideLength = 1.0f, CenterLength = 0.75f;
        /// <summary>Posicion del borde de abajo (centro) de cada ala en el marco del banco y giro hacia adentro (°).</summary>
        public const float SideX = 0.72f, SideZ = 0.58f, SideYaw = 30f;
        /// <summary>Distancia horizontal (m) del jugador al borde de abajo del atril.</summary>
        public const float CenterNear = 0.45f;
        /// <summary>Donde empieza el copete de las alas (titulo y tira de ayuda, sin controles).</summary>
        public const float CrestFrom = 0.93f;
        /// <summary>Altura del ojo usada para medir la legibilidad (m).</summary>
        public const float EyeHeight = 1.6f;

        /// <summary>Paneles: 0 izquierda (sello), 1 centro (atril), 2 derecha (campo y entorno).</summary>
        public const int PanelCount = 3;
        public const int Left = 0, Center = 1, Right = 2;

        // ---------------------------------------------------------------- alas

        /// <summary>Columnas de controles por fila (paso = <see cref="LabPanelLayout.MinControlPitch"/>).</summary>
        public const int Columns = 5;
        public const float ColumnPitch = LabPanelLayout.MinControlPitch;
        /// <summary>Centros (s) de las filas de controles de cada ala, de arriba hacia abajo; cada una con el titulo de su grupo.</summary>
        public static readonly float[] LeftRowS = { 0.50f, 0.295f, 0.09f };
        public static readonly float[] RightRowS = { 0.575f, 0.37f, 0.165f };
        /// <summary>Paso minimo entre filas (m): franja del grupo (titulo arriba, plaquita, control y ventanita de la lectura).</summary>
        public const float RowPitch = 0.2f;
        /// <summary>Altura (sobre el centro de la fila) del titulo del grupo.</summary>
        public const float RowTitleDS = 0.1f;
        /// <summary>Mitad del alto de un control con su rotulo y su lectura (m).</summary>
        public const float ControlHalfHeight = 0.08f;
        /// <summary>Grillas de iconos (sigilos, signos, casillas del campo): columnas y paso de celda (m, colliders contiguos).</summary>
        public const int GridColumns = 5;
        public const float CellPitch = 0.085f;
        /// <summary>Izquierda: grilla de sigilos (2 filas) y de signos (3 filas) lado a lado, y la altura de sus titulos.</summary>
        public const float SigilGridS = 0.805f, SignGridS = 0.7625f, SigilGridX = -0.235f, SignGridX = 0.235f, GridTitleS = 0.908f;
        /// <summary>Derecha: casillas del campo (P y 1..4) a la izquierda y botones a la derecha en dos filas de tres.</summary>
        public const float SlotS = 0.85f, SlotX = -0.235f;
        public static readonly float[] ButtonRowS = { 0.85f, 0.75f };
        public const float ButtonBlockX = 0.232f, ButtonPitch = 0.14f, ButtonWidth = 0.13f, ButtonDepth = 0.09f;

        // ---------------------------------------------------------------- atril (de arriba hacia abajo: lienzo, botones, pantalla)

        /// <summary>Lienzo de dibujo: centro sobre la pendiente y lado del papel (m).</summary>
        public const float PadS = 0.56f, PadSize = 0.3f;
        /// <summary>Fila de botones: LANZAR al medio, Borrar dibujo y Reiniciar sello a los costados.</summary>
        public const float ButtonsS = 0.29f, LaunchWidth = 0.15f, FrontButtonX = 0.14f, FrontButtonWidth = 0.11f, CenterButtonDepth = 0.09f;
        /// <summary>Pantalla «Vas a lanzar» abajo de todo: desde/hasta sobre la pendiente y ancho (m; mas ancha que el atril).</summary>
        public const float ScreenFrom = 0.015f, ScreenTo = 0.215f, ScreenWidth = 0.52f;
        /// <summary>Alto minimo de la letra de la pantalla (m) y distancia maxima de lectura desde el ojo.</summary>
        public const float ScreenTextMin = 0.022f, ScreenReadMax = 0.9f;

        // ---------------------------------------------------------------- geometria

        /// <summary>X de la columna <paramref name="index"/> de una fila de <paramref name="count"/> controles, centradas.</summary>
        public static float RowX(int count, int index) => (index - (count - 1) * 0.5f) * ColumnPitch;

        /// <summary>Centros (s) de las filas de controles de un ala.</summary>
        public static float[] RowS(int panel) => panel == Left ? LeftRowS : RightRowS;

        /// <summary>X de la casilla <paramref name="index"/> del campo (P y 1..4, contiguas).</summary>
        public static float SlotCellX(int index, int count) => SlotX + (index - (count - 1) * 0.5f) * CellPitch;

        /// <summary>X del boton <paramref name="index"/> (0..2) de una fila de botones.</summary>
        public static float ButtonX(int index) => ButtonBlockX + (index - 1) * ButtonPitch;

        /// <summary>Ancho de un panel.</summary>
        public static float PanelWidth(int panel) => panel == Center ? CenterWidth : SideWidth;

        /// <summary>Largo de la losa de un panel sobre la pendiente.</summary>
        public static float PanelLength(int panel) => panel == Center ? CenterLength : SideLength;

        /// <summary>Inclinacion de un panel (°).</summary>
        public static float PanelTilt(int panel) => panel == Center ? CenterTilt : SideTilt;

        /// <summary>Altura (m) del borde de abajo de un panel.</summary>
        public static float PanelBottomOf(int panel) => panel == Center ? CenterBottom : SideBottom;

        /// <summary>Giro (° alrededor de Y, 0 = hacia la pista, + a la derecha) de un panel: las alas miran hacia adentro.</summary>
        public static float PanelYaw(int panel) => panel == Center ? 0f : panel == Left ? -SideYaw : SideYaw;

        /// <summary>Punto del piso (relativo al jugador) bajo el centro del borde de abajo de un panel.</summary>
        public static Vector3 PanelBase(int panel)
        {
            if (panel == Center) return new Vector3(0f, 0f, CenterNear);
            return new Vector3(panel == Left ? -SideX : SideX, 0f, SideZ);
        }

        /// <summary>Punto (x a lo ancho, s sobre la pendiente) de un panel, relativo al jugador (y = altura sobre el piso).</summary>
        public static Vector3 PanelPoint(int panel, float x, float s)
        {
            float t = PanelTilt(panel) * Mathf.Deg2Rad;
            var local = new Vector3(x, PanelBottomOf(panel) + s * Mathf.Sin(t), s * Mathf.Cos(t));
            return PanelBase(panel) + Quaternion.Euler(0f, PanelYaw(panel), 0f) * local;
        }

        /// <summary>Altura (m) del borde de arriba de una losa de <paramref name="panel"/> de largo <paramref name="length"/>.</summary>
        public static float TopHeight(int panel, float length) => PanelBottomOf(panel) + length * Mathf.Sin(PanelTilt(panel) * Mathf.Deg2Rad);

        /// <summary>Alcance maximo (m) que se le exige a un panel.</summary>
        public static float ReachLimit(int panel) => panel == Center ? ReachMax : SideReachMax;

        /// <summary>Distancia horizontal (m) de un punto relativo al jugador.</summary>
        public static float Reach(Vector3 relative) => new Vector2(relative.x, relative.z).magnitude;

        /// <summary>Angulo horizontal (°, 0 = hacia la pista) de un punto relativo al jugador.</summary>
        public static float Bearing(Vector3 relative) => Mathf.Atan2(relative.x, relative.z) * Mathf.Rad2Deg;

        /// <summary>Distancia (m) del ojo (<see cref="EyeHeight"/>) a un punto relativo al jugador.</summary>
        public static float EyeDistance(Vector3 relative) => Vector3.Distance(new Vector3(0f, EyeHeight, 0f), relative);
    }
}
