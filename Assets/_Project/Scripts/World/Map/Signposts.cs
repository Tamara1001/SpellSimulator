using UnityEngine;

namespace SpellSimulator.World.Map
{
    /// <summary>
    /// Postes de señalización del modo Libre (Docs/MAPA_FLUJO_PLAN.md, paso 9 y D31): un poste en el Nexo y otro adentro del
    /// Archivo, junto a la entrada norte, con flechas de madera de UNA palabra pintadas del color de su destino (los colores
    /// son los de Data/WorldZones.json, así el poste y el tablero del mapa hablan el mismo idioma). Solo datos y cuentas:
    /// lo construye MapSignposts en el Editor y lo cubre SignpostsTests.
    /// </summary>
    public static class Signposts
    {
        public readonly struct Arrow
        {
            public readonly string Word;
            public readonly Vector2 Target;
            public readonly Color Color;

            public Arrow(string word, Vector2 target, string html)
            {
                Word = word;
                Target = target;
                Color = ColorUtility.TryParseHtmlString(html, out Color c) ? c : Color.white;
            }
        }

        public readonly struct Post
        {
            public readonly string Id;
            public readonly Vector2 Position;
            public readonly Arrow[] Arrows;

            public Post(string id, Vector2 position, Arrow[] arrows)
            {
                Id = id;
                Position = position;
                Arrows = arrows;
            }
        }

        /// <summary>Campos de agua y tierra: centros reales de sus sitios (MapStructures: WaterLab y EarthWorkshop).</summary>
        public static readonly Vector2 WaterFieldCenter = new Vector2(-22f, -30.3f), EarthFieldCenter = new Vector2(20f, -50f);

        /// <summary>Distancia mínima (m) entre el poste y el eje de cualquier camino: medio ancho del camino + 1 m de paso.</summary>
        public const float PathClearance = ValleyShape.PathWidth * 0.5f + 1f;

        public static readonly Post[] All =
        {
            // Nexo: al noroeste del cruce, fuera de los caminos a la torre, al lago y al Archivo.
            new Post("Nexus", ValleyShape.Spawn + new Vector2(-2.2f, 2.2f), new[]
            {
                new Arrow("Torre", ValleyShape.TowerCenter, "#8E6B4A"),
                new Arrow("Laboratorio", new Vector2(20f, -8f), "#4F7FC4"),
                new Arrow("Entrenamiento", ValleyShape.TrainingOrigin, "#D0563B"),
                new Arrow("Archivo", ValleyShape.ArchiveCenter, "#C9B27A"),
                new Arrow("Lago", ValleyShape.LakeCenter, "#2F7FB0"),
            }),
            // Archivo: adentro de la rotonda, al oeste de la entrada norte (r 6,5), mirando a quien entra.
            new Post("Archive", ValleyShape.ArchiveCenter + new Vector2(-1.8f, 6.3f), new[]
            {
                new Arrow("Agua", WaterFieldCenter, "#3A9BB5"),
                new Arrow("Fuego", ValleyShape.FireFieldCenter, "#C8432B"),
                new Arrow("Tierra", EarthFieldCenter, "#9C6B3C"),
                new Arrow("Viento", ValleyShape.WindFieldCenter, "#6FB7D1"),
                new Arrow("Nexo", ValleyShape.Spawn, "#8DB04A"),
            }),
        };

        /// <summary>Guiñada (grados, 0 = +Z, sentido horario visto desde arriba) de una flecha que sale de <paramref name="from"/> hacia <paramref name="to"/>.</summary>
        public static float Yaw(Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
        }

        /// <summary>Texto claro u oscuro según el brillo del color de la tabla.</summary>
        public static Color TextOn(Color board) =>
            0.299f * board.r + 0.587f * board.g + 0.114f * board.b > 0.55f ? new Color(0.14f, 0.08f, 0.04f) : new Color(0.98f, 0.95f, 0.88f);
    }
}
