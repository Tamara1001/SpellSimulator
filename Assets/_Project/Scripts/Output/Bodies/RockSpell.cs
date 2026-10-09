using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Compiler;
using SpellSimulator.Core;
using SpellSimulator.World;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Hechizo de piedra (tierra, no cristal): crea UNA roca fisica delante del papel. Se forma flotando (0 a 1 de escala, con
    /// temblor), despues cae. Se puede agarrar y arrojar (Movable), sirve de muro, proyectil y escalon, y desaparece a los
    /// 120 s. Maximo 10 vivas: al pasarse, la mas vieja se achica. Ver PLAN_OCTAVA_TANDA seccion 3.
    /// </summary>
    public static class RockSpell
    {
        private const int Variants = 4;
        private const float SpawnGap = 0.1f;

        private static readonly Mesh[] meshes = new Mesh[Variants];
        private static readonly List<ExpiringBody> rocks = new List<ExpiringBody>();
        private static readonly List<int> rockOwners = new List<int>(); // paralela a rocks
        private static readonly Color Low = new Color(0.36f, 0.31f, 0.27f), High = new Color(0.68f, 0.62f, 0.54f);

        public static void Spawn(SpellIR spell, SealFrame frame, ElementLook look, DrawingCanvas source, GameObject spellGo)
        {
            // Dueno: el del lienzo emisor (el de su ultimo trazo) si lo hay; 0 = jugador local.
            int ownerId = 0;
            if (source != null && source.Session != null && source.Session.Strokes.Count > 0)
                ownerId = source.Session.Strokes[source.Session.Strokes.Count - 1].OwnerId;
            Spawn(spell, frame, look, source, spellGo, ownerId);
        }

        public static void Spawn(SpellIR spell, SealFrame frame, ElementLook look, DrawingCanvas source, GameObject spellGo, int ownerId)
        {
            float radius = RockMath.Radius(frame.Radius, spell.inkPower);
            float formSeconds = RockMath.FormSeconds(radius);
            float mass = RockMath.Mass(radius);
            int variant = Random.Range(0, Variants);

            var go = new GameObject("Roca");
            go.transform.SetPositionAndRotation(frame.Origin + frame.Normal * (radius + SpawnGap), Random.rotation);
            go.transform.localScale = Vector3.zero;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = MeshFor(variant);
            var meshRenderer = go.AddComponent<MeshRenderer>();
            if (look != null && look.material != null) meshRenderer.sharedMaterial = look.material;

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // Una roca pesada que nace solapada con el jugador lo corre despacio, no lo catapulta.
            rb.maxDepenetrationVelocity = 2f;

            var col = go.AddComponent<MeshCollider>();
            col.convex = true;
            col.sharedMesh = filter.sharedMesh;
            col.enabled = false; // mientras se forma no estorba ni se puede agarrar

            var movable = go.AddComponent<Movable>();
            movable.SetName("roca");

            var expiring = go.AddComponent<ExpiringBody>();
            expiring.Init(RockMath.LifeSeconds, RockMath.ShrinkSeconds);

            go.AddComponent<RockForming>().Init(rb, col, radius, formSeconds);

            Register(expiring, ownerId);
            Debug.Log($"[Roca] radio {radius:F2} m masa {mass:F0} kg formacion {formSeconds:F1} s tinta x{spell.inkPower:F2}");
        }

        private static void Register(ExpiringBody body, int ownerId)
        {
            for (int i = rocks.Count - 1; i >= 0; i--)
            {
                if (rocks[i] != null) continue;
                rocks.RemoveAt(i);
                rockOwners.RemoveAt(i);
            }
            rocks.Add(body);
            rockOwners.Add(ownerId);

            // Limites: 10 totales y, con varios jugadores, 4 por duenio.
            bool limitPerOwner = PlayerRegistry.Count > 1;
            int aliveTotal = 0, aliveOwner = 0;
            for (int i = 0; i < rocks.Count; i++)
            {
                if (rocks[i].IsExpiring) continue;
                aliveTotal++;
                if (rockOwners[i] == ownerId) aliveOwner++;
            }
            // Primero el tope por duenio (solo rocas suyas) y despues el total (la mas vieja de cualquiera).
            for (int i = 0; i < rocks.Count && limitPerOwner && aliveOwner > RockMath.MaxRocksPerOwner; i++)
            {
                if (rocks[i].IsExpiring || rockOwners[i] != ownerId) continue;
                rocks[i].BeginExpire();
                aliveOwner--;
                aliveTotal--;
            }
            for (int i = 0; i < rocks.Count && RockMath.OverLimit(aliveTotal, aliveOwner, limitPerOwner); i++)
            {
                if (rocks[i].IsExpiring) continue;
                rocks[i].BeginExpire();
                aliveTotal--;
            }
        }

        private static Mesh MeshFor(int variant)
        {
            if (meshes[variant] == null) meshes[variant] = BuildRock(1000 + variant * 7919);
            return meshes[variant];
        }

        // ---------------------------------------------------------------- malla: icosaedro subdividido y deformado

        /// <summary>Roca de radio ~1 con caras planas (~80 triangulos, apta para collider convexo). Determinista por semilla.</summary>
        private static Mesh BuildRock(int seed)
        {
            var rng = new System.Random(seed);
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var verts = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
            int[] faces =
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
            };

            // Una subdivision: cada triangulo en 4, con los puntos medios compartidos por arista.
            var mid = new Dictionary<long, int>();
            var tri = new List<int>();
            for (int i = 0; i < faces.Length; i += 3)
            {
                int a = faces[i], b = faces[i + 1], c = faces[i + 2];
                int ab = Midpoint(verts, mid, a, b), bc = Midpoint(verts, mid, b, c), ca = Midpoint(verts, mid, c, a);
                tri.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }

            // Radio por vertice (compartido: la malla queda cerrada) y achatado leve.
            var squash = new Vector3(0.9f + (float)rng.NextDouble() * 0.2f, 0.78f + (float)rng.NextDouble() * 0.2f, 0.9f + (float)rng.NextDouble() * 0.2f);
            for (int i = 0; i < verts.Count; i++)
            {
                float r = 0.82f + (float)rng.NextDouble() * 0.28f;
                Vector3 v = verts[i] * r;
                verts[i] = new Vector3(v.x * squash.x, v.y * squash.y, v.z * squash.z);
            }

            int n = tri.Count;
            var pos = new Vector3[n];
            var normals = new Vector3[n];
            var colors = new Color[n];
            var uvs = new Vector2[n];
            var idx = new int[n];
            for (int i = 0; i < n; i += 3)
            {
                Vector3 a = verts[tri[i]], b = verts[tri[i + 1]], c = verts[tri[i + 2]];
                Vector3 nrm = Vector3.Cross(b - a, c - a).normalized;
                if (Vector3.Dot(nrm, (a + b + c) / 3f) < 0f) { (b, c) = (c, b); nrm = -nrm; }
                float up = Mathf.Clamp01(nrm.y * 0.5f + 0.5f);
                Color face = Color.Lerp(Low, High, up * up) * (0.9f + 0.1f * (i / 3 % 3));
                face.a = 1f;
                pos[i] = a; pos[i + 1] = b; pos[i + 2] = c;
                for (int k = 0; k < 3; k++)
                {
                    normals[i + k] = nrm;
                    colors[i + k] = face;
                    uvs[i + k] = new Vector2(k == 1 ? 1f : 0f, k == 2 ? 1f : 0f);
                    idx[i + k] = i + k;
                }
            }
            var mesh = new Mesh { name = "RockBody" };
            mesh.vertices = pos;
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.uv = uvs;
            mesh.triangles = idx;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static int Midpoint(List<Vector3> verts, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out int m)) return m;
            verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
            m = verts.Count - 1;
            cache[key] = m;
            return m;
        }
    }
}
