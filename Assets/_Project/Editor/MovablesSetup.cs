using UnityEditor;
using UnityEngine;
using SpellSimulator.EditorTools.Map;
using SpellSimulator.Reactions;
using SpellSimulator.World;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Objetos movibles con peso realista (E3). Aplica masas por tipo, convierte las sillas de la torre en movibles y crea
    /// los props sueltos (barriles, bancos, mesas, cajas, macetas, velas, libros) que se agarran con el movedor: cuanto mas
    /// pesados, mas lento y blando los lleva <see cref="Movable"/>. No toca terreno, paredes ni escaleras. Se llama desde
    /// MapBuilder.Build antes de InteractionSetup (que agrega Respawnable). Menu: SpellSimulator/Interaction/Make Props Movable.
    /// </summary>
    public static class MovablesSetup
    {
        private const string WoodPath = "Assets/_Project/Data/Materials/Madera.asset";
        private const string StonePath = "Assets/_Project/Data/Materials/Piedra.asset";

        // Masas reales aproximadas (kg) por tipo.
        public const float BookMass = 1f, CandleMass = 0.3f, PotMass = 4f, StoolMass = 3f, ChairMass = 6f,
            BenchMass = 14f, TableMass = 25f, BarrelMass = 30f, SmallBoxMass = 6f, BigBoxMass = 40f;

        public enum Kind { Book, Candle, Pot, Stool, Chair, Bench, Table, Barrel, SmallBox, BigBox }

        [MenuItem("SpellSimulator/Interaction/Make Props Movable")]
        public static void ApplyFromMenu() => Debug.Log(Apply(GameObject.Find("Map") != null ? GameObject.Find("Map").transform : null, null));

        /// <summary>Convierte lo que ya existe (sillas de la torre, cajas y cofres) en movible con su masa.</summary>
        public static string Apply(Transform map, Material material)
        {
            int chairs = 0, crates = 0;
            var tower = GameObject.Find("Tower");
            if (tower != null)
            {
                // Sillas (las mesas y estantes de la torre quedan fijos: tienen velas y objetos horneados encima).
                foreach (var r in tower.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.name != "Chair" || r.GetComponent<Rigidbody>() != null) continue;
                    MakeMovable(r.gameObject, ChairMass, "silla", WoodProfile());
                    chairs++;
                }
            }
            // Cajas y cofres ya movibles: masa por tamano (caja de 0,5 m ~ 20 kg, tope 40 kg).
            foreach (var rb in Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string n = rb.name;
                bool crate = n.StartsWith("Crate_"), chest = n.StartsWith("Chest_");
                if ((!crate && !chest) || rb.isKinematic) continue;
                Vector3 s = rb.transform.localScale;
                float volume = Mathf.Abs(s.x * s.y * s.z);
                rb.mass = chest ? Mathf.Clamp(volume * 60f, 8f, 25f) : Mathf.Clamp(volume * 160f, 3f, BigBoxMass);
                crates++;
            }
            return $"Movables: {chairs} sillas, {crates} cajas con masa realista.";
        }

        /// <summary>Props sueltos sobre la mesa de estampas del campo (velas, libros, maceta) y un par de bancos y barriles.</summary>
        public static void BuildFieldProps(Transform map, Material material, Vector3 stampTable)
        {
            var root = Root("Props_Field");
            Prop(root, material, "Candle_Field_A", Kind.Candle, stampTable + new Vector3(-1.0f, 0.83f, 0.1f), 0f);
            Prop(root, material, "Candle_Field_B", Kind.Candle, stampTable + new Vector3(1.0f, 0.83f, -0.1f), 0f);
            Prop(root, material, "Book_Field_A", Kind.Book, stampTable + new Vector3(0.75f, 0.83f, 0.12f), 25f);
            Prop(root, material, "Book_Field_B", Kind.Book, stampTable + new Vector3(-0.75f, 0.83f, -0.1f), -15f);
            Prop(root, material, "Pot_Field_A", Kind.Pot, stampTable + new Vector3(1.15f, 0.83f, 0.15f), 0f);
            Prop(root, material, "Stool_Field_A", Kind.Stool, stampTable + new Vector3(-0.6f, 0f, -1.1f), 20f, true);
            Prop(root, material, "Stool_Field_B", Kind.Stool, stampTable + new Vector3(0.7f, 0f, -1.0f), -30f, true);
            Prop(root, material, "Barrel_Field_A", Kind.Barrel, stampTable + new Vector3(2.2f, 0f, 0.3f), 0f, true);
            Prop(root, material, "Bench_Field_A", Kind.Bench, stampTable + new Vector3(0f, 0f, 1.3f), 0f, true);
        }

        public static Transform Root(string name)
        {
            var old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, name);
            return go.transform;
        }

        // ---------------------------------------------------------------- props

        /// <summary>
        /// Crea un prop movible. Con onGround la posicion se apoya sobre el terreno (la Y de position se ignora); si no, position
        /// es la base del objeto (por ejemplo sobre una mesa).
        /// </summary>
        public static GameObject Prop(Transform parent, Material material, string name, Kind kind, Vector3 position, float yaw, bool onGround = false)
        {
            var old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
            if (onGround) position.y = ValleyHeight(position) + 0.02f;

            var b = new LowPolyMesh();
            Vector3 colSize, colCenter;
            float mass;
            string label;
            var wood = WoodProfile();
            var profile = wood;
            Color woodA = new Color(0.45f, 0.3f, 0.19f), woodB = new Color(0.33f, 0.21f, 0.13f);
            switch (kind)
            {
                case Kind.Book:
                    Color cover = Color.HSVToRGB(Mathf.Abs(Mathf.Sin(position.x * 12.9f + position.z * 7.3f)), 0.55f, 0.55f);
                    b.Box(Matrix4x4.Translate(new Vector3(0f, 0.025f, 0f)), new Vector3(0.2f, 0.05f, 0.28f), cover);
                    b.Box(Matrix4x4.Translate(new Vector3(0.008f, 0.025f, 0f)), new Vector3(0.19f, 0.04f, 0.27f), new Color(0.95f, 0.9f, 0.78f));
                    colSize = new Vector3(0.2f, 0.05f, 0.28f); colCenter = new Vector3(0f, 0.025f, 0f); mass = BookMass; label = "libro"; break;
                case Kind.Candle:
                    b.Frustum(Matrix4x4.identity, 0.09f, 0.09f, 0f, 0.012f, 6, new Color(0.7f, 0.55f, 0.3f));
                    b.Frustum(Matrix4x4.identity, 0.035f, 0.035f, 0.012f, 0.17f, 6, new Color(0.95f, 0.92f, 0.82f));
                    colSize = new Vector3(0.09f, 0.17f, 0.09f); colCenter = new Vector3(0f, 0.085f, 0f); mass = CandleMass; label = "vela"; break;
                case Kind.Pot:
                    b.Frustum(Matrix4x4.identity, 0.12f, 0.17f, 0f, 0.28f, 8, new Color(0.7f, 0.38f, 0.25f));
                    b.Frustum(Matrix4x4.Translate(new Vector3(0f, 0.27f, 0f)), 0.15f, 0.04f, 0f, 0.15f, 6, new Color(0.3f, 0.55f, 0.25f));
                    colSize = new Vector3(0.34f, 0.3f, 0.34f); colCenter = new Vector3(0f, 0.15f, 0f); mass = PotMass; label = "maceta";
                    profile = AssetDatabase.LoadAssetAtPath<MaterialProfile>(StonePath) ?? wood; break;
                case Kind.Stool:
                    b.Box(Matrix4x4.Translate(new Vector3(0f, 0.42f, 0f)), new Vector3(0.38f, 0.05f, 0.38f), woodA);
                    for (int i = 0; i < 4; i++)
                        b.Box(Matrix4x4.Translate(new Vector3((i & 1) == 0 ? -0.14f : 0.14f, 0.2f, i < 2 ? -0.14f : 0.14f)), new Vector3(0.05f, 0.4f, 0.05f), woodB);
                    colSize = new Vector3(0.38f, 0.45f, 0.38f); colCenter = new Vector3(0f, 0.225f, 0f); mass = StoolMass; label = "banquito"; break;
                case Kind.Chair:
                    b.Box(Matrix4x4.Translate(new Vector3(0f, 0.45f, 0f)), new Vector3(0.45f, 0.06f, 0.45f), new Color(0.65f, 0.2f, 0.2f));
                    b.Box(Matrix4x4.Translate(new Vector3(0f, 0.8f, -0.2f)), new Vector3(0.45f, 0.65f, 0.06f), woodB);
                    for (int i = 0; i < 4; i++)
                        b.Box(Matrix4x4.Translate(new Vector3((i & 1) == 0 ? -0.18f : 0.18f, 0.21f, i < 2 ? -0.18f : 0.18f)), new Vector3(0.05f, 0.42f, 0.05f), woodB);
                    colSize = new Vector3(0.45f, 1.1f, 0.45f); colCenter = new Vector3(0f, 0.55f, 0f); mass = ChairMass; label = "silla"; break;
                case Kind.Bench:
                    b.Box(Matrix4x4.Translate(new Vector3(0f, 0.43f, 0f)), new Vector3(1.4f, 0.06f, 0.36f), woodA);
                    foreach (float sx in new[] { -0.6f, 0.6f })
                        b.Box(Matrix4x4.Translate(new Vector3(sx, 0.2f, 0f)), new Vector3(0.07f, 0.4f, 0.3f), woodB);
                    colSize = new Vector3(1.4f, 0.46f, 0.36f); colCenter = new Vector3(0f, 0.23f, 0f); mass = BenchMass; label = "banco"; break;
                case Kind.Table:
                    b.Box(Matrix4x4.Translate(new Vector3(0f, 0.76f, 0f)), new Vector3(1.2f, 0.07f, 0.7f), woodA);
                    for (int i = 0; i < 4; i++)
                        b.Box(Matrix4x4.Translate(new Vector3((i & 1) == 0 ? -0.54f : 0.54f, 0.36f, i < 2 ? -0.29f : 0.29f)), new Vector3(0.07f, 0.72f, 0.07f), woodB);
                    colSize = new Vector3(1.2f, 0.8f, 0.7f); colCenter = new Vector3(0f, 0.4f, 0f); mass = TableMass; label = "mesa"; break;
                case Kind.Barrel:
                    b.Frustum(Matrix4x4.identity, 0.25f, 0.28f, 0f, 0.38f, 10, woodA, false);
                    b.Frustum(Matrix4x4.Translate(new Vector3(0f, 0.38f, 0f)), 0.28f, 0.25f, 0f, 0.38f, 10, woodA, true);
                    b.Frustum(Matrix4x4.Translate(new Vector3(0f, 0.12f, 0f)), 0.275f, 0.275f, 0f, 0.03f, 10, Color.Lerp(woodB, Color.black, 0.3f), false);
                    b.Frustum(Matrix4x4.Translate(new Vector3(0f, 0.6f, 0f)), 0.275f, 0.275f, 0f, 0.03f, 10, Color.Lerp(woodB, Color.black, 0.3f), false);
                    colSize = new Vector3(0.56f, 0.76f, 0.56f); colCenter = new Vector3(0f, 0.38f, 0f); mass = BarrelMass; label = "barril"; break;
                case Kind.SmallBox:
                    b.Box(Matrix4x4.Translate(new Vector3(0f, 0.2f, 0f)), new Vector3(0.4f, 0.4f, 0.4f), woodA, woodB);
                    colSize = new Vector3(0.4f, 0.4f, 0.4f); colCenter = new Vector3(0f, 0.2f, 0f); mass = SmallBoxMass; label = "caja chica"; break;
                default:
                    b.Box(Matrix4x4.Translate(new Vector3(0f, 0.4f, 0f)), new Vector3(0.8f, 0.8f, 0.8f), woodA, woodB);
                    colSize = new Vector3(0.8f, 0.8f, 0.8f); colCenter = new Vector3(0f, 0.4f, 0f); mass = BigBoxMass; label = "caja grande"; break;
            }
            var mesh = MapBuilder.SaveMesh(b.Build($"Prop_{kind}"));
            var go = MapBuilder.Place(name, parent, mesh, material, position, Quaternion.Euler(0f, yaw, 0f));
            var col = go.AddComponent<BoxCollider>();
            col.center = colCenter;
            col.size = colSize;
            MakeMovable(go, mass, label, profile);
            Undo.RegisterCreatedObjectUndo(go, name);
            return go;
        }

        private static float ValleyHeight(Vector3 p) => MapBuilder.Ground(p.x, p.z).y;

        private static MaterialProfile WoodProfile() => AssetDatabase.LoadAssetAtPath<MaterialProfile>(WoodPath);

        /// <summary>Rigidbody con la masa dada + Movable con nombre + reaccion de material (si hay perfil). Sin duplicar componentes.</summary>
        private static void MakeMovable(GameObject go, float mass, string label, MaterialProfile profile)
        {
            go.isStatic = false;
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.1f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            if (go.GetComponentInChildren<Collider>() == null)
            {
                var box = go.AddComponent<BoxCollider>();
                var mf = go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) { box.center = mf.sharedMesh.bounds.center; box.size = mf.sharedMesh.bounds.size; }
            }
            var movable = go.GetComponent<Movable>();
            if (movable == null) movable = go.AddComponent<Movable>();
            var so = new SerializedObject(movable);
            so.FindProperty("objectName").stringValue = label;
            so.ApplyModifiedProperties();
            if (profile != null && go.GetComponent<ElementReactive>() == null)
            {
                var reactive = go.AddComponent<ElementReactive>();
                var rso = new SerializedObject(reactive);
                rso.FindProperty("profile").objectReferenceValue = profile;
                rso.ApplyModifiedProperties();
            }
            if (go.GetComponent<Respawnable>() == null) go.AddComponent<Respawnable>();
        }
    }
}
