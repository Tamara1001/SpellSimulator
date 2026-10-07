using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SpellSimulator.Canvas;
using SpellSimulator.Compiler;
using SpellSimulator.Output;
using SpellSimulator.Reactions;
using SpellSimulator.World.Map;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>
    /// Detalles del mapa (M5): cascada + arroyo, pasto y flores, ambiente de la torre (velas, motas magicas, atril),
    /// papeles fijos y agarrables, y post-proceso. Lo llama MapBuilder despues de la geometria principal.
    /// </summary>
    public static class MapDetails
    {
        // ---------------------------------------------------------------- cascada y arroyo

        public static void BuildWaterfall(Transform map, SpellOutputController output)
        {
            var root = new GameObject("Waterfall").transform;
            root.SetParent(map, false);

            Vector2 top = ValleyShape.WaterfallTop, bottom = ValleyShape.WaterfallBase;
            Vector2 dir = (bottom - top).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            const int samples = 40;
            const float width = 4.5f;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                Vector2 p = Vector2.Lerp(top, bottom, t);
                // Se ensancha al caer.
                float w = width * Mathf.Lerp(0.6f, 1.1f, t);
                foreach (float side in new[] { -0.5f, 0.5f })
                {
                    Vector2 q = p + perp * w * side;
                    verts.Add(new Vector3(q.x, SurfaceY(q) + 0.6f, q.y));
                    uvs.Add(new Vector2(side + 0.5f, t));
                }
                if (i == 0) continue;
                int a = verts.Count - 4;
                tris.Add(a); tris.Add(a + 2); tris.Add(a + 1);
                tris.Add(a + 1); tris.Add(a + 2); tris.Add(a + 3);
            }
            var mesh = new Mesh { name = "Waterfall" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh = MapBuilder.SaveMesh(mesh);
            var fall = MapBuilder.Place("WaterfallRibbon", root, mesh, Material("Waterfall", "SpellSimulator/Waterfall"), Vector3.zero, Quaternion.identity);
            fall.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // Arroyo: franja de agua del pie de la cascada al lago, con su volumen (moja, hace flotar).
            Vector2 riverFrom = bottom - dir * 1.5f, riverTo = ValleyShape.RiverEnd + (ValleyShape.RiverEnd - bottom).normalized * 4f;
            Vector2 riverDir = (riverTo - riverFrom).normalized, riverPerp = new Vector2(-riverDir.y, riverDir.x);
            float riverWidth = (ValleyShape.RiverHalfWidth + 1.6f) * 2f;
            var r = new Mesh { name = "River" };
            Vector3 V(Vector2 p) => new Vector3(p.x, ValleyShape.WaterLevel, p.y);
            r.SetVertices(new List<Vector3>
            {
                V(riverFrom - riverPerp * riverWidth / 2f), V(riverFrom + riverPerp * riverWidth / 2f),
                V(riverTo - riverPerp * riverWidth / 2f), V(riverTo + riverPerp * riverWidth / 2f)
            });
            r.SetTriangles(new[] { 0, 1, 2, 2, 1, 3 }, 0);
            r.RecalculateNormals();
            if (r.normals[0].y < 0f) r.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
            r.RecalculateNormals();
            r = MapBuilder.SaveMesh(r);
            var waterMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Water.mat");
            var river = MapBuilder.Place("River", root, r, waterMat, Vector3.zero, Quaternion.identity);
            river.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            var volume = new GameObject("RiverVolume");
            volume.transform.SetParent(root, false);
            Vector2 mid = (riverFrom + riverTo) / 2f;
            volume.transform.SetPositionAndRotation(new Vector3(mid.x, ValleyShape.WaterLevel, mid.y),
                Quaternion.LookRotation(new Vector3(riverDir.x, 0f, riverDir.y)));
            var box = volume.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, -1.5f, 0f);
            box.size = new Vector3(riverWidth, 3f, Vector2.Distance(riverFrom, riverTo));
            var water = volume.AddComponent<WaterVolume>();
            var so = new SerializedObject(water);
            so.FindProperty("surface").objectReferenceValue = river.GetComponent<MeshRenderer>();
            so.FindProperty("output").objectReferenceValue = output;
            so.ApplyModifiedProperties();

            // Espuma y bruma al pie.
            if (output == null) return;
            Vector3 foot = new Vector3(bottom.x, ValleyShape.WaterLevel + 0.2f, bottom.y);
            Loop("FallSplash", root, foot, output.LookFor(ElementType.Water), 70f, new Vector2(0.2f, 0.45f), new Vector2(2f, 4f), 0.9f, 1.2f, 25f);
            var fallAudio = new GameObject("FallAudio");
            fallAudio.transform.SetParent(root, false);
            fallAudio.transform.position = foot + Vector3.up * 2f;
            fallAudio.AddComponent<SpellSimulator.Audio.AmbientEmitter>();
            Loop("FallMist", root, foot + Vector3.up * 0.5f, output.LookFor(ElementType.Wind), 8f, new Vector2(1.2f, 2.4f), new Vector2(0.3f, 0.8f), -0.05f, 2.5f, 60f);
        }

        /// <summary>
        /// Altura real de la malla del terreno (no la teorica: en laderas empinadas los triangulos de 2.5 m se apartan
        /// varios metros de ValleyShape.Height y taparian la cascada).
        /// </summary>
        private static float SurfaceY(Vector2 p)
        {
            Physics.SyncTransforms();
            float best = Mathf.Max(ValleyShape.Height(p.x, p.y), ValleyShape.WaterLevel);
            foreach (var hit in Physics.RaycastAll(new Vector3(p.x, 200f, p.y), Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.name == "Terrain") best = Mathf.Max(hit.point.y, ValleyShape.WaterLevel);
            return best;
        }

        /// <summary>Particulas en bucle (cascada, velas, motas): un sistema simple con el look de un elemento.</summary>
        public static ParticleSystem Loop(string name, Transform parent, Vector3 at, ElementLook look, float rate, Vector2 size, Vector2 speed,
            float gravity, float lifetime, float coneAngle, float shapeRadius = 0.6f)
        {
            if (look == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(Vector3.up));
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.maxParticles = 300;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime * 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.gravityModifier = gravity;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = coneAngle;
            shape.radius = shapeRadius;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(look.color);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = look.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        // ---------------------------------------------------------------- pasto y flores

        public static int BuildGrass(Transform map, Material material, System.Random rng)
        {
            var root = new GameObject("Grass").transform;
            root.SetParent(map, false);
            Color[] petals = { new Color(1f, 0.95f, 0.85f), new Color(1f, 0.85f, 0.3f), new Color(0.95f, 0.55f, 0.75f), new Color(0.7f, 0.55f, 0.95f) };
            const float chunk = 20f;
            int tris = 0;
            for (float cx = -60f; cx < 60f; cx += chunk)
            for (float cz = -60f; cz < 60f; cz += chunk)
            {
                var b = new LowPolyMesh();
                for (int k = 0; k < 170; k++)
                {
                    float x = cx + (float)rng.NextDouble() * chunk, z = cz + (float)rng.NextDouble() * chunk;
                    if (!GrassAllowed(x, z)) continue;
                    Vector3 basePoint = MapBuilder.Ground(x, z);
                    float tone = ValleyShape.Fbm(x * 0.1f, z * 0.1f, 2);
                    Color blade = Color.Lerp(new Color(0.3f, 0.58f, 0.22f), new Color(0.48f, 0.72f, 0.3f), tone);
                    float height = 0.25f + (float)rng.NextDouble() * 0.3f;
                    for (int s = 0; s < 3; s++)
                    {
                        float yaw = (float)rng.NextDouble() * Mathf.PI;
                        Vector3 side = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * 0.06f;
                        Vector3 lean = new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * 0.15f;
                        Vector3 offset = new Vector3(((float)rng.NextDouble() - 0.5f) * 0.2f, 0f, ((float)rng.NextDouble() - 0.5f) * 0.2f);
                        Vector3 a = basePoint + offset - side, c = basePoint + offset + side, tip = basePoint + offset + Vector3.up * height + lean;
                        b.Tri(a, tip, c, blade);
                        b.Tri(c, tip, a, blade * 0.85f);
                    }
                    // Una de cada ~12 matas lleva flor.
                    if (rng.NextDouble() < 0.08)
                    {
                        Color petal = petals[rng.Next(petals.Length)];
                        Vector3 head = basePoint + Vector3.up * (height + 0.05f);
                        b.Frustum(Matrix4x4.Translate(head), 0.07f, 0f, 0f, 0.06f, 5, petal, false, true);
                        b.Frustum(Matrix4x4.Translate(head), 0.07f, 0f, 0f, -0.04f, 5, petal * 0.8f, false);
                    }
                }
                if (b.TriangleCount == 0) continue;
                var mesh = MapBuilder.SaveMesh(b.Build($"Grass_{Mathf.RoundToInt(cx)}_{Mathf.RoundToInt(cz)}"));
                var go = MapBuilder.Place("GrassChunk", root, mesh, material, Vector3.zero, Quaternion.identity);
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                go.isStatic = true;
                tris += b.TriangleCount;
            }
            return tris;
        }

        private static bool GrassAllowed(float x, float z)
        {
            if (new Vector2(x, z).magnitude > ValleyShape.PlayableRadius - 4f) return false;
            if (ValleyShape.LakeFactor(x, z) < 1.2f || ValleyShape.RiverDistance(x, z) < ValleyShape.RiverHalfWidth + 2.5f) return false;
            if (ValleyShape.PathDistance(x, z) < ValleyShape.PathWidth) return false;
            if (Vector2.Distance(new Vector2(x, z), ValleyShape.TowerCenter) < 6.5f) return false;
            const float e = 0.8f;
            float dx = ValleyShape.Height(x + e, z) - ValleyShape.Height(x - e, z);
            float dz = ValleyShape.Height(x, z + e) - ValleyShape.Height(x, z - e);
            return Mathf.Sqrt(dx * dx + dz * dz) / (2f * e) < 0.45f;
        }

        // ---------------------------------------------------------------- torre: ambiente

        public static void TowerAmbience(Transform tower, Material material, SpellOutputController output)
        {
            var root = new GameObject("Ambience").transform;
            root.SetParent(tower, false);
            Vector3 o = tower.position;

            // Velas sobre el escritorio y la mesa de lectura.
            Vector3[] candles = { new Vector3(-0.7f, 0.82f, 0.85f), new Vector3(0.75f, 0.82f, 0.85f), new Vector3(-1.2f, 0.82f, -1.4f) };
            var b = new LowPolyMesh();
            foreach (var c in candles)
            {
                b.Frustum(Matrix4x4.Translate(c), 0.05f, 0.05f, 0f, 0.18f, 6, new Color(0.95f, 0.92f, 0.82f));
                b.Frustum(Matrix4x4.Translate(c), 0.09f, 0.09f, -0.01f, 0.01f, 6, new Color(0.7f, 0.55f, 0.3f));
                if (output != null)
                    Loop("CandleFlame", root, o + c + Vector3.up * 0.2f, output.LookFor(ElementType.Fire), 14f,
                        new Vector2(0.05f, 0.09f), new Vector2(0.05f, 0.15f), -0.1f, 0.35f, 5f, 0.01f);
            }
            // Atril con un libro abierto, junto a la puerta.
            var lectern = Matrix4x4.TRS(new Vector3(1.9f, 0f, -1.6f), Quaternion.Euler(0f, -40f, 0f), Vector3.one);
            b.Frustum(lectern, 0.25f, 0.12f, 0f, 0.1f, 6, new Color(0.36f, 0.22f, 0.15f));
            b.Box(lectern * Matrix4x4.Translate(new Vector3(0f, 0.6f, 0f)), new Vector3(0.1f, 1.1f, 0.1f), new Color(0.36f, 0.22f, 0.15f));
            var desk = lectern * Matrix4x4.TRS(new Vector3(0f, 1.15f, 0f), Quaternion.Euler(-25f, 0f, 0f), Vector3.one);
            b.Box(desk, new Vector3(0.6f, 0.04f, 0.45f), new Color(0.45f, 0.3f, 0.2f));
            b.Box(desk * Matrix4x4.TRS(new Vector3(-0.14f, 0.04f, 0f), Quaternion.Euler(0f, 0f, 6f), Vector3.one), new Vector3(0.26f, 0.025f, 0.36f), new Color(0.96f, 0.92f, 0.8f));
            b.Box(desk * Matrix4x4.TRS(new Vector3(0.14f, 0.04f, 0f), Quaternion.Euler(0f, 0f, -6f), Vector3.one), new Vector3(0.26f, 0.025f, 0.36f), new Color(0.96f, 0.92f, 0.8f));
            var mesh = MapBuilder.SaveMesh(b.Build("TowerProps"));
            MapBuilder.Place("TowerProps", root, mesh, material, o, Quaternion.identity);

            // Motas magicas flotando en la biblioteca y el estudio.
            if (output == null) return;
            var light = output.LookFor(ElementType.Light);
            Loop("MagicMotes", root, o + Vector3.up * 0.5f, light, 6f, new Vector2(0.03f, 0.07f), new Vector2(0.05f, 0.2f), -0.02f, 6f, 70f, 3f);
            Loop("MagicMotesStudy", root, o + Vector3.up * (TowerBuilder.FloorHeight + 0.5f), light, 4f, new Vector2(0.03f, 0.07f), new Vector2(0.05f, 0.2f), -0.02f, 6f, 70f, 2.5f);
        }

        // ---------------------------------------------------------------- papeles

        /// <summary>
        /// Papeles del mapa (copias del papel del laboratorio): fijo en la pared exterior de la torre mirando al campo
        /// (emite por la cara con tinta), y agarrables chico y mediano en la mesa de lectura.
        /// </summary>
        public static void Papers(Vector3 towerOrigin, Material material, Transform map)
        {
            var template = GameObject.Find("PaperCanvas");
            if (template == null) return;

            // Pared exterior, hacia el campo de practica (sureste).
            const float angle = 324f;
            Vector3 outward = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad));
            Vector3 boardAt = towerOrigin + outward * (TowerBuilder.OuterRadius + 0.06f) + Vector3.up * 1.9f;
            var board = new LowPolyMesh();
            board.Box(Matrix4x4.identity, new Vector3(1.5f, 1.5f, 0.08f), new Color(0.45f, 0.3f, 0.2f));
            var boardMesh = MapBuilder.SaveMesh(board.Build("PaperBoard"));
            MapBuilder.Place("PaperBoard", map, boardMesh, material, boardAt, Quaternion.LookRotation(-outward));
            var wall = Paper(template, "WallPaper", boardAt + outward * 0.06f, Quaternion.LookRotation(-outward), 1.25f, kinematic: true);
            SetEmission(wall, EmissionSide.InkSide);
            MakeFixed(wall);
            var desk = GameObject.Find("DeskPaper");
            if (desk != null) MakeFixed(desk);

            // Agarrables en la mesa de lectura de la biblioteca.
            Paper(template, "Paper_S", towerOrigin + new Vector3(-1.7f, 0.87f, -1.3f), Quaternion.Euler(90f, 20f, 0f), 0.35f, kinematic: false);
            Paper(template, "Paper_M", towerOrigin + new Vector3(-1.15f, 0.87f, -1.05f), Quaternion.Euler(90f, -15f, 0f), 0.6f, kinematic: false);
        }

        private static GameObject Paper(GameObject template, string name, Vector3 position, Quaternion rotation, float size, bool kinematic)
        {
            var paper = GameObject.Find(name);
            if (paper == null)
            {
                paper = Object.Instantiate(template);
                paper.name = name;
            }
            paper.transform.SetPositionAndRotation(position, rotation);
            paper.transform.localScale = new Vector3(size, size, template.transform.localScale.z);
            var rb = paper.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = kinematic;
            return paper;
        }

        /// <summary>Papel fijo (pared, escritorio): se dibuja pero no se agarra.</summary>
        private static void MakeFixed(GameObject paper)
        {
            var grab = paper.GetComponent<World.PhysicalCanvas>();
            if (grab != null) Object.DestroyImmediate(grab);
            var rb = paper.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
        }

        private static void SetEmission(GameObject paper, EmissionSide side)
        {
            var canvas = paper.GetComponent<DrawingCanvas>();
            var so = new SerializedObject(canvas);
            so.FindProperty("emissionSide").enumValueIndex = (int)side;
            so.ApplyModifiedProperties();
        }

        // ---------------------------------------------------------------- post-proceso

        public static void PostProcess()
        {
            const string path = "Assets/_Project/Data/PostProcess.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            T Get<T>() where T : VolumeComponent
            {
                if (!profile.TryGet(out T c)) { c = profile.Add<T>(true); AssetDatabase.AddObjectToAsset(c, profile); }
                c.active = true;
                return c;
            }
            var bloom = Get<Bloom>();
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.6f);
            bloom.scatter.Override(0.65f);
            var tone = Get<Tonemapping>();
            tone.mode.Override(TonemappingMode.Neutral);
            var color = Get<ColorAdjustments>();
            color.saturation.Override(12f);
            color.contrast.Override(6f);
            color.postExposure.Override(0.1f);
            var vignette = Get<Vignette>();
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.5f);
            EditorUtility.SetDirty(profile);

            var go = GameObject.Find("PostProcess") ?? new GameObject("PostProcess");
            var volume = go.GetComponent<Volume>() ?? go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var data = cam.GetComponent<UniversalAdditionalCameraData>();
                if (data == null) continue;
                data.renderPostProcessing = true;
                EditorUtility.SetDirty(data);
            }
        }

        private static Material Material(string name, string shader)
        {
            string path = $"Assets/_Project/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(m, path);
            }
            return m;
        }
    }
}
