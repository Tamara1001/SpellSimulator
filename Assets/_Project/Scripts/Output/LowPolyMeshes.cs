using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Mallas low-poly procedurales para los VFX de tierra: esquirla de roca (particulas en modo malla) y estaca de
    /// piedra (rocas que brotan del suelo). Caras planas (vertices sin compartir) para el look facetado de StylizedLit.
    /// Deterministas por semilla; ambas caben en un cubo unitario centrado (encajan con el BoxCollider del cubo).
    /// </summary>
    public static class LowPolyMeshes
    {
        private const int ShardSides = 5;
        private const int StakeSides = 6;

        /// <summary>Roca facetada (~20 caras): dos anillos irregulares y dos puntas.</summary>
        public static Mesh Shard(int seed)
        {
            var rng = new System.Random(seed);
            var ring = new List<Vector3>();
            int n = ShardSides;
            for (int k = 0; k < 2; k++)
            {
                float y = k == 0 ? -0.18f : 0.16f;
                for (int i = 0; i < n; i++)
                {
                    float a = (i + 0.5f * k + Jitter(rng, 0.2f)) * Mathf.PI * 2f / n;
                    float r = 0.42f + Jitter(rng, 0.08f);
                    ring.Add(new Vector3(Mathf.Cos(a) * r, y + Jitter(rng, 0.06f), Mathf.Sin(a) * r));
                }
            }
            Vector3 top = new Vector3(Jitter(rng, 0.12f), 0.45f, Jitter(rng, 0.12f));
            Vector3 bottom = new Vector3(Jitter(rng, 0.1f), -0.42f, Jitter(rng, 0.1f));

            var tris = new List<Vector3>();
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector3 b0 = ring[i], b1 = ring[j], t0 = ring[n + i], t1 = ring[n + j];
                // Banda lateral (los anillos estan desfasados: dos triangulos por lado).
                Tri(tris, b0, t0, b1);
                Tri(tris, b1, t0, t1);
                Tri(tris, t0, top, t1);
                Tri(tris, b1, bottom, b0);
            }
            return Build("RockShard", tris);
        }

        /// <summary>Estaca de piedra: prisma hexagonal irregular que se afina hacia una punta astillada.</summary>
        public static Mesh Stake(int seed)
        {
            var rng = new System.Random(seed);
            int n = StakeSides;
            var baseRing = new Vector3[n];
            var midRing = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = (i + Jitter(rng, 0.15f)) * Mathf.PI * 2f / n;
                float rb = 0.46f + Jitter(rng, 0.04f);
                float rm = 0.3f + Jitter(rng, 0.05f);
                baseRing[i] = new Vector3(Mathf.Cos(a) * rb, -0.5f, Mathf.Sin(a) * rb);
                midRing[i] = new Vector3(Mathf.Cos(a) * rm, 0.12f + Jitter(rng, 0.08f), Mathf.Sin(a) * rm);
            }
            Vector3 tip = new Vector3(Jitter(rng, 0.1f), 0.5f, Jitter(rng, 0.1f));
            Vector3 center = new Vector3(0f, -0.5f, 0f);

            var tris = new List<Vector3>();
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Tri(tris, baseRing[i], midRing[i], baseRing[j]);
                Tri(tris, baseRing[j], midRing[i], midRing[j]);
                Tri(tris, midRing[i], tip, midRing[j]);
                Tri(tris, baseRing[j], center, baseRing[i]);
            }
            return Build("RockStake", tris, StakeLow, StakeHigh);
        }

        /// <summary>
        /// Cristal facetado (particulas en modo malla): prisma hexagonal alargado con una punta en cada extremo. Sin color por
        /// vertice (blanco): el tinte celeste lo da el gradiente del look.
        /// </summary>
        public static Mesh Crystal(int seed)
        {
            var rng = new System.Random(seed);
            int n = StakeSides;
            var low = new Vector3[n];
            var high = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = (i + Jitter(rng, 0.1f)) * Mathf.PI * 2f / n;
                float r = 0.2f + Jitter(rng, 0.03f);
                low[i] = new Vector3(Mathf.Cos(a) * r, -0.2f + Jitter(rng, 0.04f), Mathf.Sin(a) * r);
                high[i] = new Vector3(Mathf.Cos(a) * r * 0.9f, 0.2f + Jitter(rng, 0.04f), Mathf.Sin(a) * r * 0.9f);
            }
            Vector3 top = new Vector3(Jitter(rng, 0.04f), 0.5f, Jitter(rng, 0.04f));
            Vector3 bottom = new Vector3(Jitter(rng, 0.04f), -0.5f, Jitter(rng, 0.04f));
            var tris = new List<Vector3>();
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Tri(tris, low[i], high[i], low[j]);
                Tri(tris, low[j], high[i], high[j]);
                Tri(tris, high[i], top, high[j]);
                Tri(tris, low[j], bottom, low[i]);
            }
            return Build("CrystalShard", tris);
        }

        /// <summary>Estaca de cristal: prisma hexagonal esbelto que brota del suelo y termina en punta facetada (celeste claro).</summary>
        public static Mesh CrystalStake(int seed)
        {
            var rng = new System.Random(seed);
            int n = StakeSides;
            var baseRing = new Vector3[n];
            var midRing = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = (i + Jitter(rng, 0.08f)) * Mathf.PI * 2f / n;
                float rb = 0.42f + Jitter(rng, 0.03f);
                float rm = 0.38f + Jitter(rng, 0.03f);
                baseRing[i] = new Vector3(Mathf.Cos(a) * rb, -0.5f, Mathf.Sin(a) * rb);
                midRing[i] = new Vector3(Mathf.Cos(a) * rm, 0.18f + Jitter(rng, 0.03f), Mathf.Sin(a) * rm);
            }
            Vector3 tip = new Vector3(Jitter(rng, 0.05f), 0.5f, Jitter(rng, 0.05f));
            Vector3 center = new Vector3(0f, -0.5f, 0f);
            var tris = new List<Vector3>();
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Tri(tris, baseRing[i], midRing[i], baseRing[j]);
                Tri(tris, baseRing[j], midRing[i], midRing[j]);
                Tri(tris, midRing[i], tip, midRing[j]);
                Tri(tris, baseRing[j], center, baseRing[i]);
            }
            return Build("CrystalStake", tris, CrystalLow, CrystalHigh);
        }

        // Cristal: caras de abajo celeste profundo, las que miran arriba casi blancas (borde claro sin textura).
        private static readonly Color CrystalLow = new Color(0.62f, 0.88f, 1f);
        private static readonly Color CrystalHigh = new Color(0.94f, 1f, 1f);

        private static float Jitter(System.Random rng, float amount) => ((float)rng.NextDouble() * 2f - 1f) * amount;

        private static void Tri(List<Vector3> tris, Vector3 a, Vector3 b, Vector3 c)
        {
            tris.Add(a);
            tris.Add(b);
            tris.Add(c);
        }

        // Estaca: caras de abajo marron, las que miran arriba ocre claro (borde claro sin textura).
        private static readonly Color StakeLow = new Color(0.5f, 0.36f, 0.24f);
        private static readonly Color StakeHigh = new Color(0.93f, 0.76f, 0.5f);

        private static Mesh Build(string name, List<Vector3> tris, Color? low = null, Color? high = null)
        {
            var colors = low.HasValue ? new Color[tris.Count] : null;
            var indices = new int[tris.Count];
            var normals = new Vector3[tris.Count];
            var uvs = new Vector2[tris.Count];
            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector3 n = Vector3.Cross(tris[i + 1] - tris[i], tris[i + 2] - tris[i]).normalized;
                // Caras hacia afuera: si la normal mira al centro, invertir el orden.
                Vector3 c = (tris[i] + tris[i + 1] + tris[i + 2]) / 3f;
                if (Vector3.Dot(n, c) < 0f)
                {
                    (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
                    n = -n;
                }
                for (int k = 0; k < 3; k++)
                {
                    indices[i + k] = i + k;
                    normals[i + k] = n;
                    uvs[i + k] = new Vector2(k == 1 ? 1f : 0f, k == 2 ? 1f : 0f);
                }
                if (colors != null)
                {
                    float up = Mathf.Clamp01(n.y * 0.5f + 0.5f);
                    Color face = Color.Lerp(low.Value, high.Value, up * up) * (0.92f + 0.08f * ((i / 3) % 3));
                    face.a = 1f;
                    colors[i] = colors[i + 1] = colors[i + 2] = face;
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(tris);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            if (colors != null) mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
