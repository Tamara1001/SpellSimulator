using UnityEngine;

namespace SpellSimulator.World.Map
{
    /// <summary>
    /// Forma del mapa del modo Libre (GAME_DESIGN, "Libre"): un valle cerrado por montañas, con la torre sobre una
    /// loma, un lago con cascada, un campo de practica y caminos de tierra. Logica pura y determinista: la usa el
    /// generador del mapa (editor) y sirve en runtime para ubicar cosas sobre el terreno.
    /// Unidades en metros; origen en el centro del valle; Y arriba.
    /// </summary>
    public static class ValleyShape
    {
        // ---------------------------------------------------------------- layout
        public const float PlayableRadius = 68f;
        public const float MountainEnd = 96f;
        public const float HalfSize = 104f;

        public static readonly Vector2 TowerCenter = new Vector2(0f, 16f);
        public const float HillRadius = 20f;
        public const float HillHeight = 4f;
        /// <summary>Meseta plana en lo alto de la loma (base de la torre).</summary>
        public const float HillPlateau = 9f;

        public static readonly Vector2 LakeCenter = new Vector2(-34f, -16f);
        public static readonly Vector2 LakeRadii = new Vector2(16f, 11f);
        public const float LakeDepth = 3.2f;
        /// <summary>Altura de la superficie del lago.</summary>
        public const float WaterLevel = -1.3f;

        /// <summary>Cascada: baja por la montaña desde WaterfallTop hasta WaterfallBase y sigue en arroyo hasta el lago.</summary>
        public static readonly Vector2 WaterfallTop = new Vector2(-79f, -37.5f);
        public static readonly Vector2 WaterfallBase = new Vector2(-57f, -27f);
        public static readonly Vector2 RiverEnd = new Vector2(-46f, -21.5f);
        public const float RiverHalfWidth = 2.2f;

        public static readonly Vector2 FieldCenter = new Vector2(28f, -6f);
        public const float FieldRadius = 11f;

        public static readonly Vector2 Spawn = new Vector2(0f, -6f);

        /// <summary>Caminos de tierra: polilineas en XZ.</summary>
        public static readonly Vector2[][] Paths =
        {
            new[] { new Vector2(0f, -14f), Spawn, new Vector2(0f, 5f) },
            new[] { Spawn, new Vector2(-12f, -10f), new Vector2(-21f, -14f) },
            new[] { Spawn, new Vector2(12f, -7f), new Vector2(18f, -6f) }
        };

        public const float PathWidth = 2.2f;

        // ---------------------------------------------------------------- altura

        public static float Height(float x, float z)
        {
            var p = new Vector2(x, z);
            float h = (Fbm(x * 0.035f, z * 0.035f, 3) - 0.5f) * 1.8f;

            // Loma de la torre: meseta plana arriba y ladera suave.
            float dHill = Vector2.Distance(p, TowerCenter);
            float hill = 1f - Smooth(HillPlateau, HillRadius, dHill);
            h = Mathf.Lerp(h, HillHeight, hill);

            // Campo de practica: plano.
            float field = 1f - Smooth(FieldRadius * 0.7f, FieldRadius, Vector2.Distance(p, FieldCenter));
            h = Mathf.Lerp(h, 0f, field);

            // Lago: cuenca eliptica.
            float lake = LakeFactor(x, z);
            h -= LakeDepth * Smooth(1.15f, 0.45f, lake);

            // Arroyo: cauce desde el pie de la cascada hasta el lago.
            float river = 1f - Smooth(RiverHalfWidth, RiverHalfWidth + 2.5f, SegmentDistance(p, WaterfallBase, RiverEnd));
            h = Mathf.Lerp(h, WaterLevel - 1.2f, river);

            // Montañas: anillo que sube fuerte despues del area jugable.
            float r = p.magnitude;
            float m = Smooth(PlayableRadius - 4f, MountainEnd, r);
            float ridge = 34f + Fbm(x * 0.02f + 11f, z * 0.02f - 7f, 4) * 26f;
            h += m * m * ridge;
            return h;
        }

