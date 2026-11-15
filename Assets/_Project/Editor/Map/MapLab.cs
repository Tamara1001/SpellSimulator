using System.Globalization;
using TMPro;
using UnityEditor;
using UnityEngine;
using SpellSimulator.Casting;
using SpellSimulator.Lab;
using SpellSimulator.Output;
using SpellSimulator.Reactions;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>
    /// Genera la mesa del Laboratorio (Docs/LABORATORIO_PLAN.md, fase 2a) al este de la mesa de estampas: mesa de trabajo,
    /// 6 perillas (palanca + lectura + placas -/+), 3 botones de materializar (caja, viento, fuego), boton «valores por defecto»,
    /// soporte con las lentes de tinta, cartel con instrucciones y contadores, y los componentes de runtime (LabParameters,
    /// LabSpawner con sus anclas, LabOverlay, LabMeasurements, LabSessionLog, LabCounters).
    /// Todo cuelga de <c>Map/Lab</c> con nombres estables (<c>LabDial_Mass</c>, <c>LabPlate_Mass_Plus</c>...) porque MapLayout
    /// conserva los ajustes manuales por ruta jerarquica. Quien usa la mesa se para del lado oeste y mira al este: los experimentos
    /// (caja, viento y fuego) quedan delante, fuera del campo de practica.
    /// </summary>
    public static class MapLab
    {
        public const string RootName = "Lab";

        /// <summary>Desplazamiento de la mesa respecto de la mesa de estampas (5,5 m al este, LABORATORIO_PLAN pregunta 6).</summary>
        public static readonly Vector3 OffsetFromStampTable = new Vector3(5.5f, 0f, 0f);

        // Marco local de la mesa: yaw 90 => +Z local = este (hacia los experimentos), +X local = sur (derecha de quien la usa).
        private const float TableYaw = 90f;
        private const float TableWidth = 3f, TableDepth = 1f, TableTop = 0.82f;

        // Perillas: separacion entre modulos y posicion de las placas (separadas >= 6 cm: rayo de VR con temblor, riesgo 6).
        private const float DialSpacing = 0.48f;
        private const float PlateOffset = 0.1f, PlateWidth = 0.14f, PlateDepth = 0.12f, PlateZ = 0.05f;
        private const float PanelZ = 0.3f, PanelFront = 0.285f;
        private const float PivotY = 1.02f;

        // Fila de botones (cerca del borde de quien usa la mesa).
        private const float ButtonZ = -0.3f, ButtonWidth = 0.22f, ButtonDepth = 0.16f;

        /// <summary>Distancia minima (m) del parche de fuego a la mesa (riesgo 12: que la mesa no se queme).</summary>
        private const float MinFireDistance = 8f;
        /// <summary>Radios (m) en los que se sacan pinos y rocas: la mesa y la zona de viento quedan despejadas.</summary>
        private const float TableClearRadius = 2.5f, WindClearRadius = 3f;

        // Anclas en el marco local de la mesa. Verificadas contra el mapa (seed 1234): ninguna cae sobre un camino, el agua,
        // una estructura o un pino; el fuego queda a ~10 m de la mesa y a > 5 m del circulo de piedras.
        private static readonly Vector3 BoxAnchorLocal = new Vector3(-0.9f, 0f, 5f);      // mundo ~(40.5, -15.6)
        private static readonly Vector3 WindAnchorLocal = new Vector3(-2f, 0f, 5f);       // mundo ~(40.5, -14.5): la caja queda dentro
        private static readonly Vector3 FireAnchorLocal = new Vector3(3.5f, 0f, 9.5f);    // mundo ~(45, -20)
        private static readonly Vector3 FountainAnchorLocal = new Vector3(1.5f, 0f, 3f);  // mundo ~(38.5, -18), fase 3
        private static readonly Vector3 SignLocal = new Vector3(-1.85f, 0f, -0.2f);         // a la izquierda, sin tocar la mesa
        private static readonly Vector3 ViewerLocal = new Vector3(0f, 0f, -1f);

        private static readonly LabVariableId[] Dials =
        {
            LabVariableId.Mass, LabVariableId.WindYaw, LabVariableId.WindSpeed,
            LabVariableId.FireSeedRadius, LabVariableId.FireDecay, LabVariableId.FireMaxGenerations
        };

        private static readonly Color PanelColor = new Color(0.17f, 0.15f, 0.24f);
        private static readonly Color FrameColor = new Color(0.38f, 0.23f, 0.14f);
        private static readonly Color Brass = new Color(0.8f, 0.62f, 0.28f);
        private static readonly Color LeverTip = new Color(0.78f, 0.22f, 0.18f);
        private static readonly Color MinusColor = new Color(0.36f, 0.52f, 0.82f);
        private static readonly Color PlusColor = new Color(0.9f, 0.56f, 0.28f);
        private static readonly Color ButtonBase = new Color(0.3f, 0.2f, 0.13f);
        private static readonly Color BoxButton = new Color(0.86f, 0.72f, 0.48f);
        private static readonly Color WindButton = new Color(0.62f, 0.86f, 0.95f);
        private static readonly Color FireButton = new Color(0.95f, 0.5f, 0.25f);
        private static readonly Color ResetButton = new Color(0.62f, 0.75f, 0.55f);
        private static readonly Color LensColor = new Color(0.25f, 0.55f, 0.75f);
        private static readonly Color TextLight = new Color(0.98f, 0.93f, 0.8f);
        private static readonly Color TextGold = new Color(1f, 0.82f, 0.45f);
        private static readonly Color TextDark = new Color(0.14f, 0.08f, 0.04f);

        private const string BoxMaterialPath = "Assets/_Project/Materials/Crate.mat";
        private const string WindMaterialPath = "Assets/_Project/Materials/SpellStreak.mat";

        private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

        /// <summary>Genera la mesa del laboratorio. <paramref name="stampTable"/> = base de la mesa de estampas (sobre el piso).</summary>
        public static void Build(Transform map, Material material, Vector3 stampTable)
        {
            Vector3 center = MapBuilder.Ground(stampTable.x + OffsetFromStampTable.x, stampTable.z + OffsetFromStampTable.z);
            var root = new GameObject(RootName).transform;
            root.SetParent(map, false);
            root.SetPositionAndRotation(center, Quaternion.Euler(0f, TableYaw, 0f));

            int cleared = ClearVegetation(map, center, TableClearRadius);
            cleared += ClearVegetation(map, GroundAt(root, WindAnchorLocal), WindClearRadius);

            TowerBuilder.Table(root, material, "LabTable", Vector3.zero, TableYaw, TableWidth, TableDepth);

            var caster = SceneCaster();
            if (caster == null) Debug.LogWarning("[MapLab] no hay SpellCaster en la escena: el overlay y el registro no veran los hechizos.");

            // Perillas.
            Mesh panel = MapBuilder.SaveMesh(PanelMesh());
            Mesh lever = MapBuilder.SaveMesh(LeverMesh());
            Mesh minus = MapBuilder.SaveMesh(PlateMesh("Lab_PlateMinus", MinusColor));
            Mesh plus = MapBuilder.SaveMesh(PlateMesh("Lab_PlatePlus", PlusColor));
            for (int i = 0; i < Dials.Length; i++)
                Dial(root, material, Dials[i], (i - (Dials.Length - 1) * 0.5f) * DialSpacing, panel, lever, minus, plus);

            // Botones.
            MaterializeButton(root, material, LabSpawnKind.Box, "Materializar\ncaja", BoxButton, -0.9f);
            MaterializeButton(root, material, LabSpawnKind.Wind, "Materializar\nviento", WindButton, -0.45f);
            MaterializeButton(root, material, LabSpawnKind.Fire, "Materializar\nfuego", FireButton, 0f);
            DefaultsButton(root, material, 0.62f);

            Lenses(root, material, new Vector3(1.2f, TableTop, ButtonZ));

            // Sistemas: parametros + materializador con sus anclas.
            var systems = Child(root, "LabSystems", Vector3.zero);
            systems.AddComponent<LabParameters>();
            var spawner = systems.AddComponent<LabSpawner>();
            Transform boxAnchor = Anchor(root, systems.transform, "LabAnchor_Box", BoxAnchorLocal);
            Transform windAnchor = Anchor(root, systems.transform, "LabAnchor_Wind", WindAnchorLocal);
            Transform fireAnchor = Anchor(root, systems.transform, "LabAnchor_Fire", FireAnchorLocal);
            Transform fountainAnchor = Anchor(root, systems.transform, "LabAnchor_Fountain", FountainAnchorLocal);
            Assign(spawner, "boxAnchor", boxAnchor);
            Assign(spawner, "windAnchor", windAnchor);
            Assign(spawner, "fireAnchor", fireAnchor);
            Assign(spawner, "fountainAnchor", fountainAnchor);
            Assign(spawner, "boxMaterial", AssetDatabase.LoadAssetAtPath<Material>(BoxMaterialPath));
            Assign(spawner, "windParticleMaterial", AssetDatabase.LoadAssetAtPath<Material>(WindMaterialPath));
            CheckFireAnchor(fireAnchor.position, center);

            // Overlay de las lentes.
            var overlay = Child(root, "LabOverlay", Vector3.zero).AddComponent<LabOverlay>();
            Assign(overlay, "caster", caster);

            // Registro y mediciones.
            var recorder = Child(root, "LabRecorder", Vector3.zero);
            Assign(recorder.AddComponent<LabMeasurements>(), "caster", caster);
            Assign(recorder.AddComponent<LabSessionLog>(), "caster", caster);

            Sign(root, material);

            Debug.Log($"[MapLab] laboratorio en {center} (pinos/rocas despejados: {cleared}); fuego en {fireAnchor.position}.");
        }

        // ---------------------------------------------------------------- perillas

        private static void Dial(Transform root, Material material, LabVariableId id, float x, Mesh panel, Mesh lever, Mesh minus, Mesh plus)
        {
            LabVariableDef def = LabCatalog.Get(id);
            string key = id.ToString();
            var dialGo = Child(root, "LabDial_" + key, new Vector3(x, 0f, 0f));
            Part("Panel", dialGo.transform, panel, material, Vector3.zero, Quaternion.identity);

            // Pivote de la palanca: LabDial la gira sobre su Z local de -60 (minimo) a +60 (maximo). Con el pivote girado 180
            // grados su Z apunta hacia quien mira, asi el maximo queda a la derecha; el modelo se gira de vuelta.
            var pivot = Child(dialGo.transform, "Lever", new Vector3(0f, PivotY, PanelFront - 0.009f));
            pivot.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Part("LeverMesh", pivot.transform, lever, material, Vector3.zero, Quaternion.Euler(0f, 180f, 0f));

            var label = Text(dialGo.transform, "Label", new Vector3(0f, 1.285f, PanelFront - 0.003f), Quaternion.identity,
                new Vector2(0.42f, 0.08f), 0.34f, TextGold, def.Label);
            label.fontStyle = FontStyles.Bold;
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.12f;
            label.fontSizeMax = 0.34f;
            var readout = Text(dialGo.transform, "Readout", new Vector3(0f, 0.895f, PanelFront - 0.003f), Quaternion.identity,
                new Vector2(0.42f, 0.09f), 0.6f, TextLight, FormatValue(def.Default, def));

            var dial = dialGo.AddComponent<LabDial>();
            AssignInt(dial, "variable", (int)id);
            Assign(dial, "lever", pivot.transform);
            Assign(dial, "readout", readout);

            Plate(dialGo.transform, material, id, key, -1, minus, new Vector3(-PlateOffset, TableTop, PlateZ));
            Plate(dialGo.transform, material, id, key, 1, plus, new Vector3(PlateOffset, TableTop, PlateZ));
        }

        private static void Plate(Transform parent, Material material, LabVariableId id, string key, int dir, Mesh mesh, Vector3 local)
        {
            var go = Part($"LabPlate_{key}_{(dir > 0 ? "Plus" : "Minus")}", parent, mesh, material, local, Quaternion.identity);
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.03f, 0f);
            col.size = new Vector3(PlateWidth, 0.06f, PlateDepth);
            // El simbolo acostado sobre la placa (se lee desde arriba, con la parte de arriba hacia adelante).
            Text(go.transform, "Symbol", new Vector3(0f, 0.032f, 0f), Quaternion.Euler(90f, 0f, 0f),
                new Vector2(0.13f, 0.11f), 1f, TextDark, dir > 0 ? "+" : "-");

            var plate = go.AddComponent<LabStepPlate>();
            AssignInt(plate, "variable", (int)id);
            AssignInt(plate, "dir", dir);
            Assign(plate, "highlight", go.GetComponent<MeshRenderer>());
        }

        private static Mesh PanelMesh()
        {
            var b = new LowPolyMesh();
            float height = 0.5f, bottom = TableTop + 0.02f;
            float centerY = bottom + height * 0.5f;
            // Marco de madera detras y tablero oscuro (contraste para el texto claro).
            b.Box(Matrix4x4.Translate(new Vector3(0f, centerY, PanelZ + 0.012f)), new Vector3(0.46f, height + 0.02f, 0.02f), FrameColor);
            b.Box(Matrix4x4.Translate(new Vector3(0f, centerY, PanelZ)), new Vector3(0.44f, height, 0.03f), PanelColor);
            // Pies sobre la mesa.
            b.Box(Matrix4x4.Translate(new Vector3(-0.17f, TableTop + 0.01f, PanelZ)), new Vector3(0.06f, 0.02f, 0.1f), FrameColor);
            b.Box(Matrix4x4.Translate(new Vector3(0.17f, TableTop + 0.01f, PanelZ)), new Vector3(0.06f, 0.02f, 0.1f), FrameColor);
            // Marcas del minimo, el medio y el maximo de la palanca (-60, 0, +60 grados).
            foreach (float angle in new[] { -60f, 0f, 60f })
            {
                Quaternion rot = Quaternion.Euler(0f, 0f, angle);
                Vector3 at = new Vector3(0f, PivotY, PanelFront - 0.002f) + rot * new Vector3(0f, 0.175f, 0f);
                b.Box(Matrix4x4.TRS(at, rot, Vector3.one), new Vector3(0.012f, 0.035f, 0.004f), Brass);
            }
            return b.Build("Lab_DialPanel");
        }

        private static Mesh LeverMesh()
        {
            var b = new LowPolyMesh();
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0f, -0.002f)), new Vector3(0.036f, 0.036f, 0.012f), Brass);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.075f, -0.006f)), new Vector3(0.016f, 0.15f, 0.008f), Brass);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.155f, -0.008f)), new Vector3(0.036f, 0.036f, 0.014f), LeverTip);
            return b.Build("Lab_Lever");
        }

        private static Mesh PlateMesh(string name, Color color)
        {
            var b = new LowPolyMesh();
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.015f, 0f)), new Vector3(PlateWidth, 0.03f, PlateDepth), color * 0.8f, color);
            return b.Build(name);
        }

        // ---------------------------------------------------------------- botones

        private static void MaterializeButton(Transform root, Material material, LabSpawnKind kind, string text, Color color, float x)
        {
            var go = Button(root, material, "LabMaterialize_" + kind, "Lab_Button" + kind, text, color, x);
            var button = go.AddComponent<LabMaterializeButton>();
            AssignInt(button, "kind", (int)kind);
            Assign(button, "highlight", go.GetComponent<MeshRenderer>());
        }

        private static void DefaultsButton(Transform root, Material material, float x)
        {
            var go = Button(root, material, "LabReset", "Lab_ButtonReset", "Valores\npor defecto", ResetButton, x);
            Assign(go.AddComponent<LabResetButton>(), "highlight", go.GetComponent<MeshRenderer>());
        }

        private static GameObject Button(Transform root, Material material, string name, string meshName, string text, Color color, float x)
        {
            var b = new LowPolyMesh();
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.015f, 0f)), new Vector3(ButtonWidth, 0.03f, ButtonDepth), ButtonBase);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.045f, 0f)), new Vector3(ButtonWidth - 0.03f, 0.03f, ButtonDepth - 0.03f), color * 0.85f, color);
            Mesh mesh = MapBuilder.SaveMesh(b.Build(meshName));
            var go = Part(name, root, mesh, material, new Vector3(x, TableTop, ButtonZ), Quaternion.identity);
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.04f, 0f);
            col.size = new Vector3(ButtonWidth, 0.08f, ButtonDepth);
            var label = Text(go.transform, "Label", new Vector3(0f, 0.062f, 0f), Quaternion.Euler(90f, 0f, 0f),
                new Vector2(ButtonWidth - 0.04f, ButtonDepth - 0.04f), 0.26f, TextDark, text);
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.1f;
            label.fontSizeMax = 0.26f;
            return go;
        }

        // ---------------------------------------------------------------- lentes

        /// <summary>
        /// Soporte con las lentes de tinta (InkLenses, E/grip). El collider del soporte va en la raiz (fuera de
        /// <c>LensesVisual</c>): al ponerse las lentes el modelo se oculta pero el soporte sigue recibiendo el rayo.
        /// </summary>
        private static void Lenses(Transform root, Material material, Vector3 local)
        {
            var go = Child(root, "LabLenses", local);
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.11f, 0f);
            col.size = new Vector3(0.22f, 0.22f, 0.18f);

            var stand = new LowPolyMesh();
            stand.Box(Matrix4x4.Translate(new Vector3(0f, 0.01f, 0f)), new Vector3(0.16f, 0.02f, 0.12f), FrameColor);
            stand.Box(Matrix4x4.Translate(new Vector3(0f, 0.08f, 0f)), new Vector3(0.03f, 0.12f, 0.03f), FrameColor);
            stand.Box(Matrix4x4.Translate(new Vector3(0f, 0.145f, 0f)), new Vector3(0.15f, 0.015f, 0.03f), Brass);
            Part("Stand", go.transform, MapBuilder.SaveMesh(stand.Build("Lab_LensStand")), material, Vector3.zero, Quaternion.identity);

            var lenses = new LowPolyMesh();
            Quaternion facing = Quaternion.Euler(90f, 0f, 0f);
            foreach (float side in new[] { -0.042f, 0.042f })
            {
                lenses.Frustum(Matrix4x4.TRS(new Vector3(side, 0.035f, 0f), facing, Vector3.one), 0.036f, 0.036f, -0.004f, 0.004f, 12, Brass, true, true);
                lenses.Frustum(Matrix4x4.TRS(new Vector3(side, 0.035f, -0.001f), facing, Vector3.one), 0.03f, 0.03f, -0.005f, 0.005f, 12, LensColor, true, true);
            }
            lenses.Box(Matrix4x4.Translate(new Vector3(0f, 0.045f, 0f)), new Vector3(0.02f, 0.008f, 0.008f), Brass);
            var visual = Part("LensesVisual", go.transform, MapBuilder.SaveMesh(lenses.Build("Lab_Lenses")), material,
                new Vector3(0f, 0.15f, 0f), Quaternion.identity);

            Text(go.transform, "Label", new Vector3(0f, 0.3f, 0.05f), Quaternion.identity, new Vector2(0.34f, 0.07f), 0.3f, TextGold, "Lentes de tinta");

            var inkLenses = go.AddComponent<InkLenses>();
            Assign(inkLenses, "lensesVisual", visual);
            Assign(inkLenses, "standCollider", col);
        }

        // ---------------------------------------------------------------- cartel

        /// <summary>Cartel al costado izquierdo de la mesa (no tapa la vista de los experimentos): instrucciones y contadores.</summary>
        private static void Sign(Transform root, Material material)
        {
            Vector3 at = GroundAt(root, SignLocal);
            Quaternion rotation = root.rotation * Quaternion.LookRotation(SignLocal - ViewerLocal);

            var b = new LowPolyMesh();
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.6f, 0.05f)), new Vector3(0.07f, 1.2f, 0.07f), FrameColor);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 1.4f, 0.015f)), new Vector3(1.06f, 0.86f, 0.03f), FrameColor);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 1.4f, 0f)), new Vector3(1f, 0.8f, 0.03f), PanelColor);
            Mesh mesh = MapBuilder.SaveMesh(b.Build("Lab_Sign"));
            var go = MapBuilder.Place("LabSign", root, mesh, material, at, rotation);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.92f, 0.02f);
            col.size = new Vector3(1.06f, 1.84f, 0.1f);

            // El texto se lee desde -Z (el cartel mira lejos de quien lo lee).
            const float face = -0.018f;
            var title = Text(go.transform, "Title", new Vector3(0f, 1.73f, face), Quaternion.identity, new Vector2(0.94f, 0.1f), 0.55f, TextGold, "Laboratorio");
            title.fontStyle = FontStyles.Bold;
            var help = Text(go.transform, "Instructions", new Vector3(0f, 1.57f, face), Quaternion.identity, new Vector2(0.94f, 0.2f), 0.3f, TextLight,
                "Apunta a las placas - / + para cambiar la variable; materializa para verlo en el mundo; las lentes muestran los parámetros.");
            help.enableAutoSizing = true;
            help.fontSizeMin = 0.12f;
            help.fontSizeMax = 0.3f;

            var fire = Counter(go.transform, "FireArea", 1.4f, "Área quemada: -");
            var box = Counter(go.transform, "BoxDistance", 1.3f, "Distancia de la caja: -");
            var reach = Counter(go.transform, "CastReach", 1.2f, "Alcance del hechizo: -");
            var duration = Counter(go.transform, "CastDuration", 1.1f, "Duración del hechizo: -");

            var counters = go.AddComponent<LabCounters>();
            Assign(counters, "fireAreaText", fire);
            Assign(counters, "boxDistanceText", box);
            Assign(counters, "castReachText", reach);
            Assign(counters, "castDurationText", duration);
        }

        private static TextMeshPro Counter(Transform parent, string name, float y, string text)
        {
            var t = Text(parent, name, new Vector3(0f, y, -0.018f), Quaternion.identity, new Vector2(0.94f, 0.09f), 0.34f, TextLight, text);
            t.alignment = TextAlignmentOptions.Left;
            return t;
        }

        // ---------------------------------------------------------------- utilidades

        private static GameObject Child(Transform parent, string name, Vector3 local)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.identity;
            return go;
        }

        private static GameObject Part(string name, Transform parent, Mesh mesh, Material material, Vector3 local, Quaternion localRotation)
        {
            var go = Child(parent, name, local);
            go.transform.localRotation = localRotation;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        private static TextMeshPro Text(Transform parent, string name, Vector3 local, Quaternion localRotation, Vector2 size, float fontSize, Color color, string text)
        {
            var go = Child(parent, name, local);
            go.transform.localRotation = localRotation;
            var label = go.AddComponent<TextMeshPro>();
            label.rectTransform.sizeDelta = size;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.text = text;
            return label;
        }

        /// <summary>Ancla (Transform) en el piso bajo un punto del marco local de la mesa, con la orientacion de la mesa.</summary>
        private static Transform Anchor(Transform root, Transform parent, string name, Vector3 local)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(GroundAt(root, local), root.rotation);
            return go.transform;
        }

        private static Vector3 GroundAt(Transform root, Vector3 local)
        {
            Vector3 p = root.TransformPoint(local);
            return MapBuilder.Ground(p.x, p.z);
        }

        /// <summary>El parche de fuego tiene que caer sobre pasto (no camino ni agua) y lejos de la mesa (riesgo 12).</summary>
        private static void CheckFireAnchor(Vector3 fire, Vector3 table)
        {
            if (GrassFire.IsFireproofAt(fire))
                Debug.LogWarning($"[MapLab] el ancla del fuego {fire} cae en un camino o en el agua: el parche no prendera.");
            Vector3 d = fire - table;
            d.y = 0f;
            if (d.magnitude < MinFireDistance)
                Debug.LogWarning($"[MapLab] el ancla del fuego esta a {d.magnitude:0.0} m de la mesa (minimo {MinFireDistance} m).");
        }

        /// <summary>Saca pinos y rocas (ya generados) dentro de un radio horizontal. Devuelve cuantos saco.</summary>
        private static int ClearVegetation(Transform map, Vector3 center, float radius)
        {
            int removed = 0;
            foreach (string groupName in new[] { "Trees", "Rocks" })
            {
                Transform group = map.Find(groupName);
                if (group == null) continue;
                for (int i = group.childCount - 1; i >= 0; i--)
                {
                    Transform t = group.GetChild(i);
                    Vector3 d = t.position - center;
                    d.y = 0f;
                    if (d.magnitude >= radius) continue;
                    Object.DestroyImmediate(t.gameObject);
                    removed++;
                }
            }
            return removed;
        }

        /// <summary>El lanzador que ya usan las anotaciones de sellos (SealAnnotations); si no hay, el primero de la escena.</summary>
        private static SpellCaster SceneCaster()
        {
            var annotations = Object.FindFirstObjectByType<SealAnnotations>(FindObjectsInactive.Include);
            if (annotations != null)
            {
                var caster = new SerializedObject(annotations).FindProperty("caster").objectReferenceValue as SpellCaster;
                if (caster != null) return caster;
            }
            return Object.FindFirstObjectByType<SpellCaster>(FindObjectsInactive.Include);
        }

        private static string FormatValue(float value, LabVariableDef def)
        {
            string number = value.ToString(def.Format, Spanish);
            return string.IsNullOrEmpty(def.Unit) ? number : number + " " + def.Unit;
        }

        private static void Assign(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            if (p == null)
            {
                Debug.LogError($"[MapLab] {target.GetType().Name} no tiene el campo serializado «{property}».");
                return;
            }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignInt(Object target, string property, int value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            if (p == null)
            {
                Debug.LogError($"[MapLab] {target.GetType().Name} no tiene el campo serializado «{property}».");
                return;
            }
            p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
