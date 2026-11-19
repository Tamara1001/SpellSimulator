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
                if (c.name.StartsWith("Easel_Wind") || c.name.StartsWith("Easel_Door")) continue;
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
        /// Caballete. Con <paramref name="slotted"/> es un atril con slot (CanvasEasel): el lienzo se agarra, se cambia acercando otro
        /// y se gira el atril con clic en la base. Sin slot (tutorial) el lienzo queda fijo: sus ejercicios dependen de el.
        /// </summary>
        internal static void Easel(GameObject template, Material material, string name, Vector3 ground, float yaw, bool slotted = true)
        {
            var old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(ground, Quaternion.Euler(0f, yaw, 0f));
            var b = new LowPolyMesh();
            // Tres patas (dos adelante, una atras, a espaldas del lienzo) y travesaños. El lienzo mira a +Z.
            void Leg(Vector3 bottom, Vector3 top, Color color)
            {
                Vector3 axis = top - bottom;
                b.Box(Matrix4x4.TRS((bottom + top) / 2f, Quaternion.FromToRotation(Vector3.up, axis), Vector3.one), new Vector3(0.06f, axis.magnitude, 0.05f), color);
            }
            // Patas delanteras inclinadas hacia atras (arriba mas atras que abajo) y una trasera de apoyo.
            const float frontBottomZ = 0.3f, frontTopZ = -0.05f, legHeight = 1.9f;
            Leg(new Vector3(-0.5f, 0f, frontBottomZ), new Vector3(-0.4f, legHeight, frontTopZ), Wood);
            Leg(new Vector3(0.5f, 0f, frontBottomZ), new Vector3(0.4f, legHeight, frontTopZ), Wood);
            Leg(new Vector3(0f, 0f, -0.95f), new Vector3(0f, legHeight, -0.12f), DarkWood);
            b.Box(Matrix4x4.TRS(new Vector3(0f, 0.55f, frontBottomZ + (frontTopZ - frontBottomZ) * 0.55f / legHeight + 0.06f), Quaternion.identity, Vector3.one), new Vector3(1.0f, 0.05f, 0.14f), DarkWood); // reposa-lienzo
            b.Box(Matrix4x4.TRS(new Vector3(0f, 1.95f, frontTopZ), Quaternion.identity, Vector3.one), new Vector3(0.95f, 0.05f, 0.07f), DarkWood);
            // Atril con slot: base maciza (patines) para que sea un objeto pesado estable que se puede empujar y llevar.
            if (slotted)
            {
                b.Box(Matrix4x4.TRS(new Vector3(0f, 0.06f, -0.3f), Quaternion.identity, Vector3.one), new Vector3(1.1f, 0.12f, 1.5f), DarkWood);
                b.Box(Matrix4x4.TRS(new Vector3(0f, 0.15f, -0.3f), Quaternion.identity, Vector3.one), new Vector3(0.9f, 0.06f, 1.3f), Wood);
            }
            var mesh = MapBuilder.SaveMesh(b.Build($"Easel_{name}"));
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = material;
            // El lienzo se apoya sobre el plano de las patas delanteras, inclinado lo mismo que ellas.
            float lean = Mathf.Atan2(frontBottomZ - frontTopZ, legHeight);
            Vector3 faceLocal = new Vector3(0f, Mathf.Sin(lean), Mathf.Cos(lean));
            Vector3 topLocal = new Vector3(0f, Mathf.Cos(lean), -Mathf.Sin(lean));
            const float canvasY = 1.25f;
            Vector3 onPlane = new Vector3(0f, canvasY, frontBottomZ + (frontTopZ - frontBottomZ) * canvasY / legHeight);
            Vector3 center = onPlane + faceLocal * 0.035f;
            Quaternion canvasRotation = Quaternion.LookRotation(-(root.transform.rotation * faceLocal), root.transform.rotation * topLocal);
            if (!slotted)
            {
                var fixedCanvas = Fixed(template, material, name + "_Canvas", root.transform.TransformPoint(center), canvasRotation, 0.9f, 1.2f, Dressing.None);
                fixedCanvas.transform.SetParent(root.transform, true);
                Undo.RegisterCreatedObjectUndo(root, name);
                return;
            }

            // Atril con slot: lienzo L rectangular agarrable (acoplado al arrancar), slot con la pose y base solida para girarlo.
            var canvas = Grabbable(template, material, name + "_Canvas", root.transform.TransformPoint(center), canvasRotation, 1.0f, 1.4f, Dressing.None);
            canvas.GetComponent<Rigidbody>().isKinematic = true;
            canvas.transform.SetParent(root.transform, true);
            var respawn = canvas.GetComponent<Respawnable>();
            if (respawn != null) Object.DestroyImmediate(respawn);
            var slot = new GameObject("Slot").transform;
            slot.SetParent(root.transform, false);
            slot.localPosition = center;
            slot.localRotation = Quaternion.LookRotation(faceLocal, topLocal);
            var foot = root.AddComponent<BoxCollider>();
            foot.center = new Vector3(0f, 0.09f, -0.3f);
            foot.size = new Vector3(1.1f, 0.18f, 1.5f);
            var upright = root.AddComponent<BoxCollider>(); // el bastidor, detras del plano del lienzo
            upright.center = new Vector3(0f, 1.05f, -0.3f);
            upright.size = new Vector3(0.95f, 1.9f, 0.4f);
            var easel = root.AddComponent<CanvasEasel>();
            easel.Configure(slot, canvas.GetComponent<PhysicalCanvas>());
            ReverseButton(root.transform, easel);
            // Mismas fisicas que las cajas y demas objetos pesados: rigidbody + Movable (agarrable), reaccion de la madera y reinicio.
            MovablesSetup.MakeMovable(root, EaselMass, "atril", MovablesSetup.WoodProfile());
            EditorUtility.SetDirty(easel);
            Undo.RegisterCreatedObjectUndo(root, name);
        }

        /// <summary>Masa (kg) de un atril con slot: pesado como una mesa.</summary>
        private const float EaselMass = 25f;

        /// <summary>
        /// Boton al frente de la base del atril con dos flechas circulares (como el simbolo de reciclar): invierte el sentido de disparo del atril.
        /// Armado con cubos chicos (sin colliders propios) y un collider generoso para apuntarlo.
        /// </summary>
        private static void ReverseButton(Transform easelRoot, CanvasEasel easel)
        {
            Material baseMat = CanvasFrameMaterials.Solid("EaselButton", new Color(0.18f, 0.14f, 0.22f));
            Material iconMat = CanvasFrameMaterials.Solid("EaselButtonIcon", new Color(0.95f, 0.85f, 0.5f));
            var button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = "ReverseButton";
            button.transform.SetParent(easelRoot, false);
            button.transform.localPosition = new Vector3(0f, 0.2f, 0.25f);
            button.transform.localScale = new Vector3(0.16f, 0.03f, 0.16f);
            button.GetComponent<MeshRenderer>().sharedMaterial = baseMat;
            var box = button.GetComponent<BoxCollider>();
            box.size = new Vector3(1.3f, 3f, 1.3f);
            button.AddComponent<EaselReverseButton>().Configure(easel);
            // Dos arcos de ~120 grados con punta de flecha, uno frente al otro.
            const float radius = 0.045f;
            for (int arc = 0; arc < 2; arc++)
            {
                float start = arc * 180f + 20f;
                for (int k = 0; k <= 4; k++)
                {
                    float a = (start + k * 25f) * Mathf.Deg2Rad;
                    IconBit(button.transform, iconMat, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius, -(start + k * 25f) + 90f, new Vector3(0.012f, 0.4f, 0.03f));
                }
                float tip = (start + 4 * 25f) * Mathf.Deg2Rad;
                Vector3 at = new Vector3(Mathf.Cos(tip), 0f, Mathf.Sin(tip)) * radius;
                IconBit(button.transform, iconMat, at, -(start + 100f) + 45f, new Vector3(0.012f, 0.4f, 0.035f));
                IconBit(button.transform, iconMat, at, -(start + 100f) + 135f, new Vector3(0.012f, 0.4f, 0.035f));
            }
        }

        private static void IconBit(Transform parent, Material material, Vector3 positionOnButton, float yaw, Vector3 size)
        {
            var bit = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(bit.GetComponent<Collider>());
            bit.name = "Icon";
            bit.transform.SetParent(parent, false);
            // El padre mide 0,16 x 0,03 x 0,16: se compensa para que las piezas queden con el tamaño pedido (m).
            Vector3 s = parent.localScale;
            bit.transform.localPosition = new Vector3(positionOnButton.x / s.x, 0.65f, positionOnButton.z / s.z);
            bit.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            bit.transform.localScale = new Vector3(size.x / s.x, size.y, size.z / s.z);
            bit.GetComponent<MeshRenderer>().sharedMaterial = material;
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
