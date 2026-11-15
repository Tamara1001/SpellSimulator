using UnityEngine;

namespace SpellSimulator.World.Map
{
    /// <summary>
    /// Proyección del valle al cuadro del mapa de la torre (Docs/LABORATORIO_V3_PLAN.md (e)) y medidas del cuadro. Lógica pura:
    /// la usan el generador del Editor (MapTowerMap), <see cref="TowerMap"/> y los tests.
    /// Marco del cuadro: +X a la derecha de quien lo mira, +Y arriba, −Z hacia la habitación; z = 0 es la cara del muro.
    /// En el dibujo, arriba es el norte (+Z del mundo) y a la derecha el este (+X del mundo).
    /// </summary>
    public static class MapProjection
    {
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
