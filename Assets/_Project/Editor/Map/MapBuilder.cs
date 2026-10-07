using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Reactions;
using SpellSimulator.World.Map;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>
    /// Genera el mapa del modo Libre en la escena Pradera (M5): terreno low-poly con montañas, lago, pinos, rocas,
    /// la torre (biblioteca, estudio, terraza con rampas en espiral), luz y niebla; y reubica los objetos de juego
    /// (jugador, papel, herramientas, blancos, muñecos). Determinista: regenerar da el mismo mapa.
    /// Menu: SpellSimulator > Map > Build Pradera. Las mallas se guardan en Art/Generated.
    /// </summary>
    public static class MapBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Pradera.unity";
        public const string LabScenePath = "Assets/_Project/Scenes/Escena.unity";
        public const string GeneratedFolder = "Assets/_Project/Art/Generated";
        private const int Seed = 1234;

        private static Material lit;
        private static System.Random rng;

        [MenuItem("SpellSimulator/Map/Build Pradera")]
        public static void BuildFromMenu() => Build();

        public static string Build()
        {
            if (!File.Exists(ScenePath)) AssetDatabase.CopyAsset(LabScenePath, ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(GeneratedFolder);
            rng = new System.Random(Seed);
            lit = StylizedMaterial();

            var old = GameObject.Find("Map");
            if (old != null) Object.DestroyImmediate(old);
            var map = new GameObject("Map").transform;

            int tris = 0;
            tris += BuildTerrain(map);
            BuildLake(map);
            tris += BuildTrees(map);
            tris += BuildRocks(map);
            tris += TowerBuilder.Build(map, lit, TowerOrigin());
            var output = Object.FindFirstObjectByType<Output.SpellOutputController>();
            MapDetails.BuildWaterfall(map, output);
            tris += MapDetails.BuildGrass(map, lit, rng);
            MapDetails.TowerAmbience(GameObject.Find("Tower").transform, lit, output);
            MapDetails.LibraryBooks(GameObject.Find("Tower").transform, lit);
            SetupLighting();
            RelocateGameplay();
            MapDetails.Papers(TowerOrigin(), lit, map);
            Vector3 stampTable = Ground(ValleyShape.FieldCenter.x + 2f, ValleyShape.FieldCenter.y - 10.5f);
            MapDetails.Stamps(map, lit, stampTable, TowerOrigin() + new Vector3(0.5f, 0.9f, 0.3f));
            MapContraptions.Build(map, lit, stampTable + new Vector3(-5f, 0f, 0f));
            MapDetails.PostProcess();
            ToolModels.Apply(lit);
            TowerProps.Build(map, lit, TowerOrigin());
            CanvasDesigns.Build(map, lit, TowerOrigin(), stampTable);
            InteractionSetup.Apply();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return $"Pradera generada: {tris} triangulos.";
        }

        public static Vector3 Ground(float x, float z) => new Vector3(x, ValleyShape.Height(x, z), z);

        public static Vector3 TowerOrigin() => Ground(ValleyShape.TowerCenter.x, ValleyShape.TowerCenter.y);

        public static float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);

        // ---------------------------------------------------------------- materiales

        private static Material StylizedMaterial()
        {
            const string path = "Assets/_Project/Materials/Stylized.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("SpellSimulator/StylizedLit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.enableInstancing = true;
            m.SetColor("_ShadowTint", new Color(0.8f, 0.78f, 0.9f));
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Mesh SaveMesh(Mesh mesh)
        {
            string path = $"{GeneratedFolder}/{mesh.name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        public static GameObject Place(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Quaternion rotation, float scale = 1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        // ---------------------------------------------------------------- terreno

        private static int BuildTerrain(Transform map)
        {
            const float cell = 2.5f;
            float half = ValleyShape.HalfSize;
            int n = Mathf.CeilToInt(half * 2f / cell);
            var b = new LowPolyMesh();
            for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                float x0 = -half + i * cell, x1 = x0 + cell, z0 = -half + j * cell, z1 = z0 + cell;
                Vector3 a = Ground(x0, z0), bb = Ground(x0, z1), c = Ground(x1, z1), d = Ground(x1, z0);
                // Diagonal alternada: menos patron visible.
                if (((i + j) & 1) == 0)
                {
                    Tri(b, a, bb, c);
                    Tri(b, a, c, d);
                }
                else
                {
                    Tri(b, a, bb, d);
                    Tri(b, bb, c, d);
                }
            }
            var mesh = SaveMesh(b.Build("Terrain"));
            var go = Place("Terrain", map, mesh, lit, Vector3.zero, Quaternion.identity);
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.AddComponent<GroundSurface>();
            go.isStatic = true;
            return b.TriangleCount;
        }

        private static void Tri(LowPolyMesh b, Vector3 a, Vector3 c1, Vector3 c2)
        {
            Vector3 n = Vector3.Cross(c1 - a, c2 - a).normalized;
            Vector3 center = (a + c1 + c2) / 3f;
            float slope = Mathf.Sqrt(Mathf.Max(0f, 1f - n.y * n.y)) / Mathf.Max(n.y, 0.05f);
            b.Tri(a, c1, c2, ValleyShape.GroundColor(center.x, center.z, center.y, slope));
        }

        // ---------------------------------------------------------------- lago

        private static void BuildLake(Transform map)
        {
            var root = new GameObject("Lake").transform;
            root.SetParent(map, false);
            Vector3 center = new Vector3(ValleyShape.LakeCenter.x, ValleyShape.WaterLevel, ValleyShape.LakeCenter.y);
            root.position = center;

            // Superficie: elipse un poco mas grande que la orilla (el terreno la tapa en los bordes).
            var surface = new GameObject("WaterSurface");
            surface.transform.SetParent(root, false);
            var mesh = new Mesh { name = "LakeSurface" };
            const int rings = 8, sides = 40;
            var verts = new System.Collections.Generic.List<Vector3> { Vector3.zero };
            var tris = new System.Collections.Generic.List<int>();
            for (int r = 1; r <= rings; r++)
                for (int s = 0; s < sides; s++)
                {
                    float a = s * Mathf.PI * 2f / sides, k = r / (float)rings * 1.35f;
                    verts.Add(new Vector3(Mathf.Cos(a) * ValleyShape.LakeRadii.x * k, 0f, Mathf.Sin(a) * ValleyShape.LakeRadii.y * k));
                }
            for (int s = 0; s < sides; s++) { tris.Add(0); tris.Add(1 + (s + 1) % sides); tris.Add(1 + s); }
            for (int r = 1; r < rings; r++)
                for (int s = 0; s < sides; s++)
                {
                    int a0 = 1 + (r - 1) * sides + s, a1 = 1 + (r - 1) * sides + (s + 1) % sides;
                    int b0 = a0 + sides, b1 = a1 + sides;
                    tris.Add(a0); tris.Add(a1); tris.Add(b1);
                    tris.Add(a0); tris.Add(b1); tris.Add(b0);
                }
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh = SaveMesh(mesh);
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r0 = surface.AddComponent<MeshRenderer>();
            r0.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Water.mat");
            r0.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var volume = new GameObject("WaterVolume");
            volume.transform.SetParent(root, false);
            var box = volume.AddComponent<BoxCollider>();
            box.isTrigger = true;
            float depth = ValleyShape.LakeDepth + 1f;
            box.center = new Vector3(0f, -depth / 2f, 0f);
            box.size = new Vector3(ValleyShape.LakeRadii.x * 2.2f, depth, ValleyShape.LakeRadii.y * 2.2f);
            var water = volume.AddComponent<WaterVolume>();
            var so = new SerializedObject(water);
            so.FindProperty("surface").objectReferenceValue = r0;
            so.FindProperty("output").objectReferenceValue = Object.FindFirstObjectByType<Output.SpellOutputController>();
            so.ApplyModifiedProperties();
        }

        // ---------------------------------------------------------------- pinos

        private static Mesh PineMesh(int variant)
        {
            var b = new LowPolyMesh();
            var trunk = new Color(0.42f, 0.27f, 0.18f);
            Color[] greens = { new Color(0.16f, 0.42f, 0.24f), new Color(0.2f, 0.5f, 0.27f), new Color(0.13f, 0.36f, 0.22f) };
            b.Frustum(Matrix4x4.identity, 0.28f, 0.2f, 0f, 2.6f, 6, trunk);
            int tiers = 3 + variant % 2;
            float y = 1.6f, radius = 2.2f + 0.2f * variant;
            for (int t = 0; t < tiers; t++)
            {
                var m = Matrix4x4.Rotate(Quaternion.Euler(0f, 17f * t + variant * 11f, 0f));
                float height = 2.6f - t * 0.25f;
                b.Frustum(m, radius, 0f, y, y + height, 7, greens[(t + variant) % greens.Length], false, true);
                y += height * 0.55f;
                radius *= 0.76f;
            }
            return SaveMesh(b.Build($"Pine_{variant}"));
        }

        private static int BuildTrees(Transform map)
        {
            var root = new GameObject("Trees").transform;
            root.SetParent(map, false);
            var meshes = new[] { PineMesh(0), PineMesh(1), PineMesh(2) };
            int count = 0, tris = 0;
            const float spacing = 5.5f;
            for (float x = -90f; x <= 90f; x += spacing)
            for (float z = -90f; z <= 90f; z += spacing)
            {
                float px = x + Range(-2f, 2f), pz = z + Range(-2f, 2f);
                if (!ValleyShape.CanPlantTree(px, pz)) continue;
                // Mas bosque hacia las montañas y en manchones; claros en el centro.
                float r = new Vector2(px, pz).magnitude;
                float density = Mathf.Lerp(0.12f, 0.75f, ValleyShape.Smooth(30f, 70f, r)) * (0.4f + ValleyShape.Fbm(px * 0.05f, pz * 0.05f, 2));
                if (rng.NextDouble() > density) continue;
                var mesh = meshes[rng.Next(meshes.Length)];
                var go = Place("Pine", root, mesh, lit, Ground(px, pz) + Vector3.down * 0.1f, Quaternion.Euler(0f, Range(0f, 360f), 0f), Range(0.8f, 1.35f));
                var col = go.AddComponent<CapsuleCollider>();
                col.radius = 0.35f;
                col.height = 4f;
                col.center = new Vector3(0f, 2f, 0f);
                go.isStatic = true;
                count++;
                tris += mesh.triangles.Length / 3;
            }
            Debug.Log($"[Mapa] {count} pinos.");
            return tris;
        }

        // ---------------------------------------------------------------- rocas

        private static Mesh RockMesh(int variant)
        {
            // Octaedro subdividido una vez, deformado con ruido: roca facetada.
            var b = new LowPolyMesh();
            Vector3[] o = { Vector3.up, Vector3.right, Vector3.forward, Vector3.left, Vector3.back, Vector3.down };
            int[,] faces = { { 0, 2, 1 }, { 0, 3, 2 }, { 0, 4, 3 }, { 0, 1, 4 }, { 5, 1, 2 }, { 5, 2, 3 }, { 5, 3, 4 }, { 5, 4, 1 } };
            Vector3 Deform(Vector3 v)
            {
                v = v.normalized;
                float k = 0.75f + 0.5f * Mathf.PerlinNoise(v.x * 2.1f + variant * 3.3f, v.z * 2.1f + v.y * 1.7f);
                return new Vector3(v.x * 1.2f, v.y * 0.75f, v.z) * k;
            }
            var stone = new Color(0.58f, 0.55f, 0.52f);
            var moss = new Color(0.33f, 0.55f, 0.25f);
            for (int f = 0; f < 8; f++)
            {
                Vector3 a = o[faces[f, 0]], c1 = o[faces[f, 1]], c2 = o[faces[f, 2]];
                Vector3 ab = (a + c1) / 2f, bc = (c1 + c2) / 2f, ca = (c2 + a) / 2f;
                Vector3[,] sub = { { a, ab, ca }, { ab, c1, bc }, { ca, bc, c2 }, { ab, bc, ca } };
                for (int s = 0; s < 4; s++)
                {
                    Vector3 p0 = Deform(sub[s, 0]), p1 = Deform(sub[s, 1]), p2 = Deform(sub[s, 2]);
                    Vector3 n = Vector3.Cross(p1 - p0, p2 - p0).normalized;
                    Color c = n.y > 0.65f ? moss : LowPolyMesh.Jitter(stone, 0.12f, f * 4 + s + variant * 31);
                    b.Tri(p0, p1, p2, c);
                }
            }
            return SaveMesh(b.Build($"Rock_{variant}"));
        }

        private static int BuildRocks(Transform map)
        {
            var root = new GameObject("Rocks").transform;
            root.SetParent(map, false);
            var meshes = new[] { RockMesh(0), RockMesh(1), RockMesh(2), RockMesh(3) };
            int placed = 0, tries = 0, tris = 0;
            while (placed < 45 && tries++ < 800)
            {
                float px = Range(-75f, 75f), pz = Range(-75f, 75f);
                bool shore = ValleyShape.LakeFactor(px, pz) is float lake && lake > 1.05f && lake < 1.5f;
                if (!shore && !ValleyShape.CanPlantTree(px, pz)) continue;
                float r = new Vector2(px, pz).magnitude;
                float size = r > 55f ? Range(1.5f, 3.5f) : Range(0.5f, 1.6f);
                var mesh = meshes[rng.Next(meshes.Length)];
                var go = Place("Rock", root, mesh, lit, Ground(px, pz) + Vector3.down * size * 0.25f,
                    Quaternion.Euler(Range(-8f, 8f), Range(0f, 360f), Range(-8f, 8f)), size);
                var col = go.AddComponent<MeshCollider>();
                col.sharedMesh = mesh;
                col.convex = true;
                go.isStatic = true;
                placed++;
                tris += mesh.triangles.Length / 3;
            }
            return tris;
        }

        // ---------------------------------------------------------------- luz

        private static void SetupLighting()
        {
            var sun = GameObject.Find("Directional Light");
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
                var light = sun.GetComponent<Light>();
                light.color = new Color(1f, 0.93f, 0.8f);
                light.intensity = 1.25f;
                light.shadows = LightShadows.Soft;
            }

            const string skyPath = "Assets/_Project/Materials/Sky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            sky.SetColor("_SkyTint", new Color(0.45f, 0.65f, 0.95f));
            sky.SetColor("_GroundColor", new Color(0.55f, 0.6f, 0.5f));
            sky.SetFloat("_AtmosphereThickness", 0.8f);
            sky.SetFloat("_Exposure", 1.2f);
            RenderSettings.skybox = sky;
            // La camara de PC dibujaba un color liso (marron): el cielo solo se ve con Skybox.
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                cam.clearFlags = CameraClearFlags.Skybox;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.66f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.36f, 0.26f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.68f, 0.8f, 0.92f);
            RenderSettings.fogStartDistance = 55f;
            RenderSettings.fogEndDistance = 230f;
        }

        // ---------------------------------------------------------------- objetos de juego

        private static void RelocateGameplay()
        {
            // Lo que era del laboratorio y no va en el mapa.
            foreach (var name in new[] { "Floor", "Pond", "LooseStone" })
            {
                var go = GameObject.Find(name);
                if (go != null) Object.DestroyImmediate(go);
            }

            // Campo de practica: el papel del laboratorio mira a los blancos y muñecos, igual que antes.
            Vector3 field = Ground(ValleyShape.FieldCenter.x, ValleyShape.FieldCenter.y);
            Vector3 offset = new Vector3(field.x, field.y - 0.5f, field.z - 10f);
            foreach (var name in new[] { "TestProps", "PaperCanvas", "Dummy_A", "Dummy_B" })
            {
                var go = GameObject.Find(name);
                if (go == null || !LabPositions.TryGetValue(name, out var lab)) continue;
                go.transform.position = lab + offset;
            }

            // Mesa de herramientas al lado del atril, con lapiz, pincel, goma, trapo y tintero encima.
            var mapRoot = GameObject.Find("Map").transform;
            Vector3 tableAt = Ground(field.x - 2.2f, field.z - 10.5f);
            TowerBuilder.Table(mapRoot, lit, "ToolTable", tableAt - mapRoot.position, 0f, 1.6f, 0.7f);
            float x = -0.6f;
            foreach (var name in new[] { "Pen", "Brush", "Eraser", "Rag", "Inkwell" })
            {
                var go = GameObject.Find(name);
                if (go == null) continue;
                go.transform.position = tableAt + new Vector3(x, 0.95f, 0f);
                x += 0.3f;
            }

            var player = GameObject.Find("Player");
            if (player != null)
            {
                player.transform.position = Ground(ValleyShape.Spawn.x, ValleyShape.Spawn.y) + Vector3.up * 1.6f;
                player.transform.rotation = Quaternion.identity; // mirando a la torre (+Z)
            }

            // Escritorio de la biblioteca: un papel horizontal que emite por la cara con tinta (hacia arriba).
            var desk = GameObject.Find("LibraryDesk");
            var paper = GameObject.Find("PaperCanvas");
            if (desk != null && paper != null && GameObject.Find("DeskPaper") == null)
            {
                var deskPaper = Object.Instantiate(paper, desk.transform.position + Vector3.up * 0.86f, Quaternion.Euler(90f, 0f, 0f));
                deskPaper.name = "DeskPaper";
                deskPaper.transform.localScale = new Vector3(0.7f, 0.7f, deskPaper.transform.localScale.z);
                var rb = deskPaper.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;
                var canvas = deskPaper.GetComponent<DrawingCanvas>();
                var so = new SerializedObject(canvas);
                so.FindProperty("emissionSide").enumValueIndex = (int)EmissionSide.InkSide;
                so.ApplyModifiedProperties();
            }
        }

        /// <summary>Posiciones originales en el laboratorio (para que regenerar no acumule desplazamientos).</summary>
        private static readonly System.Collections.Generic.Dictionary<string, Vector3> LabPositions = new System.Collections.Generic.Dictionary<string, Vector3>
        {
            { "TestProps", Vector3.zero },
            { "PaperCanvas", new Vector3(0f, 2.5f, 2f) },
            { "Dummy_A", new Vector3(0f, 0.5f, 11f) },
            { "Dummy_B", new Vector3(-3.5f, 0.5f, 5f) },
            { "Pen", new Vector3(0.28f, 1.0f, -1.36f) },
            { "Brush", new Vector3(0.46f, 1.0f, -1.36f) },
            { "Eraser", new Vector3(-0.24f, 0.8f, -1.4f) },
            { "Rag", new Vector3(0.11f, 0.8f, -1.4f) },
            { "Inkwell", new Vector3(-0.6f, 0.9f, -1.4f) }
        };
    }
}
