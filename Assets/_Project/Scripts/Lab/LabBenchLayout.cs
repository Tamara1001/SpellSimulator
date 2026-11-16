using UnityEngine;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Medidas puras del banco compacto del Laboratorio v4 (Docs/LABORATORIO_V4_PLAN.md, punto 1). El jugador se para en
    /// <see cref="StandZ"/> (marco del laboratorio: +Z hacia la pista, +X a su derecha) y todo lo que se toca queda a ≤
    /// <see cref="ReachMax"/> m en horizontal de ese punto:
    /// - adelante (±<see cref="FrontHalfAngle"/>°) una mesa baja con el atril de dibujo, LANZAR y dos botones (no tapa la pista);
    /// - a cada lado dos paneles casi verticales (<see cref="FacetTilt"/>°) que lo rodean en arco: izquierda el sello (adentro
    ///   hechizo y signos, afuera tinta y afinado), derecha los objetos del campo (adentro) y el viento/fuego (afuera).
    /// Los paneles son empinados para apilar filas en altura sin alejarse: profundidad util ≈ 0,24 m.
    /// Marco de un panel (igual que el v3): +X a lo ancho, +Z subiendo la pendiente (s), +Y la normal (hacia el jugador).
    /// </summary>
    public static class LabBenchLayout
    {
        /// <summary>Donde se para el jugador (Z del marco del laboratorio; X = 0).</summary>
        public const float StandZ = -0.2f;
        /// <summary>Distancia horizontal maxima (m) de cualquier control al punto donde se para el jugador.</summary>
        public const float ReachMax = 0.7f;
        /// <summary>Profundidad util maxima (m) de cualquier parte del banco.</summary>
        public const float MaxUsefulDepth = 0.6f;
        /// <summary>Altura de la mesa (m sobre el piso).</summary>
        public const float DeskTop = 0.86f;

        // ---------------------------------------------------------------- paneles laterales
        /// <summary>Distancia horizontal (m) del jugador al borde de abajo de cada panel.</summary>
        public const float FacetNear = 0.44f;
        /// <summary>Inclinacion de los paneles (° desde la horizontal): casi verticales para apilar filas sin alejarse.</summary>
        public const float FacetTilt = 76f;
        /// <summary>Largo de la zona de controles sobre la pendiente (m) y alto del copete de arriba (titulo y ayuda).</summary>
        public const float FacetSlope = 0.98f, CrestSlope = 0.15f;
        /// <summary>Ancho de cada panel (m) y altura de su borde de abajo sobre el piso.</summary>
        public const float FacetWidth = 0.5f, FacetBottom = 0.8f;
        /// <summary>Mitad del angulo que ocupa la mesa de adelante (°).</summary>
        public const float FrontHalfAngle = 36f;

        /// <summary>Paneles: 0 izquierda afuera, 1 izquierda adentro, 2 derecha adentro, 3 derecha afuera.</summary>
        public const int FacetCount = 4;
        public const int LeftOuter = 0, LeftInner = 1, RightInner = 2, RightOuter = 3;

        /// <summary>Centro de cada fila de controles sobre la pendiente (de arriba hacia abajo; paso <see cref="RowPitch"/>).</summary>
        public static readonly float[] RowS = { 0.9f, 0.74f, 0.58f, 0.42f, 0.26f, 0.1f };
        /// <summary>Paso entre filas (m): plaquita del rotulo arriba, control, ventanita de la lectura abajo.</summary>
        public const float RowPitch = 0.16f;
        /// <summary>Columnas de controles (paso = <see cref="LabPanelLayout.MinControlPitch"/>).</summary>
        public static readonly float[] ColumnX = { -LabPanelLayout.MinControlPitch * 0.5f, LabPanelLayout.MinControlPitch * 0.5f };
        /// <summary>Botones de una fila de tres (+ Caja, Borrar…): paso y tamaño (m).</summary>
        public const float ButtonPitch = 0.16f, ButtonWidth = 0.14f, ButtonDepth = 0.1f;
        /// <summary>Grillas de iconos (sigilos, signos, lista de objetos): columnas y paso de celda (m, colliders contiguos).</summary>
        public const int GridColumns = 5;
        public const float CellPitch = 0.085f;
        /// <summary>Margen izquierdo de cada panel para el titulo del grupo (texto acostado a lo largo de la pendiente).</summary>
        public const float SpineX = -FacetWidth * 0.5f + 0.021f;

        // ---------------------------------------------------------------- mesa de adelante
        /// <summary>Mesa de adelante: borde cercano y profundidad (m).</summary>
        public const float FrontNear = FacetNear, FrontDepth = 0.3f;
        /// <summary>Atril de dibujo: centro (m, desde el jugador, altura sobre el piso), lado del papel e inclinacion (° desde la horizontal).</summary>
        public static readonly Vector3 PadCenter = new Vector3(0f, DeskTop + 0.33f, 0.6f);
        public const float PadSize = 0.3f, PadTilt = 55f;
        /// <summary>LANZAR (centro sobre la mesa) y botones a sus costados (Borrar dibujo / Reiniciar sello).</summary>
        public static readonly Vector3 LaunchCenter = new Vector3(0f, DeskTop, 0.5f);
        public const float LaunchWidth = 0.24f, LaunchDepth = 0.12f, FrontButtonX = 0.2f, FrontButtonWidth = 0.12f;
        /// <summary>Ancho de la mesa de adelante (m): termina donde empiezan los paneles de adentro.</summary>
        public const float FrontWidth = 0.58f;

        /// <summary>Mitad del angulo (°) que ocupa un panel visto desde el jugador.</summary>
        public static float FacetHalfAngle => Mathf.Atan2(FacetWidth * 0.5f, FacetNear) * Mathf.Rad2Deg;

        /// <summary>Giro (° alrededor de Y, 0 = hacia la pista, + a la derecha) del panel <paramref name="facet"/>.</summary>
        public static float FacetYaw(int facet)
        {
            float half = FacetHalfAngle;
            float inner = FrontHalfAngle + half, outer = inner + 2f * half;
            switch (facet)
            {
                case LeftOuter: return -outer;
                case LeftInner: return -inner;
                case RightInner: return inner;
                default: return outer;
            }
        }

        /// <summary>Profundidad horizontal (m) de la zona de controles de un panel.</summary>
        public static float FacetDepth => FacetSlope * Mathf.Cos(FacetTilt * Mathf.Deg2Rad);

        /// <summary>Punto (x a lo ancho, s sobre la pendiente) de un panel, relativo al jugador (y = altura sobre el piso).</summary>
        public static Vector3 FacetPoint(int facet, float x, float s)
        {
            float t = FacetTilt * Mathf.Deg2Rad;
            var local = new Vector3(x, FacetBottom + s * Mathf.Sin(t), FacetNear + s * Mathf.Cos(t));
            return Quaternion.Euler(0f, FacetYaw(facet), 0f) * local;
        }

        /// <summary>Distancia horizontal (m) de un punto relativo al jugador.</summary>
        public static float Reach(Vector3 relative) => new Vector2(relative.x, relative.z).magnitude;
    }
}