        /// <summary>Distancia normalizada al centro del lago (1 = orilla).</summary>
        public static float LakeFactor(float x, float z)
        {
            Vector2 d = new Vector2(x, z) - LakeCenter;
            return Mathf.Sqrt((d.x * d.x) / (LakeRadii.x * LakeRadii.x) + (d.y * d.y) / (LakeRadii.y * LakeRadii.y));
        }

        /// <summary>Distancia (m) al cauce del arroyo y a la cascada.</summary>
        public static float RiverDistance(float x, float z)
        {
            var p = new Vector2(x, z);
            return Mathf.Min(SegmentDistance(p, WaterfallBase, RiverEnd), SegmentDistance(p, WaterfallTop, WaterfallBase));
        }

        /// <summary>Distancia (m) al camino mas cercano.</summary>
        public static float PathDistance(float x, float z)
        {
            var p = new Vector2(x, z);
            float best = float.MaxValue;
            foreach (var path in Paths)
                for (int i = 0; i + 1 < path.Length; i++)
                    best = Mathf.Min(best, SegmentDistance(p, path[i], path[i + 1]));
            return best;
        }

        // ---------------------------------------------------------------- color

        public static readonly Color Grass = new Color(0.36f, 0.62f, 0.24f);
        public static readonly Color GrassDark = new Color(0.26f, 0.5f, 0.2f);
        public static readonly Color Dirt = new Color(0.6f, 0.46f, 0.3f);
        public static readonly Color Sand = new Color(0.82f, 0.74f, 0.52f);
        public static readonly Color Rock = new Color(0.74f, 0.6f, 0.52f);
        public static readonly Color RockDark = new Color(0.55f, 0.46f, 0.42f);

        /// <summary>Color del suelo segun pendiente, altura, lago y caminos (estilo low-poly, por triangulo).</summary>
        public static Color GroundColor(float x, float z, float height, float slope)
        {
            float tone = Fbm(x * 0.08f, z * 0.08f, 2);
            Color c = Color.Lerp(GrassDark, Grass, tone);

            float lake = LakeFactor(x, z);
            if (lake < 1.25f) c = Color.Lerp(Sand, c, Smooth(1.0f, 1.25f, lake));
            float bank = SegmentDistance(new Vector2(x, z), WaterfallBase, RiverEnd);
            if (bank < RiverHalfWidth + 3f) c = Color.Lerp(Sand, c, Smooth(RiverHalfWidth, RiverHalfWidth + 3f, bank));

            float path = PathDistance(x, z);
            if (path < PathWidth) c = Color.Lerp(Dirt, c, Smooth(PathWidth * 0.5f, PathWidth, path));

            // Roca: pendiente fuerte o montaña alta.
            float rock = Mathf.Max(Smooth(0.45f, 0.7f, slope), Smooth(12f, 22f, height));
            Color stone = Color.Lerp(RockDark, Rock, tone);
            return Color.Lerp(c, stone, rock);
        }

        /// <summary>Se pueden poner arboles aca (lejos de la torre, el lago, el campo, los caminos y la montaña).</summary>
        public static bool CanPlantTree(float x, float z)
        {
            var p = new Vector2(x, z);
            if (p.magnitude > MountainEnd - 18f) return false;
            if (Vector2.Distance(p, TowerCenter) < HillRadius - 2f) return false;
            if (LakeFactor(x, z) < 1.3f) return false;
            if (RiverDistance(x, z) < RiverHalfWidth + 4f) return false;
            if (Vector2.Distance(p, FieldCenter) < FieldRadius + 2f) return false;
            if (Vector2.Distance(p, Spawn) < 8f) return false;
            return PathDistance(x, z) > PathWidth + 2f;
        }

        // ---------------------------------------------------------------- utilidades

        public static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Ruido fractal 0..1 (Perlin de Unity, determinista).</summary>
        public static float Fbm(float x, float z, int octaves)
        {
            float sum = 0f, amplitude = 0.5f, frequency = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(x * frequency + 37.1f * i, z * frequency - 13.7f * i) * amplitude;
                norm += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }
            return sum / norm;
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
