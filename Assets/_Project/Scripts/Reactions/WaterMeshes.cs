using UnityEngine;

namespace SpellSimulator.Reactions
{
    /// <summary>
    /// Mallas y materiales compartidos del agua en el mundo (charcos, superficies de zonas y recipientes, hielo).
    /// Se crean una sola vez (perezosos) y se reutilizan; color por vertice (estilo low-poly del proyecto).
    /// - Disco: abanico de 12 lados en XZ, radio 1, UV mapeada al circulo (el shader SpellParticle lo dibuja como gota con borde).
    /// - Placa de hielo: prisma octogonal, radio 1 y alto 1 (base en y = -1, cara de arriba en y = 0), tonos celestes por cara.
    /// </summary>
    public static class WaterMeshes
    {
        private const int DiscSides = 12;
        private const int IceSides = 8;

        public static readonly Color WaterColor = new Color(0.45f, 0.78f, 0.97f, 0.8f);
        private static readonly Color IceTop = new Color(0.86f, 0.96f, 1f, 0.92f);
        private static readonly Color IceSide = new Color(0.6f, 0.82f, 0.95f, 0.92f);
        private static readonly Color IceSideDark = new Color(0.5f, 0.72f, 0.88f, 0.92f);

        private static Mesh disc, ice;
        private static Material iceMaterial, fallbackWater;
        private static PhysicsMaterial icePhysics;

        /// <summary>Disco de agua (radio 1, plano XZ, normal arriba).</summary>
        public static Mesh Disc
        {
            get
            {
                if (disc != null) return disc;
                var vertices = new Vector3[DiscSides + 1];
                var uvs = new Vector2[DiscSides + 1];
                var colors = new Color[DiscSides + 1];
                var triangles = new int[DiscSides * 3];
                vertices[0] = Vector3.zero;
                uvs[0] = new Vector2(0.5f, 0.5f);
                colors[0] = WaterColor;
                for (int i = 0; i < DiscSides; i++)
                {
                    float a = i * Mathf.PI * 2f / DiscSides;
                    float x = Mathf.Cos(a), z = Mathf.Sin(a);
                    vertices[i + 1] = new Vector3(x, 0f, z);
                    // 0.97: el borde del poligono cae justo sobre el filo claro del shader (gota).
                    uvs[i + 1] = new Vector2(0.5f + 0.485f * x, 0.5f + 0.485f * z);
                    colors[i + 1] = WaterColor;
                    triangles[i * 3] = 0;
                    triangles[i * 3 + 1] = 1 + (i + 1) % DiscSides;
                    triangles[i * 3 + 2] = 1 + i;
                }
                disc = new Mesh { name = "Agua_Disco (runtime)", hideFlags = HideFlags.DontSave };
                disc.vertices = vertices;
                disc.uv = uvs;
                disc.colors = colors;
                disc.triangles = triangles;
                disc.RecalculateNormals();
                disc.RecalculateBounds();
                return disc;
            }
        }

        /// <summary>Placa de hielo: prisma octogonal (radio 1, alto 1, cara superior en y = 0).</summary>
        public static Mesh Ice
        {
            get
            {
                if (ice != null) return ice;
                // Caras planas: vertices propios por cara (tapa + 8 lados de 4 vertices).
                int topCount = IceSides + 1;
                var vertices = new Vector3[topCount + IceSides * 4];
                var colors = new Color[vertices.Length];
                var triangles = new int[IceSides * 3 + IceSides * 6];
                vertices[0] = Vector3.zero;
                colors[0] = IceTop;
                for (int i = 0; i < IceSides; i++)
                {
                    float a = (i + 0.5f) * Mathf.PI * 2f / IceSides;
                    vertices[i + 1] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    colors[i + 1] = IceTop;
                    triangles[i * 3] = 0;
                    triangles[i * 3 + 1] = 1 + (i + 1) % IceSides;
                    triangles[i * 3 + 2] = 1 + i;
                }
                int t = IceSides * 3;
                for (int i = 0; i < IceSides; i++)
                {
                    Vector3 p0 = vertices[1 + i], p1 = vertices[1 + (i + 1) % IceSides];
                    int v = topCount + i * 4;
                    vertices[v] = p0;
                    vertices[v + 1] = p1;
                    vertices[v + 2] = p1 + Vector3.down;
                    vertices[v + 3] = p0 + Vector3.down;
                    Color c = (i % 2 == 0) ? IceSide : IceSideDark;
                    colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = c;
                    triangles[t++] = v; triangles[t++] = v + 1; triangles[t++] = v + 2;
                    triangles[t++] = v; triangles[t++] = v + 2; triangles[t++] = v + 3;
                }
                ice = new Mesh { name = "Hielo_Placa (runtime)", hideFlags = HideFlags.DontSave };
                ice.vertices = vertices;
                ice.colors = colors;
                ice.triangles = triangles;
                ice.RecalculateNormals();
                ice.RecalculateBounds();
                return ice;
            }
        }

        /// <summary>Material del hielo (color por vertice, translucido). Sprites/Default siempre esta en el build.</summary>
        public static Material IceMaterial
        {
            get
            {
                if (iceMaterial == null)
                    iceMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Hielo (runtime)", color = Color.white, hideFlags = HideFlags.DontSave };
                return iceMaterial;
            }
        }

        /// <summary>Material de agua si no hay look de agua a mano (disco celeste plano).</summary>
        public static Material FallbackWater
        {
            get
            {
                if (fallbackWater == null)
                    fallbackWater = new Material(Shader.Find("Sprites/Default")) { name = "Agua (runtime)", color = Color.white, hideFlags = HideFlags.DontSave };
                return fallbackWater;
            }
        }

        /// <summary>Hielo resbaladizo: casi sin friccion (gana la menor de las dos superficies).</summary>
        public static PhysicsMaterial IcePhysics
        {
            get
            {
                if (icePhysics == null)
                    icePhysics = new PhysicsMaterial("Hielo")
                    {
                        dynamicFriction = 0.03f,
                        staticFriction = 0.03f,
                        bounciness = 0f,
                        frictionCombine = PhysicsMaterialCombine.Minimum,
                        bounceCombine = PhysicsMaterialCombine.Minimum,
                        hideFlags = HideFlags.DontSave,
                    };
                return icePhysics;
            }
        }

        /// <summary>Crea un hijo con el disco de agua (radio 1; se escala desde afuera).</summary>
        public static MeshRenderer CreateDisc(string name, Transform parent, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Disc;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material != null ? material : FallbackWater;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        /// <summary>Crea un hijo con la placa de hielo (radio 1, alto 1; se escala desde afuera).</summary>
        public static MeshRenderer CreateIce(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Ice;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = IceMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }
    }
}
