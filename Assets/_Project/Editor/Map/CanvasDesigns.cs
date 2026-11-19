using UnityEditor;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.World;
using SpellSimulator.World.Map;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>
    /// Variedad de lienzos (copias del papel del laboratorio con otra forma y otro vestido):
    /// cuadernos (con paginas) y lienzos grandes que se agarran y caen con gravedad, y soportes fijos: pizarra de pared,
    /// caballetes y mesa de dibujo inclinada. Todo lienzo apoya sobre algo (mesa, caballete, pared, soportes o piso).
    /// Todos emiten por la cara con tinta (InkSide): dar vuelta la hoja cambia hacia donde apunta el hechizo.
    /// </summary>
    public static partial class CanvasDesigns
    {
        private static readonly Color Leather = new Color(0.36f, 0.2f, 0.26f);
        private static readonly Color Wood = new Color(0.42f, 0.28f, 0.18f);
        private static readonly Color DarkWood = new Color(0.3f, 0.19f, 0.12f);
        private static readonly Color Metal = new Color(0.62f, 0.62f, 0.66f);

        public static void Build(Transform map, Material material, Vector3 towerOrigin, Vector3 fieldOrigin)
        {
            var template = GameObject.Find("PaperCanvas");
            if (template == null) return;
            var tower = GameObject.Find("Tower");
            float floor = TowerBuilder.FloorHeight;

            // Lienzos flotantes de versiones anteriores del mapa: se quitan.
            foreach (var oldName in new[] { "FloatingScroll_Study", "FloatingScroll_Field" })
            {
                var stale = GameObject.Find(oldName);
                if (stale != null) Object.DestroyImmediate(stale);
            }

            // Todos los lienzos de papel emiten por la cara con tinta.
            foreach (var c in Object.FindObjectsByType<DrawingCanvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // Los caballetes del tutorial disparan hacia el objetivo (AwayFromViewer, ver MapTutorial.MakeEasel).
                if (c.name.StartsWith("Easel_Earth") || c.name.StartsWith("Easel_Door")) continue;
                SetInkSide(c.gameObject);
            }

            // Biblioteca: cuadernos (con paginas) a la izquierda de la mesa de trabajo (las herramientas van a la derecha).
            Vector3 bench = towerOrigin + TowerProps.LibraryBench + Vector3.up * 0.82f;
            var notebookA = Grabbable(template, material, "Notebook_A", bench + new Vector3(-0.78f, 0.012f, 0.02f), Quaternion.Euler(90f, 12f, 0f), 0.24f, 0.32f, Dressing.Notebook);
            AddPages(notebookA);
            var notebookB = Grabbable(template, material, "Notebook_B", bench + new Vector3(-0.45f, 0.012f, -0.08f), Quaternion.Euler(90f, -20f, 0f), 0.2f, 0.28f, Dressing.Notebook);
            AddPages(notebookB);

            // Las pizarras de las estaciones de agua/viento, la mesa de dibujo y el caballete del estudio ya no estan en la torre:
            // los lienzos fijos del tutorial son los caballetes de MapTutorial (piso 1 y planta baja). Se borran los de builds anteriores
            // (cuelgan de la raiz de la escena, no de Map).
            foreach (string retired in new[] { "WallBoard_Library", "BigCanvas_Library", "DraftingTable", "Easel_Study", "Easel_Study_Canvas" })
            {
                var stale = GameObject.Find(retired);
                if (stale != null) Object.DestroyImmediate(stale);
            }

            // Lienzo gigante de pie en el entrenamiento, con dos soportes de madera detras para que no se caiga ni flote.
            // La cara mira a la mesa de estampas (fieldOrigin). El papel enorme queda en el piso, como antes.
            // Posiciones relativas al atril (TrainingLayout): el entrenamiento ya no esta en el campo viejo.
            float trainingYaw = TrainingLayout.YawDegrees;
            Vector3 giantBase = MapBuilder.TrainingGround(TrainingLayout.GiantStand);
            Quaternion giantRotation = StandingRotation(fieldOrigin - giantBase, 0f);
            Stand(material, "Stand_GiantCanvas_Field", giantBase, giantRotation);
            // De pie sobre su soporte y quieto hasta que se lo agarre (si no, la gravedad lo tumba al arrancar).
            Grabbable(template, material, "GiantCanvas_Field", StandingPosition(giantBase, giantRotation, 2.2f),
                giantRotation, 3f, 2.2f, Dressing.Framed).GetComponent<Rigidbody>().isKinematic = true;
            Grabbable(template, material, "GiantPaper_Field", MapBuilder.TrainingGround(TrainingLayout.GiantPaper) + new Vector3(0f, 0.15f, 0f),
                Quaternion.Euler(0f, trainingYaw, 0f) * Quaternion.Euler(90f, 20f, 0f), 3.5f, 3.5f, Dressing.None);

            // Entrenamiento: caballete con un lienzo grande.
            Easel(template, material, "Easel_Field", MapBuilder.TrainingGround(TrainingLayout.Easel), 200f + trainingYaw);

            // Zonas de practica por la pradera (lago, senderos) con lienzos, carteles y props movibles.
            PracticeZones(map, template, material, towerOrigin, fieldOrigin);

            // Restocker: pinceles y selector de lienzo / libreta (Bloque C).
            MapRestocker.Build(map, towerOrigin);
        }

        private enum Dressing { None, Notebook, Framed }

        /// <summary>Paginas por cuaderno (NotebookPages.pageCount).</summary>
        private const int NotebookPageCount = CanvasRules.MaxPages;

        /// <summary>Margen (m) del marco de madera que sobresale del papel (ver Dress, Dressing.Framed).</summary>
        private const float FrameMargin = 0.045f;

        // ---------------------------------------------------------------- piezas

        /// <summary>Ajusta un ancho × alto viejo al preset S/M/L mas parecido (CanvasRules) y devuelve el spec: todos los lienzos del mapa salen de ahi.</summary>
        private static CanvasSpec Snap(string name, ref float width, ref float height)
        {
            var kind = name.Contains("Notebook") ? CanvasKind.Notebook : CanvasKind.Sheet;
            CanvasSpec spec = CanvasRules.Nearest(kind, width, height);
            Vector2 d = CanvasRules.Dimensions(spec);
            width = d.x;
            height = d.y;
            return spec;
        }

        private static GameObject Spawn(GameObject template, string name, Vector3 position, Quaternion rotation, float width, float height, CanvasSpec spec)
        {
            var old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
            var go = Object.Instantiate(template);
            go.name = name;
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = new Vector3(width, height, template.transform.localScale.z);
            SetInkSide(go);
            Undo.RegisterCreatedObjectUndo(go, name);
            return go;
        }

        /// <summary>Lienzo que se agarra y cae con gravedad (al soltarlo se acuesta si es una hoja).</summary>
        private static GameObject Grabbable(GameObject template, Material material, string name, Vector3 position, Quaternion rotation,
            float width, float height, Dressing dressing)
        {
            CanvasSpec spec = Snap(name, ref width, ref height);
            var go = Spawn(template, name, position, rotation, width, height, spec);
            var rb = go.GetComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;
            var grab = go.GetComponent<PhysicalCanvas>();
            if (grab != null)
            {
                var so = new SerializedObject(grab);
                so.FindProperty("objectName").stringValue = spec.kind == CanvasKind.Notebook ? "libreta" : "lienzo";
                so.FindProperty("startFloating").boolValue = false;
                so.ApplyModifiedProperties();
            }
            Dress(go, material, dressing, width, height);
            if (go.GetComponent<Respawnable>() == null) go.AddComponent<Respawnable>();
            return go;
        }

        /// <summary>Cuaderno agarrable con paginas (lo usa el tutorial de la torre).</summary>
        internal static GameObject NotebookAt(GameObject template, Material material, string name, Vector3 position, Quaternion rotation, float width, float height)
        {
            var go = Grabbable(template, material, name, position, rotation, width, height, Dressing.Notebook);
            AddPages(go);
            return go;
        }

        /// <summary>Hoja de papel agarrable; con floating queda flotando en el aire donde se la pone (lienzo flotante).</summary>
        internal static GameObject SheetAt(GameObject template, string name, Vector3 position, Quaternion rotation, float size, bool floating)
        {
            float sw = size, sh = size;
            CanvasSpec spec = Snap(name, ref sw, ref sh);
            var go = Spawn(template, name, position, rotation, sw, sh, spec);
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = false; rb.useGravity = !floating; }
            var grab = go.GetComponent<PhysicalCanvas>();
            if (grab != null)
            {
                var so = new SerializedObject(grab);
                so.FindProperty("startFloating").boolValue = floating;
                so.ApplyModifiedProperties();
            }
            if (go.GetComponent<Respawnable>() == null) go.AddComponent<Respawnable>();
            return go;
        }

        /// <summary>Cuaderno con paginas: NotebookPages con pageCount (se configura por SerializedObject).</summary>
        private static void AddPages(GameObject notebook)
        {
            var pages = notebook.AddComponent<NotebookPages>();
            var so = new SerializedObject(pages);
            so.FindProperty("pageCount").intValue = NotebookPageCount;
            so.ApplyModifiedProperties();
        }

        /// <summary>
        /// Rotacion de un lienzo de pie: la cara mira a faceDirection (horizontal) y el borde de arriba se inclina
        /// leanDegrees hacia atras. El mesh mira a -Z, asi que la cara visible es -forward (como en Easel).
        /// </summary>
        private static Quaternion StandingRotation(Vector3 faceDirection, float leanDegrees)
        {
            Vector3 face = Vector3.ProjectOnPlane(faceDirection, Vector3.up).normalized;
            float lean = leanDegrees * Mathf.Deg2Rad;
            Vector3 normal = face * Mathf.Cos(lean) + Vector3.up * Mathf.Sin(lean);
            Vector3 top = Vector3.up * Mathf.Cos(lean) - face * Mathf.Sin(lean);
            return Quaternion.LookRotation(-normal, top);
        }

        /// <summary>Centro de un lienzo de pie cuyo borde de abajo (con marco) apoya en bottom.</summary>
        private static Vector3 StandingPosition(Vector3 bottom, Quaternion rotation, float height) =>
            bottom + rotation * Vector3.up * (height * 0.5f + FrameMargin);

        /// <summary>Soportes de madera (dos patas y un travesaño) detras de un lienzo de pie, con la base en el piso.</summary>
        private static void Stand(Material material, string name, Vector3 ground, Quaternion rotation)
        {
            var old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(ground, rotation);
            var b = new LowPolyMesh();
            // En el espacio del soporte el lienzo mira a -Z: las patas van detras (+Z), desde el piso hasta la espalda.
            foreach (float sx in new[] { -0.8f, 0.8f })
            {
                Vector3 bottom = new Vector3(sx, 0f, 0.6f);
                Vector3 top = new Vector3(sx, 1.4f, 0.03f);
                Vector3 axis = top - bottom;
                b.Box(Matrix4x4.TRS((bottom + top) / 2f, Quaternion.FromToRotation(Vector3.up, axis), Vector3.one), new Vector3(0.08f, axis.magnitude, 0.08f), Wood);
            }
            b.Box(Matrix4x4.TRS(new Vector3(0f, 0.5f, 0.4f), Quaternion.identity, Vector3.one), new Vector3(1.8f, 0.07f, 0.07f), DarkWood);
            root.AddComponent<MeshFilter>().sharedMesh = MapBuilder.SaveMesh(b.Build($"Stand_{name}"));
            root.AddComponent<MeshRenderer>().sharedMaterial = material;
            Undo.RegisterCreatedObjectUndo(root, name);
        }

        /// <summary>Lienzo fijo (no se agarra): pizarra, caballete, mesa.</summary>
        private static GameObject Fixed(GameObject template, Material material, string name, Vector3 position, Quaternion rotation,
            float width, float height, Dressing dressing)
        {
            CanvasSpec spec = Snap(name, ref width, ref height);
            var go = Spawn(template, name, position, rotation, width, height, spec);
            var grab = go.GetComponent<PhysicalCanvas>();
            if (grab != null) Object.DestroyImmediate(grab);
            go.GetComponent<Rigidbody>().isKinematic = true;
            var respawn = go.GetComponent<Respawnable>();
            if (respawn != null) Object.DestroyImmediate(respawn);
            Dress(go, material, dressing, width, height);
            return go;
        }

        private static void SetInkSide(GameObject go)
        {
            var canvas = go.GetComponent<DrawingCanvas>();
            if (canvas == null) return;
            var so = new SerializedObject(canvas);
            var p = so.FindProperty("emissionSide");
            if (p == null) return;
            p.enumValueIndex = (int)EmissionSide.InkSide;
            so.ApplyModifiedProperties();
        }

        // ---------------------------------------------------------------- vestidos

        /// <summary>Envoltorio que cancela la escala del lienzo: adentro se modela en metros. La cara visible mira a -Z.</summary>
        private static LowPolyMesh Begin(GameObject canvas, out Transform wrapper, string name)
        {
            var dress = new GameObject(name);
            dress.transform.SetParent(canvas.transform, false);
            Vector3 s = canvas.transform.localScale;
            dress.transform.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
            wrapper = dress.transform;
            return new LowPolyMesh();
        }

        private static void Finish(LowPolyMesh mesh, Transform wrapper, Material material, string name)
        {
            wrapper.gameObject.AddComponent<MeshFilter>().sharedMesh = MapBuilder.SaveMesh(mesh.Build(name));
            wrapper.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void Dress(GameObject canvas, Material material, Dressing dressing, float w, float h)
        {
            if (dressing == Dressing.None) return;
            var b = Begin(canvas, out Transform wrapper, "Dressing");
            switch (dressing)
            {
                case Dressing.Notebook:
                    // Tapa de cuero detras de las hojas y espiral a la izquierda.
                    b.Box(Matrix4x4.TRS(new Vector3(0f, 0f, 0.007f), Quaternion.identity, Vector3.one), new Vector3(w + 0.03f, h + 0.03f, 0.01f), Leather);
                    for (float y = -h / 2f + 0.03f; y < h / 2f - 0.02f; y += 0.045f)
                        b.Box(Matrix4x4.TRS(new Vector3(-w / 2f - 0.004f, y, 0.0f), Quaternion.identity, Vector3.one), new Vector3(0.022f, 0.012f, 0.016f), Metal);
                    break;
                case Dressing.Framed:
                    // Marco de madera: respaldo mas grande y cuatro listones que sobresalen alrededor de la hoja.
                    b.Box(Matrix4x4.TRS(new Vector3(0f, 0f, 0.012f), Quaternion.identity, Vector3.one), new Vector3(w + 0.1f, h + 0.1f, 0.02f), Wood);
                    float t = 0.045f, d = -0.012f;
                    b.Box(Matrix4x4.TRS(new Vector3(0f, h / 2f + t / 2f, d), Quaternion.identity, Vector3.one), new Vector3(w + 0.1f, t, 0.03f), DarkWood);
                    b.Box(Matrix4x4.TRS(new Vector3(0f, -h / 2f - t / 2f, d), Quaternion.identity, Vector3.one), new Vector3(w + 0.1f, t, 0.03f), DarkWood);
                    b.Box(Matrix4x4.TRS(new Vector3(-w / 2f - t / 2f, 0f, d), Quaternion.identity, Vector3.one), new Vector3(t, h, 0.03f), DarkWood);
                    b.Box(Matrix4x4.TRS(new Vector3(w / 2f + t / 2f, 0f, d), Quaternion.identity, Vector3.one), new Vector3(t, h, 0.03f), DarkWood);
                    break;
            }
            Finish(b, wrapper, material, $"Dressing_{dressing}_{canvas.name}");
        }

        // ---------------------------------------------------------------- soportes

        /// <summary>
        /// Caballete en A (dos montantes inclinados, una pata trasera y travesaños), sin base ni herramientas. Siempre es un objeto
        /// fisico pesado (Movable). Con <paramref name="slotted"/> trae un slot (CanvasEasel) para poner, sacar y cambiar el lienzo y
        /// un boton de flechas circulares que invierte el sentido de disparo. Sin slot (tutorial) el lienzo queda fijo al caballete:
        /// sus ejercicios dependen de el, pero el caballete entero se puede mover.
        /// </summary>
        internal static void Easel(GameObject template, Material material, string name, Vector3 ground, float yaw, bool slotted = true)
        {
            var old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(ground, Quaternion.Euler(0f, yaw, 0f));
            var b = new LowPolyMesh();
            // El lienzo mira a +Z. Montantes delanteros inclinados hacia atras y una pata trasera de apoyo.
            const float frontBottomZ = 0.3f, frontTopZ = -0.05f, legHeight = 1.9f, rearBottomZ = -0.95f, rearTopZ = -0.12f;
            float lean = Mathf.Atan2(frontBottomZ - frontTopZ, legHeight);
            Vector3 faceLocal = new Vector3(0f, Mathf.Sin(lean), Mathf.Cos(lean));
            Vector3 topLocal = new Vector3(0f, Mathf.Cos(lean), -Mathf.Sin(lean));
            Quaternion planeRotation = Quaternion.LookRotation(faceLocal, topLocal);
            Vector3 OnPlane(float x, float y) => new Vector3(x, y, frontBottomZ + (frontTopZ - frontBottomZ) * y / legHeight);

            void Leg(Vector3 bottom, Vector3 top, float width, float depth, Color color)
            {
                Vector3 axis = top - bottom;
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, axis);
                b.Box(Matrix4x4.TRS((bottom + top) / 2f, rot, Vector3.one), new Vector3(width, axis.magnitude, depth), color);
                var part = new GameObject("Col_Leg");
                part.transform.SetParent(root.transform, false);
                part.transform.localPosition = (bottom + top) / 2f;
                part.transform.localRotation = rot;
                part.AddComponent<BoxCollider>().size = new Vector3(width, axis.magnitude, depth);
            }
            void Board(float x, float y, Vector3 size, Color color, bool collider)
            {
                Vector3 pos = OnPlane(x, y);
                b.Box(Matrix4x4.TRS(pos, planeRotation, Vector3.one), size, color);
                if (!collider) return;
                var part = new GameObject("Col_Board");
                part.transform.SetParent(root.transform, false);
                part.transform.localPosition = pos;
                part.transform.localRotation = planeRotation;
                part.AddComponent<BoxCollider>().size = size;
            }
            Leg(new Vector3(-0.5f, 0f, frontBottomZ), new Vector3(-0.42f, legHeight, frontTopZ), 0.07f, 0.06f, Wood);
            Leg(new Vector3(0.5f, 0f, frontBottomZ), new Vector3(0.42f, legHeight, frontTopZ), 0.07f, 0.06f, Wood);
            Leg(new Vector3(0f, 0f, rearBottomZ), new Vector3(0f, legHeight - 0.1f, rearTopZ), 0.07f, 0.06f, DarkWood);
            // Travesaños: arriba, uno ancho abajo donde apoya el lienzo (lleva el boton) y un arriostre hacia la pata trasera.
            Board(0f, 1.97f, new Vector3(1.0f, 0.07f, 0.07f), DarkWood, false);
            Board(0f, 0.45f, new Vector3(1.0f, 0.22f, 0.05f), DarkWood, true);
            b.Box(Matrix4x4.TRS(new Vector3(0f, 1.0f, -0.3f), Quaternion.identity, Vector3.one), new Vector3(0.05f, 0.05f, 0.5f), DarkWood);
            var mesh = MapBuilder.SaveMesh(b.Build($"Easel_{name}"));
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = material;

            const float canvasY = 1.25f;
            Vector3 center = OnPlane(0f, canvasY) + faceLocal * 0.035f;
            Quaternion canvasRotation = Quaternion.LookRotation(-(root.transform.rotation * faceLocal), root.transform.rotation * topLocal);
            if (!slotted)
            {
                var fixedCanvas = Fixed(template, material, name + "_Canvas", root.transform.TransformPoint(center), canvasRotation, 0.9f, 1.2f, Dressing.None);
                fixedCanvas.transform.SetParent(root.transform, true);
                MovablesSetup.MakeMovable(root, EaselMass, "caballete", MovablesSetup.WoodProfile());
                Undo.RegisterCreatedObjectUndo(root, name);
                return;
            }

            // Atril con slot: lienzo L rectangular agarrable (acoplado al arrancar) y el boton de invertir el sentido.
            var canvas = Grabbable(template, material, name + "_Canvas", root.transform.TransformPoint(center), canvasRotation, 1.0f, 1.4f, Dressing.None);
            canvas.GetComponent<Rigidbody>().isKinematic = true;
            canvas.transform.SetParent(root.transform, true);
            var respawn = canvas.GetComponent<Respawnable>();
            if (respawn != null) Object.DestroyImmediate(respawn);
            var slot = new GameObject("Slot").transform;
            slot.SetParent(root.transform, false);
            slot.localPosition = center;
            slot.localRotation = Quaternion.LookRotation(faceLocal, topLocal);
            var easel = root.AddComponent<CanvasEasel>();
            easel.Configure(slot, canvas.GetComponent<PhysicalCanvas>());
            ReverseButton(root.transform, easel, OnPlane(0f, 0.45f) + faceLocal * 0.03f, planeRotation);
            // Mismas fisicas que las cajas y demas objetos pesados: rigidbody + Movable (agarrable), reaccion de la madera y reinicio.
            MovablesSetup.MakeMovable(root, EaselMass, "atril", MovablesSetup.WoodProfile());
            EditorUtility.SetDirty(easel);
            Undo.RegisterCreatedObjectUndo(root, name);
        }

        /// <summary>Masa (kg) de un caballete: pesado como una mesa.</summary>
        private const float EaselMass = 25f;

        /// <summary>
        /// Boton sobre el travesaño de abajo, mirando al frente: disco oscuro con dos flechas circulares doradas (textura generada, como el
        /// simbolo de reciclar). Invierte el sentido de disparo del atril. Collider generoso para apuntarlo.
        /// </summary>
        private static void ReverseButton(Transform easelRoot, CanvasEasel easel, Vector3 localPosition, Quaternion localRotation)
        {
            var button = new GameObject("ReverseButton");
            button.transform.SetParent(easelRoot, false);
            button.transform.localPosition = localPosition;
            button.transform.localRotation = localRotation;
            var box = button.AddComponent<BoxCollider>();
            box.size = new Vector3(0.26f, 0.26f, 0.1f);
            button.AddComponent<EaselReverseButton>().Configure(easel);
            var icon = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(icon.GetComponent<Collider>());
            icon.name = "Icon";
            icon.transform.SetParent(button.transform, false);
            icon.transform.localPosition = new Vector3(0f, 0f, 0.012f);
            icon.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // la cara visible del quad mira a -Z: se gira hacia el frente
            icon.transform.localScale = new Vector3(0.2f, 0.2f, 1f);
            icon.GetComponent<MeshRenderer>().sharedMaterial = ReverseIconMaterial();
        }

        /// <summary>Textura y material del icono de dos flechas circulares (se genera una vez y se guarda en Art/Generated).</summary>
        private static Material ReverseIconMaterial()
        {
            const string texPath = "Assets/_Project/Art/Generated/EaselReverseIcon.png";
            const string matPath = "Assets/_Project/Materials/EaselReverseIcon.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing != null && AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) != null) return existing;
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var pixels = new Color[n * n];
            Color bg = new Color(0.16f, 0.12f, 0.2f, 1f), fg = new Color(0.98f, 0.86f, 0.45f, 1f);
            const float r = 0.3f, half = 0.045f, head = 0.12f;
            // Dos arcos de 120 grados (sentido antihorario) con punta de flecha, uno frente al otro.
            float[] starts = { 25f, 205f };
            const float span = 120f;
            for (int py = 0; py < n; py++)
            for (int px = 0; px < n; px++)
            {
                float x = (px + 0.5f) / n - 0.5f, y = (py + 0.5f) / n - 0.5f;
                float dist = Mathf.Sqrt(x * x + y * y);
                Color c = dist < 0.5f ? bg : new Color(0f, 0f, 0f, 0f);
                if (dist < 0.5f)
                {
                    float ang = Mathf.Atan2(y, x) * Mathf.Rad2Deg; if (ang < 0f) ang += 360f;
                    bool on = false;
                    foreach (float s in starts)
                    {
                        float rel = ang - s; if (rel < 0f) rel += 360f;
                        if (rel <= span && Mathf.Abs(dist - r) <= half) on = true;
                        // Punta: triangulo en el extremo, apuntando en sentido antihorario (tangente).
                        float ea = (s + span) * Mathf.Deg2Rad;
                        Vector2 radial = new Vector2(Mathf.Cos(ea), Mathf.Sin(ea));
                        Vector2 tangent = new Vector2(-radial.y, radial.x);
                        Vector2 baseCenter = radial * r;
                        Vector2 a = baseCenter + radial * (half * 2.6f), bpt = baseCenter - radial * (half * 2.6f), tip = baseCenter + tangent * head;
                        if (InTriangle(new Vector2(x, y), a, bpt, tip)) on = true;
                    }
                    if (on) c = fg;
                }
                pixels[py * n + px] = c;
            }
            tex.SetPixels(pixels);
            System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath(texPath), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "EaselReverseIcon" };
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = 3000;
            m.SetFloat("_SrcBlend", 5f); m.SetFloat("_DstBlend", 10f); m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            AssetDatabase.CreateAsset(m, matPath);
            return m;
        }

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool neg = d1 < 0f || d2 < 0f || d3 < 0f, pos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(neg && pos);
        }

        private static void DraftingTable(GameObject template, Material material, string name, Vector3 ground, float yaw)
        {
            var old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(ground, Quaternion.Euler(0f, yaw, 0f));
            var b = new LowPolyMesh();
            foreach (float sx in new[] { -0.55f, 0.55f })
                foreach (float sz in new[] { -0.3f, 0.3f })
                    b.Box(Matrix4x4.TRS(new Vector3(sx, 0.45f, sz), Quaternion.identity, Vector3.one), new Vector3(0.07f, 0.9f, 0.07f), DarkWood);
            // Tablero inclinado 20 grados hacia quien dibuja (-Z) y reborde para el lapiz.
            var top = Matrix4x4.TRS(new Vector3(0f, 0.95f, 0f), Quaternion.Euler(-20f, 0f, 0f), Vector3.one);
            b.Box(top, new Vector3(1.3f, 0.05f, 0.85f), Wood);
            b.Box(top * Matrix4x4.TRS(new Vector3(0f, 0.05f, -0.4f), Quaternion.identity, Vector3.one), new Vector3(1.3f, 0.05f, 0.04f), DarkWood);
            var mesh = MapBuilder.SaveMesh(b.Build($"Drafting_{name}"));
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = material;
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.5f, 0f);
            col.size = new Vector3(1.3f, 1.0f, 0.8f);
            // Cara visible hacia arriba y hacia quien se sienta del lado -Z.
            Vector3 center = root.transform.TransformPoint(new Vector3(0f, 0.985f, 0f) + Quaternion.Euler(-20f, 0f, 0f) * new Vector3(0f, 0.032f, 0f));
            var canvas = Fixed(template, material, name + "_Canvas", center,
                Quaternion.LookRotation(-(root.transform.rotation * new Vector3(0f, Mathf.Cos(20f * Mathf.Deg2Rad), -Mathf.Sin(20f * Mathf.Deg2Rad))),
                    root.transform.rotation * new Vector3(0f, Mathf.Sin(20f * Mathf.Deg2Rad), Mathf.Cos(20f * Mathf.Deg2Rad))), 1.1f, 0.7f, Dressing.None);
            canvas.transform.SetParent(root.transform, true);
            Undo.RegisterCreatedObjectUndo(root, name);
        }
    }
}
