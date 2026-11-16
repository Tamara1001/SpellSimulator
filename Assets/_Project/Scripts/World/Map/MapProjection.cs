using UnityEngine;

namespace SpellSimulator.World.Map
{
    /// <summary>
    /// Proyección del valle al cuadro del mapa de la torre (Docs/LABORATORIO_V3_PLAN.md (e)) y medidas del cuadro y de la
    /// leyenda. Lógica pura: la usan el generador del Editor (MapTowerMap),
    /// <see cref="TowerMap"/> y los tests.
    /// Marco del cuadro: +X a la derecha de quien lo mira, +Y arriba, −Z hacia la habitación; z = 0 es la cara del muro.
    /// En el dibujo, arriba es el norte (+Z del mundo) y a la derecha el este (+X del mundo).
    /// </summary>
    public static class MapProjection
    {
        /// <summary>Medidas exteriores del marco (m). Entra en la cara del segmento del muro (1,53 m) sin pisar la esquina.</summary>
        public const float BoardWidth = 1.5f, BoardHeight = 1.14f;

        /// <summary>Ancho del listón del marco (m): el pergamino va de −(alto/2 − listón) a +(alto/2 − listón).</summary>
        public const float FrameBar = 0.055f;

        /// <summary>Centro del valle que cae en el centro del área del mapa (m, XZ del mundo).</summary>
        public static readonly Vector2 WorldCenter = new Vector2(5f, 3f);

        /// <summary>
        /// Lado (m del mundo) del cuadrado de valle que entra en el área del mapa: de x −55 a 65 y de z −57 a 63 (entran la
        /// cueva y la torre de vigía al norte y el entrenamiento al sur).
        /// </summary>
        public const float WorldSpan = 120f;

        /// <summary>Centro (m, marco del cuadro) del área cuadrada del mapa; a su derecha queda la columna de texto.</summary>
        public static readonly Vector2 TableCenter = new Vector2(-0.195f, 0f);

        /// <summary>Lado (m) del área cuadrada del mapa.</summary>
        public const float TableSide = 0.96f;

        /// <summary>Metros del cuadro por metro del mundo.</summary>
        public const float Scale = TableSide / WorldSpan;

        /// <summary>Z (marco del cuadro) de la cara del pergamino.</summary>
        public const float FaceZ = -0.07f;

        /// <summary>Lo más alto de la pintura del mapa sobre el pergamino (capas cada 2 mm: valle, loma, caminos, lago).</summary>
        public const float PaintTop = 0.010f;

        /// <summary>Radio y alto (m) de los marcadores de zona.</summary>
        public const float MarkerRadius = 0.022f, MarkerDepth = 0.016f;

        /// <summary>Separación del texto sobre la superficie que tiene debajo (≥ 6 mm: nada coplanar, sin ZTest Always).</summary>
        public const float TextLift = 0.006f;

        // ---------------------------------------------------------------- portales

        /// <summary>Medio ancho (m, del centro a la punta) del rombo de un portal en el mapa; mismo alto que los marcadores.</summary>
        public const float PortalRadius = 0.018f;

        /// <summary>
        /// Corrimiento (m, marco del cuadro) del rombo de un portal respecto del lugar de su estructura: arriba a la derecha,
        /// para no tapar el marcador numerado de la zona (torre, torre de vigía, cueva y círculo tienen los dos).
        /// </summary>
        public static readonly Vector2 PortalSymbolOffset = new Vector2(0.033f, 0.033f);

        /// <summary>Punto del cuadro donde va el rombo de un portal cuya estructura está en (x, z) del mundo.</summary>
        public static Vector2 PortalToTable(float x, float z) => WorldToTable(x, z) + PortalSymbolOffset;

        // ---------------------------------------------------------------- columna y leyenda

        /// <summary>Columna de texto a la derecha del mapa (centro X y ancho, marco del cuadro).</summary>
        public const float ColumnX = 0.4975f, ColumnWidth = 0.36f;

        /// <summary>Borde de arriba de la primera fila de la leyenda y borde de abajo de la última (m, marco del cuadro).</summary>
        public const float LegendTop = 0.235f, LegendBottom = -0.505f;

        /// <summary>Alto máximo de una fila de la leyenda (m); con muchas filas se achica para que entren todas.</summary>
        public const float LegendMaxRowHeight = 0.0385f;

        /// <summary>Radio (m) de la chapa de color de cada fila (con su número; los portales llevan un rombo).</summary>
        public const float LegendChipRadius = 0.0145f;

        /// <summary>Alto (m) de la barra de resaltado sobre el pergamino y de la chapa de color sobre el pergamino.</summary>
        public const float LegendBarDepth = 0.002f, LegendChipDepth = 0.004f;

        /// <summary>Filas de la leyenda: una por zona, un título «Portales» y una por portal (sin portales, sin título).</summary>
        public static int LegendRows(int zones, int portals) => zones + (portals > 0 ? portals + 1 : 0);

        /// <summary>Alto de cada fila para que entren <paramref name="rows"/> filas entre LegendTop y LegendBottom.</summary>
        public static float LegendRowHeight(int rows) =>
            rows <= 0 ? LegendMaxRowHeight : Mathf.Min(LegendMaxRowHeight, (LegendTop - LegendBottom) / rows);

        /// <summary>Fila (0 = arriba) de la entrada: zonas primero; los portales van después del título «Portales».</summary>
        public static int LegendRowOf(int entry, int zones) => entry < zones ? entry : entry + 1;

        /// <summary>Centro Y (marco del cuadro) de la fila.</summary>
        public static float LegendRowCenterY(int row, float rowHeight) => LegendTop - rowHeight * (row + 0.5f);

        /// <summary>Centro X de la chapa de color de cada fila (borde izquierdo de la columna + margen).</summary>
        public static float LegendChipX => ColumnX - ColumnWidth * 0.5f + 0.02f;

        /// <summary>Borde izquierdo del nombre de cada fila (a la derecha de la chapa).</summary>
        public static float LegendNameLeft => LegendChipX + LegendChipRadius + 0.012f;

        // ---------------------------------------------------------------- proyección

        /// <summary>Proyección general: (world − center) · scale, en el plano del cuadro.</summary>
        public static Vector2 WorldToPlane(Vector2 world, Vector2 center, float scale) => (world - center) * scale;

        /// <summary>Punto del mundo (x, z) en coordenadas del cuadro (m, +X derecha, +Y arriba).</summary>
        public static Vector2 WorldToTable(float x, float z) => TableCenter + WorldToPlane(new Vector2(x, z), WorldCenter, Scale);

        /// <summary>Inversa de <see cref="WorldToTable"/>.</summary>
        public static Vector2 TableToWorld(Vector2 table) => WorldCenter + (table - TableCenter) / Scale;

        /// <summary>True si el punto del cuadro queda dentro del área del mapa, con <paramref name="margin"/> libre al borde.</summary>
        public static bool InsideTable(Vector2 table, float margin = 0f)
        {
            float half = TableSide * 0.5f - margin;
            return Mathf.Abs(table.x - TableCenter.x) <= half && Mathf.Abs(table.y - TableCenter.y) <= half;
        }

        /// <summary>Lleva un punto del cuadro al borde del área del mapa si se sale (con margen).</summary>
        public static Vector2 ClampToTable(Vector2 table, float margin = 0f)
        {
            float half = TableSide * 0.5f - margin;
            return new Vector2(
                Mathf.Clamp(table.x, TableCenter.x - half, TableCenter.x + half),
                Mathf.Clamp(table.y, TableCenter.y - half, TableCenter.y + half));
        }
    }
}
