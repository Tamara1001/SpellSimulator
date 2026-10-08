using UnityEditor;
using UnityEngine;
using SpellSimulator.Output;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// G2: looks de agua, viento y tierra (el fuego y la luz no se tocan). Crea los materiales de particula con forma
    /// (gota, bocanada toon, hoja, cinta de estela), la malla de esquirla low-poly y los looks secundarios (capas e
    /// impactos) en Data/Elements/Fx, y reescribe Water/Wind/Earth. Idempotente: se puede correr de nuevo.
    /// </summary>
    public static class ElementVfxSetup
    {
        private const string ElementsDir = "Assets/_Project/Data/Elements";
        private const string FxDir = ElementsDir + "/Fx";
        private const string MaterialsDir = "Assets/_Project/Materials";
        private const string MeshPath = "Assets/_Project/Art/Generated/RockShard.asset";
        private const string AlphaParticlePath = MaterialsDir + "/SpellParticleAlpha.mat";

        [MenuItem("SpellSimulator/VFX/Setup Element Looks (agua, viento, tierra)")]
        public static void Setup()
        {
            if (!AssetDatabase.IsValidFolder(FxDir)) AssetDatabase.CreateFolder(ElementsDir, "Fx");

            var water = ParticleMaterial("SpellWater", 1f, new Color(0.88f, 0.97f, 1f));
            var puff = ParticleMaterial("SpellPuff", 2f, new Color(1f, 0.95f, 0.85f));
            var leaf = ParticleMaterial("SpellLeaf", 3f, Color.white);
            var streak = ParticleMaterial("SpellStreak", 4f, Color.white);
            var rock = RockMaterial();
            var shard = ShardMesh();

            SetupWater(water, puff);
            SetupWind(leaf, streak);
            SetupEarth(rock, shard, puff);
            SetupCrystal();

            AssetDatabase.SaveAssets();
            Debug.Log("[VFX Setup] Looks de agua, viento y tierra actualizados.");
        }

        // ---------------------------------------------------------------- agua

        private static void SetupWater(Material water, Material puff)
        {
            var foam = Look("WaterFoam");
            Base(foam, puff, Gradient(
                new[] { (new Color(0.92f, 0.97f, 1f), 0f), (Color.white, 1f) },
                new[] { (0f, 0f), (0.85f, 0.15f), (0.6f, 0.6f), (0f, 1f) }), size: 0.05f, gravity: 0.8f, lifetime: 0.6f, turbulence: 0.3f);
            foam.layerRatio = 0.22f;
            foam.layerSpeed = 0.85f;
            foam.maxParticles = 90;
            Save(foam);

            var spray = Look("WaterSpray");
            Base(spray, water, Gradient(
                new[] { (new Color(0.8f, 0.93f, 1f), 0f), (new Color(0.55f, 0.8f, 1f), 1f) },
                new[] { (0.9f, 0f), (0.7f, 0.6f), (0f, 1f) }), size: 0.035f, gravity: 1.6f, lifetime: 0.45f, turbulence: 0f);
            spray.stretch = 0.5f;
            spray.speed = 2.2f;
            spray.impactCount = 4;
            spray.impactChance = 0.45f;
            spray.maxParticles = 120;
            Save(spray);

            var main = Load("Water");
            Base(main, water, Gradient(
                new[] { (new Color(0.55f, 0.85f, 1f), 0f), (new Color(0.3f, 0.65f, 1f), 0.35f), (new Color(0.22f, 0.5f, 0.92f), 1f) },
                new[] { (0f, 0f), (0.85f, 0.08f), (0.7f, 0.7f), (0f, 1f) }), size: 0.08f, gravity: 1.1f, lifetime: 1f, turbulence: 0.12f);
            main.stretch = 0.15f;
            main.speed = 1.1f;
            main.density = 0.75f;
            main.maxParticles = 260;
            main.layers = new[] { foam };
            main.impacts = new[] { spray };
            main.crumble = false;
            Save(main);
        }

        // ---------------------------------------------------------------- viento

        private static void SetupWind(Material leaf, Material streak)
        {
            var lines = Look("WindStreak");
            Base(lines, streak, Gradient(
                new[] { (new Color(0.92f, 0.98f, 1f), 0f), (Color.white, 1f) },
                new[] { (0f, 0f), (0.9f, 0.15f), (0.7f, 0.6f), (0f, 1f) }), size: 0.035f, gravity: 0f, lifetime: 0.7f, turbulence: 0.7f);
            lines.trails = true;
            lines.trailLifetime = 0.6f;
            lines.layerRatio = 0.09f;
            lines.layerSpeed = 1.05f;
            lines.maxParticles = 36;
            Save(lines);

            var leaves = Look("WindLeaf");
            Base(leaves, leaf, Gradient(
                new[] { (new Color(0.62f, 0.82f, 0.42f), 0f), (new Color(0.95f, 0.85f, 0.5f), 1f) },
                new[] { (0f, 0f), (1f, 0.1f), (1f, 0.8f), (0f, 1f) }), size: 0.08f, gravity: 0.04f, lifetime: 0.7f, turbulence: 0.9f);
            leaves.spin = 300f;
            leaves.layerRatio = 0.08f;
            leaves.layerSpeed = 0.8f;
            leaves.maxParticles = 28;
            Save(leaves);

            // El cuerpo deja de ser humo gris: motas de aire palidas y tenues; la lectura la dan estelas y hojas.
            // Ojo: la cascada (FallMist) y el vapor de WaterVolume usan este material y color.
            var main = Load("Wind");
            Base(main, AssetDatabase.LoadAssetAtPath<Material>(AlphaParticlePath), Gradient(
                new[] { (new Color(0.9f, 0.97f, 1f), 0f), (new Color(0.82f, 0.93f, 1f), 1f) },
                new[] { (0f, 0f), (0.14f, 0.2f), (0.08f, 0.7f), (0f, 1f) }), size: 0.045f, gravity: 0f, lifetime: 0.7f, turbulence: 1.2f);
            main.stretch = 3f;
            main.speed = 1.6f;
            main.density = 0.45f;
            main.maxParticles = 200;
            main.layers = new[] { lines, leaves };
            main.impacts = null;
            Save(main);
        }

        // ---------------------------------------------------------------- tierra

        private static void SetupEarth(Material rock, Mesh shard, Material puff)
        {
            var dustColor = new[] { (new Color(0.86f, 0.72f, 0.5f), 0f), (new Color(0.7f, 0.56f, 0.38f), 1f) };

            var dust = Look("EarthDust");
            Base(dust, puff, Gradient(dustColor, new[] { (0f, 0f), (0.7f, 0.1f), (0.4f, 0.6f), (0f, 1f) }),
                size: 0.09f, gravity: -0.04f, lifetime: 0.9f, turbulence: 0.25f);
            dust.sizeOverLife = new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1.6f));
            dust.layerRatio = 0.12f;
            dust.layerSpeed = 0.3f;
            dust.maxParticles = 70;
            Save(dust);

            var puffImpact = Look("EarthDustPuff");
            Base(puffImpact, puff, Gradient(dustColor, new[] { (0.75f, 0f), (0.45f, 0.5f), (0f, 1f) }),
                size: 0.08f, gravity: -0.05f, lifetime: 0.7f, turbulence: 0.2f);
            puffImpact.sizeOverLife = new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1.8f));
            puffImpact.speed = 0.7f;
            puffImpact.impactCount = 2;
            puffImpact.impactChance = 0.6f;
            puffImpact.maxParticles = 60;
            Save(puffImpact);

            var pebbles = Look("EarthPebbles");
            Base(pebbles, rock, Gradient(
                new[] { (new Color(0.85f, 0.66f, 0.42f), 0f), (new Color(0.7f, 0.52f, 0.33f), 1f) },
                new[] { (1f, 0f), (1f, 1f) }), size: 0.035f, gravity: 2.2f, lifetime: 0.8f, turbulence: 0f);
            pebbles.mesh = shard;
            pebbles.spin = 400f;
            pebbles.sizeOverLife = Crumble();
            pebbles.speed = 2f;
            pebbles.impactCount = 3;
            pebbles.impactChance = 0.5f;
            pebbles.maxParticles = 60;
            Save(pebbles);

            var main = Load("Earth");
            Base(main, rock, Gradient(
                new[] { (new Color(0.95f, 0.74f, 0.45f), 0f), (new Color(0.82f, 0.6f, 0.38f), 0.5f), (new Color(0.68f, 0.5f, 0.32f), 1f) },
                new[] { (1f, 0f), (1f, 1f) }), size: 0.085f, gravity: 2f, lifetime: 1.1f, turbulence: 0.05f);
            main.mesh = shard;
            main.spin = 160f;
            main.sizeOverLife = Crumble();
            main.speed = 0.8f;
            main.density = 0.3f;
            main.maxParticles = 140;
            main.layers = new[] { dust };
            main.impacts = new[] { puffImpact, pebbles };
            main.crumble = true;
            Save(main);
        }

        // ---------------------------------------------------------------- cristal

        private const string CrystalMeshPath = "Assets/_Project/Art/Generated/CrystalShard.asset";
        private const string SystemManagersPrefab = "Assets/_Project/Prefabs/System/SystemManagers.prefab";

        /// <summary>
        /// Cristal: variante celeste de Tierra (mismo elemento y fisica; el sigilo crystal elige este look). Esquirlas facetadas
        /// de celeste claro a cian con borde claro, motas brillantes, y al chocar fragmentos y destellos. Mismo presupuesto que Tierra.
        /// </summary>
        [MenuItem("SpellSimulator/VFX/Setup Cristal (variante celeste de tierra)")]
        public static void SetupCrystalMenu()
        {
            if (!AssetDatabase.IsValidFolder(FxDir)) AssetDatabase.CreateFolder(ElementsDir, "Fx");
            SetupCrystal();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void SetupCrystal()
        {
            var glass = CrystalMaterial();
            var mote = AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/SpellParticleAdditive.mat");
            var shard = CrystalShardMesh();

            var sparkle = Look("CrystalSparkle");
            Base(sparkle, mote, Gradient(
                new[] { (new Color(0.75f, 0.95f, 1f), 0f), (new Color(0.35f, 0.85f, 1f), 1f) },
                new[] { (0f, 0f), (0.9f, 0.15f), (0.5f, 0.6f), (0f, 1f) }), size: 0.03f, gravity: -0.02f, lifetime: 0.8f, turbulence: 0.3f);
            sparkle.sizeOverLife = new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.3f, 1.2f), new Keyframe(1f, 0f));
            sparkle.layerRatio = 0.14f;
            sparkle.layerSpeed = 0.35f;
            sparkle.maxParticles = 70;
            Save(sparkle);

            var glint = Look("CrystalGlint");
            Base(glint, mote, Gradient(
                new[] { (Color.white, 0f), (new Color(0.5f, 0.9f, 1f), 1f) },
                new[] { (0f, 0f), (1f, 0.08f), (0.4f, 0.5f), (0f, 1f) }), size: 0.07f, gravity: 0f, lifetime: 0.45f, turbulence: 0f);
            glint.sizeOverLife = new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.6f));
            glint.speed = 0.5f;
            glint.impactCount = 2;
            glint.impactChance = 0.6f;
            glint.maxParticles = 40;
            Save(glint);

            var fragments = Look("CrystalFragments");
            Base(fragments, glass, Gradient(
                new[] { (new Color(0.92f, 1f, 1f), 0f), (new Color(0.6f, 0.9f, 1f), 1f) },
                new[] { (1f, 0f), (1f, 1f) }), size: 0.03f, gravity: 2.2f, lifetime: 0.8f, turbulence: 0f);
            fragments.mesh = shard;
            fragments.spin = 500f;
            fragments.sizeOverLife = Crumble();
            fragments.speed = 2.1f;
            fragments.impactCount = 3;
            fragments.impactChance = 0.5f;
            fragments.maxParticles = 60;
            Save(fragments);

            var main = CrystalLook();
            Base(main, glass, Gradient(
                new[] { (new Color(0.95f, 1f, 1f), 0f), (new Color(0.75f, 0.96f, 1f), 0.5f), (new Color(0.58f, 0.9f, 1f), 1f) },
                new[] { (1f, 0f), (1f, 1f) }), size: 0.085f, gravity: 1.6f, lifetime: 1.1f, turbulence: 0.05f);
            main.mesh = shard;
            main.spin = 220f;
            main.sizeOverLife = Crumble();
            main.speed = 0.8f;
            main.density = 0.3f;
            main.maxParticles = 140;
            main.layers = new[] { sparkle };
            main.impacts = new[] { glint, fragments };
            main.crumble = true;
            main.crystalline = true;
            main.push = 1f;
            Save(main);

            AssignToPrefab(main);
            Debug.Log("[VFX Setup] Look de cristal actualizado.");
        }

        private static ElementLook CrystalLook()
        {
            string path = $"{ElementsDir}/Crystal.asset";
            var look = AssetDatabase.LoadAssetAtPath<ElementLook>(path);
            if (look != null) return look;
            look = ScriptableObject.CreateInstance<ElementLook>();
            AssetDatabase.CreateAsset(look, path);
            return look;
        }

        /// <summary>Asigna el look al SpellOutputController del prefab de managers (las escenas lo instancian).</summary>
        private static void AssignToPrefab(ElementLook crystal)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SystemManagersPrefab) == null) return;
            var root = PrefabUtility.LoadPrefabContents(SystemManagersPrefab);
            try
            {
                var output = root.GetComponentInChildren<SpellOutputController>(true);
                if (output == null) return;
                var so = new SerializedObject(output);
                var prop = so.FindProperty("crystal");
                if (prop == null || prop.objectReferenceValue == crystal) return;
                prop.objectReferenceValue = crystal;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, SystemManagersPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Material CrystalMaterial()
        {
            string path = $"{MaterialsDir}/SpellCrystal.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("SpellSimulator/StylizedLit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", new Color(0.82f, 0.97f, 1f));
            mat.SetColor("_ShadowTint", new Color(0.78f, 0.92f, 1f));
            mat.SetFloat("_Bands", 3f);
            mat.SetFloat("_RimStrength", 0.9f);
            mat.SetFloat("_RimSharpness", 1f);
            mat.SetFloat("_RimCut", 0.5f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Mesh CrystalShardMesh()
        {
            var fresh = LowPolyMeshes.Crystal(11);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(CrystalMeshPath);
            if (mesh == null)
            {
                AssetDatabase.CreateAsset(fresh, CrystalMeshPath);
                return fresh;
            }
            EditorUtility.CopySerialized(fresh, mesh);
            mesh.name = "CrystalShard";
            EditorUtility.SetDirty(mesh);
            Object.DestroyImmediate(fresh);
            return mesh;
        }

        /// <summary>Brota rapido, se sostiene y se desmorona al final.</summary>
        private static AnimationCurve Crumble() => new AnimationCurve(
            new Keyframe(0f, 0.3f), new Keyframe(0.08f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f));

        // ---------------------------------------------------------------- helpers

        private static void Base(ElementLook look, Material material, Gradient color, float size, float gravity, float lifetime, float turbulence)
        {
            look.material = material;
            look.color = color;
            look.size = size;
            look.gravity = gravity;
            look.lifetime = lifetime;
            look.turbulence = turbulence;
            look.mesh = null;
            look.spin = 0f;
            look.stretch = 0f;
            look.trails = false;
            look.sizeOverLife = new AnimationCurve();
            look.density = 1f;
            look.speed = 1f;
        }

        private static Gradient Gradient((Color c, float t)[] colors, (float a, float t)[] alphas)
        {
            var g = new Gradient();
            var ck = new GradientColorKey[colors.Length];
            for (int i = 0; i < colors.Length; i++) ck[i] = new GradientColorKey(colors[i].c, colors[i].t);
            var ak = new GradientAlphaKey[alphas.Length];
            for (int i = 0; i < alphas.Length; i++) ak[i] = new GradientAlphaKey(alphas[i].a, alphas[i].t);
            g.SetKeys(ck, ak);
            return g;
        }

        private static ElementLook Load(string name)
        {
            var look = AssetDatabase.LoadAssetAtPath<ElementLook>($"{ElementsDir}/{name}.asset");
            if (look == null) throw new System.InvalidOperationException($"Falta {ElementsDir}/{name}.asset");
            return look;
        }

        private static ElementLook Look(string name)
        {
            string path = $"{FxDir}/{name}.asset";
            var look = AssetDatabase.LoadAssetAtPath<ElementLook>(path);
            if (look != null) return look;
            look = ScriptableObject.CreateInstance<ElementLook>();
            AssetDatabase.CreateAsset(look, path);
            return look;
        }

        private static void Save(Object asset) => EditorUtility.SetDirty(asset);

        private static Material ParticleMaterial(string name, float shape, Color rim)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(AssetDatabase.LoadAssetAtPath<Material>(AlphaParticlePath));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetFloat("_Additive", 0f);
            mat.SetFloat("_SrcBlend", 5f);
            mat.SetFloat("_DstBlend", 10f);
            mat.SetFloat("_Shape", shape);
            mat.SetColor("_RimColor", rim);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material RockMaterial()
        {
            string path = $"{MaterialsDir}/SpellRock.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("SpellSimulator/StylizedLit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", Color.white);
            mat.SetColor("_ShadowTint", new Color(0.72f, 0.66f, 0.78f));
            mat.SetFloat("_Bands", 3f);
            mat.SetFloat("_RimStrength", 0.55f);
            mat.SetFloat("_RimSharpness", 1f);
            mat.SetFloat("_RimCut", 0.6f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Mesh ShardMesh()
        {
            var fresh = LowPolyMeshes.Shard(7);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null)
            {
                AssetDatabase.CreateAsset(fresh, MeshPath);
                return fresh;
            }
            EditorUtility.CopySerialized(fresh, mesh);
            mesh.name = "RockShard";
            EditorUtility.SetDirty(mesh);
            Object.DestroyImmediate(fresh);
            return mesh;
        }
    }
}
