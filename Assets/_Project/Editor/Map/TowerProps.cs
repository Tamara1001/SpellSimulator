using UnityEditor;
using UnityEngine;
using SpellSimulator.Core;
using SpellSimulator.Reactions;
using SpellSimulator.World;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>
    /// Cosas dentro de la torre para experimentar. Planta baja (E5): los primeros experimentos del jugador son aca, y el
    /// recorrido invita a probar primero VIENTO y despues AGUA:
    /// - Estacion de viento (sureste, a la derecha de la entrada): pizarra de pared (WallBoard_Library, ver CanvasDesigns),
    ///   mesa fija con papeles, plumas y velas apagadas, y una guirnalda de banderines con bisagra que se mueven con el viento.
    /// - Estacion de agua (oeste, bajo la ventana): pila de piedra con agua de verdad (WaterVolume), un barquito que flota,
    ///   un balde y el lienzo grande enmarcado (BigCanvas_Library) al lado.
    /// Cada estacion tiene un cartel en tablilla con el glifo a dibujar. El fuego queda para la terraza y el exterior.
    /// Ademas: mesas de trabajo con herramientas (plumas, pinceles, gomas, trapos, tinteros FIJOS y PORTATILES de las tres
    /// clases de tinta) en la biblioteca y el estudio,
    /// el taller de alquimia del segundo piso y cajas movibles. Las herramientas se clonan de las del campo.
    /// </summary>
    public static class TowerProps
    {
        private static readonly Color WoodDark = new Color(0.36f, 0.22f, 0.15f);
        private static readonly Color WoodLight = new Color(0.45f, 0.3f, 0.2f);
        private static readonly Color Glass = new Color(0.85f, 0.92f, 0.95f);
        private static readonly Color Cork = new Color(0.6f, 0.42f, 0.25f);
        private static readonly Color Paper = new Color(0.96f, 0.92f, 0.8f);
        private static readonly Color Stone = new Color(0.66f, 0.62f, 0.55f);
        private static readonly Color StoneDark = new Color(0.52f, 0.49f, 0.44f);
        private static readonly Color Iron = new Color(0.2f, 0.19f, 0.2f);

        private const string WoodPath = "Assets/_Project/Data/Materials/Madera.asset";
        private const string PaperPath = "Assets/_Project/Data/Materials/Papel.asset";

        /// <summary>Angulos (grados, 0 = este) de las estaciones de la planta baja: las usa tambien CanvasDesigns.</summary>
        public const float WindAngle = 324f, WaterAngle = 180f;

        /// <summary>Mesa de trabajo de la biblioteca (centro local, yaw, ancho, fondo): al norte del escritorio, fuera del pasillo.</summary>
        public static readonly Vector3 LibraryBench = new Vector3(-0.1f, 0f, 2.15f);
        public const float LibraryBenchWidth = 2f, LibraryBenchDepth = 0.7f;

        /// <summary>Mesa de la torre: centro local (respecto del origen), yaw, ancho (X local) y fondo (Z local).</summary>
        private readonly struct Desk
        {
            public readonly Vector3 Center;
            public readonly float Yaw;
            public readonly float Width;
            public readonly float Depth;

            public Desk(Vector3 center, float yaw, float width, float depth)
            {
                Center = center;
                Yaw = yaw;
                Width = width;
                Depth = depth;
            }
        }

        public static void Build(Transform map, Material material, Vector3 origin)
        {
            var tower = GameObject.Find("Tower");
            if (tower == null) return;
            float floor = TowerBuilder.FloorHeight;

            // Mesa de trabajo de la biblioteca: herramientas a la derecha (los cuadernos van a la izquierda, ver CanvasDesigns).
            var library = new Desk(LibraryBench, 0f, LibraryBenchWidth, LibraryBenchDepth);
            TowerBuilder.Table(tower.transform, material, "Workbench_Library", library.Center, library.Yaw, library.Width, library.Depth);
            // Los dos tinteros de la biblioteca son FIJOS (clones de la plantilla del campo); al final, dos PORTATILES.
            Tools(origin, library, new[] { "Pen", "Pen", "Brush", "Brush", "Eraser", "Rag", "Inkwell", "Inkwell",
                PortablePrefix + nameof(InkKind.Powerful), PortablePrefix + nameof(InkKind.Normal) }, 0.25f, 0.4f, material);

            // Estudio (primer piso): otra mesa con herramientas y tinta.
            var study = new Desk(new Vector3(-0.5f, floor, 2.1f), 170f, 1.8f, 0.7f);
            TowerBuilder.Table(tower.transform, material, "Workbench_Study", study.Center, study.Yaw, study.Width, study.Depth);
            Tools(origin, study, new[] { "Pen", "Brush", "Eraser", "Rag", "Inkwell",
                PortablePrefix + nameof(InkKind.Normal), PortablePrefix + nameof(InkKind.Resistant) }, 0.3f, 0f, material);

            // Segundo piso: taller de alquimia.
            Alchemy(map, tower.transform, material, origin);

            // Estaciones de la planta baja.
            var stations = new GameObject("Stations").transform;
            stations.SetParent(tower.transform, false);
            stations.position = origin;
            WindStation(map, stations, material, origin);
            WaterStation(map, stations, material, origin);

            // Cajas y baules movibles (se pueden cargar, apilar y quemar): en planta baja, bajo la rampa al norte.
            Crate(map, material, "Chest_Library_A", origin + TowerBuilder.Polar(3.9f, 80f, 0.26f), new Vector3(0.7f, 0.4f, 0.45f), -170f + 15f);
            Crate(map, material, "Crate_Library_A", origin + TowerBuilder.Polar(3.8f, 97f, 0.31f), Vector3.one * 0.5f, -10f);
            Crate(map, material, "Crate_Library_B", origin + TowerBuilder.Polar(3.8f, 97f, 0.78f), Vector3.one * 0.4f, 20f);
            Crate(map, material, "Chest_Study_A", origin + new Vector3(1.4f, floor + 0.2f, -1.6f), new Vector3(0.7f, 0.4f, 0.45f), 160f);
            Crate(map, material, "Crate_Study_A", origin + new Vector3(1.9f, floor + 0.25f, -1.0f), Vector3.one * 0.5f, 5f);
        }

        /// <summary>Plantilla de Tools: tintero PORTATIL de la clase que sigue ("InkwellPortable:Powerful"), clonado del tintero del campo.</summary>
        private const string PortablePrefix = "InkwellPortable:";

        /// <summary>
        /// Clona herramientas del campo sobre una mesa, en dos filas ordenadas. Las posiciones se calculan en el sistema
        /// local de la mesa (girado por su yaw), corridas shiftX sobre su ancho, y quedan dentro de la cubierta: en X,
        /// +-(ancho/2 - 0.1); en Z, +-(fondo/2 - 0.08). Plumas y pinceles van echados; los tinteros, parados. Los clones de "Inkwell" son FIJOS
        /// (como la plantilla); los <see cref="PortablePrefix"/> se reconfiguran como portatiles de su clase.
        /// </summary>
        private static void Tools(Vector3 origin, Desk desk, string[] templates, float spacing, float shiftX, Material material)
        {
            Vector3 deskAt = origin + desk.Center;
            float halfX = desk.Width / 2f - 0.1f;
            float halfZ = desk.Depth / 2f - 0.08f;
            int columns = Mathf.CeilToInt(templates.Length / 2f);
            float step = Mathf.Min(spacing, 2f * halfX / columns);
            Quaternion deskRotation = Quaternion.Euler(0f, desk.Yaw, 0f);
            for (int index = 0; index < templates.Length; index++)
            {
                string template = templates[index];
                bool portable = template.StartsWith(PortablePrefix);
                InkKind inkKind = InkKind.Normal;
                if (portable && !System.Enum.TryParse(template.Substring(PortablePrefix.Length), out inkKind)) continue;
                var source = GameObject.Find(portable ? "Inkwell" : template);
                if (source == null) continue;
                string baseName = portable ? "InkwellPortable" + inkKind : template;
                string name = $"{baseName}_{(int)((deskAt.y + 0.84f) * 100)}_{index}";
                var old = GameObject.Find(name);
                if (old != null) Object.DestroyImmediate(old);
                var clone = Object.Instantiate(source);
                clone.name = name;
                // Fila 0 (lado de atras) y fila 1 (lado de adelante); columnas centradas sobre la mesa.
                int row = index & 1, column = index >> 1;
                float x = Mathf.Clamp((column - (columns - 1) * 0.5f) * step + shiftX, -halfX, halfX);
                float z = (row == 0 ? -0.5f : 0.5f) * halfZ;
                // Plumas, pinceles y demas quedan echados; los tinteros, parados (sin inclinacion en X ni Z).
                // Los fijos son kinematicos: van a ras de la cubierta (0.82 m, TowerBuilder.Table); lo demas cae un poco.
                bool upright = template == "Inkwell" || portable;
                clone.transform.SetPositionAndRotation(
                    deskAt + deskRotation * new Vector3(x, 0f, z) + new Vector3(0f, template == "Inkwell" ? 0.82f : 0.84f + (upright ? 0.01f : 0.04f), 0f),
                    Quaternion.Euler(0f, desk.Yaw + 90f + index * 15f, upright ? 0f : 90f));
                if (portable) ToolModels.MakeInkwell(clone, material, inkKind, true);
                var respawn = clone.GetComponent<Respawnable>();
                if (respawn != null) Object.DestroyImmediate(respawn);
                clone.AddComponent<Respawnable>();
                Undo.RegisterCreatedObjectUndo(clone, name);
            }
        }

        // ---------------------------------------------------------------- estacion de viento

        /// <summary>
        /// Sureste de la planta baja, a la derecha de quien entra. La pizarra de pared (CanvasDesigns, WallBoard_Library)
        /// emite el viento hacia el centro: barre la mesa de abajo (papeles, plumas, velas apagadas) y hace flamear la
        /// guirnalda de banderines colgada sobre la pizarra.
        /// </summary>
        private static void WindStation(Transform map, Transform stations, Material material, Vector3 origin)
        {
            float a = WindAngle;
            Vector3 inward = -TowerBuilder.Polar(1f, a, 0f);
            Vector3 tangent = TowerBuilder.Polar(1f, a + 90f, 0f);
            Vector3 tableLocal = TowerBuilder.Polar(4.45f, a, 0f);
            TowerBuilder.Table(stations, material, "WindTable", tableLocal, TowerBuilder.TangentYaw(a), 1.4f, 0.5f);
            Vector3 top = origin + tableLocal + Vector3.up * 0.825f;
            Vector3 On(float lateral, float toCenter) => top + tangent * lateral + inward * toCenter;
            float yaw = TowerBuilder.TangentYaw(a);

            // Velas apagadas en las puntas, tres hojas sueltas al medio y tres plumas adelante.
            MovablesSetup.Prop(map, material, "Candle_Wind_A", MovablesSetup.Kind.Candle, On(-0.56f, 0f), 0f);
            MovablesSetup.Prop(map, material, "Candle_Wind_B", MovablesSetup.Kind.Candle, On(0.56f, -0.05f), 0f);
            for (int i = 0; i < 3; i++)
                LightProp(map, material, $"WindSheet_{i}", LooseSheet(), On(-0.26f + i * 0.26f, -0.05f), yaw + 90f + (i - 1) * 9f,
                    new Vector3(0.29f, 0.008f, 0.21f), new Vector3(0f, 0.003f, 0f), 0.02f, "hoja suelta", PaperPath, 0.7f);
            for (int i = 0; i < 3; i++)
                LightProp(map, material, $"Feather_{i}", Feather(i), On(-0.28f + i * 0.28f, 0.17f), yaw + (i - 1) * 20f,
                    new Vector3(0.24f, 0.012f, 0.05f), new Vector3(0f, 0.006f, 0f), 0.01f, "pluma", PaperPath, 0.8f);

            Bunting(stations, material, origin, a);
            Sign(stations, material, "Sign_WindStation", origin + TowerBuilder.Polar(3.7f, 306f, 0.05f), origin + new Vector3(0f, 0f, -1.5f),
                "1 · Estación de viento", "Dibujá el sigilo <b>Viento</b>\nen la pizarra y mirá volar\npapeles, plumas y banderines.");
        }

        /// <summary>Guirnalda sobre la pizarra: soga entre dos ganchos y cinco banderines con bisagra (reaccionan al viento).</summary>
        private static void Bunting(Transform stations, Material material, Vector3 origin, float angle)
        {
            float face = TowerBuilder.InnerRadius * Mathf.Cos(9f * Mathf.Deg2Rad);
            Vector3 outward = TowerBuilder.Polar(1f, angle, 0f);
            Quaternion rotation = Quaternion.LookRotation(outward);   // +Z hacia el muro, +X a lo largo del muro
            Vector3 Wall(float lateral, float y, float off) => origin + outward * (face - off) + rotation * new Vector3(lateral, 0f, 0f) + Vector3.up * y;

            // Soga con caida suave (cuatro tramos) y ganchos clavados en el muro.
            const float y0 = 2.72f, sag = 0.08f, half = 0.72f;
            var rope = new LowPolyMesh();
            Vector3 L(float t) => Wall(Mathf.Lerp(-half, half, t), y0 - sag * 4f * t * (1f - t), 0.1f) - origin;
            for (int i = 0; i < 4; i++)
            {
                Vector3 p0 = L(i / 4f), p1 = L((i + 1) / 4f);
                rope.Box(Matrix4x4.TRS((p0 + p1) * 0.5f, Quaternion.LookRotation(p1 - p0), Vector3.one), new Vector3(0.015f, 0.015f, (p1 - p0).magnitude + 0.01f), new Color(0.75f, 0.65f, 0.45f));
            }
            foreach (float s in new[] { -half, half })
                rope.Box(Matrix4x4.TRS(Wall(s, y0, 0.05f) - origin, rotation, Vector3.one), new Vector3(0.03f, 0.03f, 0.1f), Iron);
            var ropeMesh = MapBuilder.SaveMesh(rope.Build("BuntingRope"));
            MapBuilder.Place("BuntingRope", stations, ropeMesh, material, origin, Quaternion.identity).isStatic = true;

            Color[] colors = { new Color(0.75f, 0.2f, 0.16f), new Color(0.92f, 0.7f, 0.25f), new Color(0.2f, 0.5f, 0.55f), new Color(0.95f, 0.9f, 0.78f), new Color(0.75f, 0.2f, 0.16f) };
            for (int i = 0; i < colors.Length; i++)
            {
                float t = (i + 0.5f) / colors.Length;
                Vector3 at = L(t) + origin + Vector3.down * 0.012f;
                var b = new LowPolyMesh();
                Vector3 l = new Vector3(-0.11f, 0f, 0f), r = new Vector3(0.11f, 0f, 0f), tip = new Vector3(0f, -0.28f, 0f);
                b.Tri(l, tip, r, colors[i]);
                b.Tri(r, tip, l, colors[i] * 0.85f);
                var mesh = MapBuilder.SaveMesh(b.Build($"Pennant_{i}"));
                var go = MapBuilder.Place($"Pennant_{i}", stations, mesh, material, at, rotation);
                var col = go.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, -0.11f, 0f);
                col.size = new Vector3(0.2f, 0.22f, 0.01f);
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 0.03f;
                rb.linearDamping = 0.8f;
                rb.angularDamping = 0.6f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                // Bisagra a lo largo de la soga (anclada al mundo): cuelgan quietos y se levantan con el viento.
                var hinge = go.AddComponent<HingeJoint>();
                hinge.anchor = Vector3.zero;
                hinge.axis = Vector3.right;
                hinge.useLimits = true;
                hinge.limits = new JointLimits { min = -85f, max = 85f };
            }
        }

        private static LowPolyMesh LooseSheet()
        {
            var b = new LowPolyMesh();
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.003f, 0f)), new Vector3(0.29f, 0.006f, 0.21f), Paper);
            // Renglones de tinta.
            for (int k = 0; k < 4; k++)
                b.Box(Matrix4x4.Translate(new Vector3(-0.03f + (k & 1) * 0.02f, 0.0065f, -0.06f + k * 0.04f)), new Vector3(0.18f, 0.001f, 0.008f), new Color(0.3f, 0.25f, 0.35f));
            return b;
        }

        private static LowPolyMesh Feather(int variant)
        {
            var b = new LowPolyMesh();
            Color[] vanes = { new Color(0.95f, 0.93f, 0.88f), new Color(0.55f, 0.4f, 0.3f), new Color(0.3f, 0.45f, 0.55f) };
            // Canon y barbas (dos rombos chatos a los lados), echada sobre la mesa.
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.004f, 0f)), new Vector3(0.24f, 0.006f, 0.008f), new Color(0.85f, 0.8f, 0.65f));
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 a = new Vector3(-0.07f, 0.006f, 0f), c = new Vector3(0.12f, 0.006f, 0f), m = new Vector3(0.03f, 0.007f, side * 0.025f);
                if (side > 0f) { b.Tri(a, m, c, vanes[variant % vanes.Length]); }
                else { b.Tri(a, c, m, vanes[variant % vanes.Length] * 0.9f); }
            }
            return b;
        }

        /// <summary>Objeto liviano movible (hoja, pluma, barquito, balde): Rigidbody, Movable, reaccion de material y Respawnable.</summary>
        private static GameObject LightProp(Transform parent, Material material, string name, LowPolyMesh mesh, Vector3 position, float yaw,
            Vector3 colliderSize, Vector3 colliderCenter, float mass, string label, string profilePath, float airDrag)
        {
            var old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
            var saved = MapBuilder.SaveMesh(mesh.Build($"Prop_{name}"));
            var go = MapBuilder.Place(name, parent, saved, material, position, Quaternion.Euler(0f, yaw, 0f));
            var col = go.AddComponent<BoxCollider>();
            col.size = colliderSize;
            col.center = colliderCenter;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.linearDamping = airDrag;
            rb.angularDamping = Mathf.Min(1f, airDrag * 0.4f);   // livianos: giran y revolotean
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var movable = go.AddComponent<Movable>();
            var so = new SerializedObject(movable);
            so.FindProperty("objectName").stringValue = label;
            so.ApplyModifiedProperties();
            var profile = AssetDatabase.LoadAssetAtPath<MaterialProfile>(profilePath);
            if (profile != null)
            {
                var reactive = go.AddComponent<ElementReactive>();
                var rso = new SerializedObject(reactive);
                rso.FindProperty("profile").objectReferenceValue = profile;
                rso.ApplyModifiedProperties();
            }
            go.AddComponent<Respawnable>();
            Undo.RegisterCreatedObjectUndo(go, name);
            return go;
        }

        // ---------------------------------------------------------------- estacion de agua

        /// <summary>
        /// Oeste de la planta baja, bajo la ventana y enfrente de quien entra (detras del atril): pila de piedra con agua
        /// real (flota, moja, salpica), un barquito y un balde. El lienzo grande enmarcado esta al lado (CanvasDesigns).
        /// </summary>
        private static void WaterStation(Transform map, Transform stations, Material material, Vector3 origin)
        {
            float a = WaterAngle;
            Vector3 basinAt = origin + TowerBuilder.Polar(3.85f, a, 0.05f);
            const float outer = 0.56f, inner = 0.46f, height = 0.78f, bottom = 0.26f, water = 0.66f;
            var b = new LowPolyMesh();
            b.Frustum(Matrix4x4.identity, outer + 0.04f, outer + 0.04f, 0f, 0.12f, 8, StoneDark, true, false);        // base
            b.Frustum(Matrix4x4.identity, outer - 0.04f, outer, 0.12f, height - 0.08f, 8, Stone, false);            // cuerpo
            b.Frustum(Matrix4x4.identity, outer + 0.04f, outer + 0.04f, height - 0.08f, height, 8, StoneDark, false); // borde
            // Labio superior (anillo entre el borde de adentro y el de afuera) y paredes interiores mirando hacia adentro.
            for (int i = 0; i < 8; i++)
            {
                float a0 = i * 45f, a1 = (i + 1) * 45f;
                Vector3 P(float r, float ang, float y) => new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad) * r, y, Mathf.Sin(ang * Mathf.Deg2Rad) * r);
                b.Quad(P(inner, a0, height), P(inner, a1, height), P(outer + 0.04f, a1, height), P(outer + 0.04f, a0, height), StoneDark * 1.08f);
                b.Quad(P(inner, a1, bottom), P(inner, a1, height), P(inner, a0, height), P(inner, a0, bottom), StoneDark * 0.8f);
            }
            b.Frustum(Matrix4x4.identity, inner, inner, bottom - 0.01f, bottom, 8, StoneDark * 0.7f, true, false);    // fondo
            // Caño de bronce en el muro, sobre la pila.
            Vector3 toWall = TowerBuilder.Polar(1f, a, 0f);
            // El caño entra al muro (cara interior a ~0.99 m del centro de la pila) y la boca cae sobre el agua.
            b.Box(Matrix4x4.TRS(toWall * 0.73f + Vector3.up * 1.15f, Quaternion.LookRotation(toWall), Vector3.one), new Vector3(0.06f, 0.06f, 0.62f), new Color(0.7f, 0.5f, 0.25f));
            b.Box(Matrix4x4.TRS(toWall * 0.44f + Vector3.up * 1.1f, Quaternion.LookRotation(toWall), Vector3.one), new Vector3(0.05f, 0.12f, 0.05f), new Color(0.7f, 0.5f, 0.25f));
            var mesh = MapBuilder.SaveMesh(b.Build("WaterBasin"));
            var basin = MapBuilder.Place("WaterBasin", stations, mesh, material, basinAt, Quaternion.identity);
            basin.isStatic = true;
            // Colisiones: fondo y cuatro paredes (cuadrado inscripto): lo que cae adentro queda en el agua.
            void Box(Vector3 center, Vector3 size)
            {
                var c = basin.AddComponent<BoxCollider>();
                c.center = center;
                c.size = size;
            }
            Box(new Vector3(0f, bottom / 2f, 0f), new Vector3(outer * 1.6f, bottom, outer * 1.6f));
            const float wallT = 0.1f, side = 0.45f;
            Box(new Vector3(side, height / 2f, 0f), new Vector3(wallT, height, side * 2f + wallT));
            Box(new Vector3(-side, height / 2f, 0f), new Vector3(wallT, height, side * 2f + wallT));
            Box(new Vector3(0f, height / 2f, side), new Vector3(side * 2f + wallT, height, wallT));
            Box(new Vector3(0f, height / 2f, -side), new Vector3(side * 2f + wallT, height, wallT));

            // Superficie del agua (material del lago) y su volumen.
            var w = new LowPolyMesh();
            w.Frustum(Matrix4x4.identity, inner - 0.005f, inner - 0.005f, water - 0.002f, water, 8, Color.white, true, false);
            var waterMesh = MapBuilder.SaveMesh(w.Build("WaterBasinSurface"));
            var waterMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Water.mat");
            var surface = MapBuilder.Place("WaterBasinSurface", basin.transform, waterMesh, waterMat, basinAt, Quaternion.identity);
            surface.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var volume = new GameObject("WaterBasinVolume");
            volume.transform.SetParent(basin.transform, false);
            var box = volume.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, (water + bottom) / 2f, 0f);
            box.size = new Vector3(side * 2f - wallT, water - bottom, side * 2f - wallT);
            var waterVolume = volume.AddComponent<WaterVolume>();
            var so = new SerializedObject(waterVolume);
            so.FindProperty("surface").objectReferenceValue = surface.GetComponent<MeshRenderer>();
            so.FindProperty("output").objectReferenceValue = Object.FindFirstObjectByType<Output.SpellOutputController>();
            so.ApplyModifiedProperties();

            // Barquito de madera flotando en la pila y un balde en el piso, al lado.
            var boat = new LowPolyMesh();
            boat.Box(Matrix4x4.Translate(new Vector3(0f, 0.03f, 0f)), new Vector3(0.1f, 0.05f, 0.22f), WoodLight);
            boat.Frustum(Matrix4x4.TRS(new Vector3(0f, 0.03f, 0.11f), Quaternion.Euler(90f, 0f, 0f), Vector3.one), 0.05f, 0f, 0f, 0.06f, 4, WoodLight, false);
            boat.Box(Matrix4x4.Translate(new Vector3(0f, 0.13f, 0f)), new Vector3(0.012f, 0.18f, 0.012f), WoodDark);
            Vector3 s0 = new Vector3(0f, 0.08f, -0.005f), s1 = new Vector3(0f, 0.21f, -0.005f), s2 = new Vector3(0f, 0.08f, -0.09f);
            boat.Tri(s0, s1, s2, Paper);
            boat.Tri(s1, s0, s2, Paper * 0.9f);
            LightProp(map, material, "ToyBoat", boat, basinAt + new Vector3(0.05f, water - 0.02f, -0.08f), 35f,
                new Vector3(0.1f, 0.06f, 0.24f), new Vector3(0f, 0.03f, 0f), 0.25f, "barquito", WoodPath, 0.3f);

            var bucket = new LowPolyMesh();
            bucket.Frustum(Matrix4x4.identity, 0.13f, 0.16f, 0f, 0.28f, 8, WoodLight, false, true);
            bucket.Frustum(Matrix4x4.Translate(new Vector3(0f, 0.05f, 0f)), 0.138f, 0.14f, 0f, 0.03f, 8, Iron, false);
            bucket.Frustum(Matrix4x4.Translate(new Vector3(0f, 0.22f, 0f)), 0.152f, 0.155f, 0f, 0.03f, 8, Iron, false);
            bucket.Frustum(Matrix4x4.identity, 0.145f, 0.145f, 0.2f, 0.21f, 8, new Color(0.35f, 0.55f, 0.7f), true, false);
            LightProp(map, material, "Bucket_Water", bucket, origin + TowerBuilder.Polar(3.35f, 194f, 0.05f), 0f,
                new Vector3(0.3f, 0.28f, 0.3f), new Vector3(0f, 0.14f, 0f), 1.5f, "balde", WoodPath, 0.1f);

            Sign(stations, material, "Sign_WaterStation", origin + TowerBuilder.Polar(3.45f, 160f, 0.05f), origin + new Vector3(0f, 0f, -1.5f),
                "2 · Estación de agua", "Dibujá el sigilo <b>Agua</b>\nen la pizarra del muro: el chorro\ncae sobre la pila y mueve el barquito.\n<size=75%>Ojo: el agua borra la tinta.</size>");
        }

        // ---------------------------------------------------------------- carteles

        /// <summary>Cartel en tablilla sobre un poste (como los de las zonas de practica), legible desde viewer.</summary>
        private static void Sign(Transform parent, Material material, string name, Vector3 at, Vector3 viewer, string title, string hint)
        {
            var old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
            var b = new LowPolyMesh();
            b.Frustum(Matrix4x4.identity, 0.16f, 0.13f, 0f, 0.06f, 6, WoodDark, true, false);
            // Poste detras de la tablilla (no tapa el texto) y tablilla con marco.
            b.Box(Matrix4x4.Translate(new Vector3(0f, 0.78f, 0.07f)), new Vector3(0.08f, 1.56f, 0.08f), WoodDark);
            b.Box(Matrix4x4.Translate(new Vector3(0f, 1.3f, 0f)), new Vector3(1.05f, 0.66f, 0.05f), new Color(0.78f, 0.62f, 0.4f));
            b.Box(Matrix4x4.Translate(new Vector3(0f, 1.3f, 0.006f)), new Vector3(1.12f, 0.73f, 0.04f), WoodDark);
            var mesh = MapBuilder.SaveMesh(b.Build($"Sign_{name}"));
            Vector3 look = viewer - at;
            look.y = 0f;
            // El texto se lee desde -Z: el cartel mira "hacia afuera" de quien lo lee (forward = -viewer).
            var go = MapBuilder.Place(name, parent, mesh, material, at, Quaternion.LookRotation(-look.normalized));
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.83f, 0.03f);
            col.size = new Vector3(1.1f, 1.66f, 0.14f);
            go.isStatic = true;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.3f, -0.03f);
            var label = labelGo.AddComponent<TMPro.TextMeshPro>();
            label.rectTransform.sizeDelta = new Vector2(0.98f, 0.6f);
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.2f;
            label.fontSizeMax = 0.6f;
            label.color = new Color(0.14f, 0.08f, 0.04f);
            label.text = $"<b>{title}</b>\n{hint}";
        }

        // ---------------------------------------------------------------- alquimia

        /// <summary>
        /// Taller de alquimia en el segundo piso (y = 2 * FloorHeight). Todo cae en el area central libre (radio menor
        /// que 2.6 desde el eje): la rampa y el hueco de la escalera viven en el anillo exterior (radio desde 2.9).
        /// </summary>
        private static void Alchemy(Transform map, Transform tower, Material material, Vector3 origin)
        {
            float y = TowerBuilder.FloorHeight * 2f;
            float top = y + 0.82f;   // cubierta de la mesa (ver TowerBuilder.Table)

            // Mesa grande, dos sillas enfrentadas (norte y sur) y estante exento contra el oeste (libros hacia el centro).
            TowerBuilder.Table(tower, material, "AlchemyTable", new Vector3(0f, y, 0f), 0f, 2.2f, 1f);
            TowerBuilder.Chair(tower, material, new Vector3(0f, y, 1f), 180f);
            TowerBuilder.Chair(tower, material, new Vector3(0f, y, -1f), 0f);
            TowerBuilder.Shelf(tower, material, 180f, y, 2.15f);

            // Baules y cajas movibles; la caja apilada queda sobre el segundo baul.
            Crate(map, material, "Chest_Tower2_A", origin + new Vector3(-1.2f, y + 0.2f, -1.6f), new Vector3(0.7f, 0.4f, 0.45f), 30f);
            Crate(map, material, "Chest_Tower2_B", origin + new Vector3(1.5f, y + 0.2f, -1f), new Vector3(0.7f, 0.4f, 0.45f), -20f);
            Crate(map, material, "Crate_Tower2_B", origin + new Vector3(1.5f, y + 0.6f, -1f), Vector3.one * 0.4f, 20f);
            Crate(map, material, "Crate_Tower2_A", origin + new Vector3(1.5f, y + 0.25f, 0.3f), Vector3.one * 0.5f, -10f);

            var b = new LowPolyMesh();

            // Alfombra circular chata (3 cm) bajo la mesa y las sillas.
            b.Frustum(Matrix4x4.Translate(new Vector3(0f, y + 0.01f, 0f)), 2.1f, 2.1f, 0f, 0.03f, 24, new Color(0.5f, 0.22f, 0.18f));

            // Atril con libro abierto (decorativo), en el lado noreste.
            var lectern = Matrix4x4.TRS(new Vector3(0.95f, y, 1.65f), Quaternion.Euler(0f, 240f, 0f), Vector3.one);
            b.Frustum(lectern, 0.25f, 0.12f, 0f, 0.1f, 6, WoodDark);
            b.Box(lectern * Matrix4x4.Translate(new Vector3(0f, 0.6f, 0f)), new Vector3(0.1f, 1.1f, 0.1f), WoodDark);
            var desk = lectern * Matrix4x4.TRS(new Vector3(0f, 1.15f, 0f), Quaternion.Euler(-25f, 0f, 0f), Vector3.one);
            b.Box(desk, new Vector3(0.6f, 0.04f, 0.45f), WoodLight);
            b.Box(desk * Matrix4x4.TRS(new Vector3(-0.14f, 0.04f, 0f), Quaternion.Euler(0f, 0f, 6f), Vector3.one), new Vector3(0.26f, 0.025f, 0.36f), Paper);
            b.Box(desk * Matrix4x4.TRS(new Vector3(0.14f, 0.04f, 0f), Quaternion.Euler(0f, 0f, -6f), Vector3.one), new Vector3(0.26f, 0.025f, 0.36f), Paper);

            // Matraces y frascos de colores sobre la mesa.
            Flask(b, new Vector3(-0.7f, top, 0.15f), 0.075f, new Color(0.25f, 0.7f, 0.35f));
            Flask(b, new Vector3(-0.42f, top, -0.2f), 0.09f, new Color(0.3f, 0.45f, 0.9f));
            Flask(b, new Vector3(-0.1f, top, 0.22f), 0.06f, new Color(0.95f, 0.75f, 0.2f));
            Flask(b, new Vector3(0.28f, top, 0.05f), 0.07f, new Color(0.85f, 0.25f, 0.2f));

            // Gradilla con cuatro probetas y un vaso de precipitados.
            b.Box(Matrix4x4.Translate(new Vector3(0.6f, top + 0.02f, -0.3f)), new Vector3(0.42f, 0.04f, 0.12f), WoodDark);
            Color[] tubes = { new Color(0.85f, 0.25f, 0.2f), new Color(0.95f, 0.75f, 0.2f), new Color(0.25f, 0.7f, 0.35f), new Color(0.3f, 0.45f, 0.9f) };
            for (int i = 0; i < tubes.Length; i++)
                b.Frustum(Matrix4x4.Translate(new Vector3(0.47f + i * 0.09f, top + 0.04f, -0.3f)), 0.02f, 0.02f, 0f, 0.14f, 8, tubes[i], true, true);
            b.Frustum(Matrix4x4.Translate(new Vector3(-0.1f, top, -0.3f)), 0.045f, 0.045f, 0f, 0.1f, 10, new Color(0.4f, 0.8f, 0.9f), true, true);

            // Un solo mesh decorativo en coordenadas locales de la torre (el origen es la posicion de la torre).
            var mesh = MapBuilder.SaveMesh(b.Build("TowerAlchemy"));
            MapBuilder.Place("TowerAlchemy", tower, mesh, material, origin, Quaternion.identity).isStatic = true;
        }

        /// <summary>Matraz de liquido de color: esfera, cuello de vidrio y tapon.</summary>
        private static void Flask(LowPolyMesh b, Vector3 at, float radius, Color liquid)
        {
            Sphere(b, at + Vector3.up * radius, radius, liquid);
            b.Frustum(Matrix4x4.Translate(at + Vector3.up * (radius * 1.8f)), radius * 0.3f, radius * 0.3f, 0f, radius * 1.2f, 8, Glass);
            b.Frustum(Matrix4x4.Translate(at + Vector3.up * (radius * 3f)), radius * 0.35f, radius * 0.35f, 0f, radius * 0.25f, 8, Cork);
        }

        /// <summary>Esfera low-poly (4 franjas, 8 meridianos) con las caras hacia afuera.</summary>
        private static void Sphere(LowPolyMesh b, Vector3 center, float radius, Color color)
        {
            const int lat = 4, lon = 8;
            Vector3 P(int i, int j)
            {
                float theta = Mathf.PI * i / lat, phi = 2f * Mathf.PI * j / lon;
                return center + new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi)) * radius;
            }
            for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                    b.Quad(P(i, j), P(i, j + 1), P(i + 1, j + 1), P(i + 1, j), color);
        }

        /// <summary>Caja de madera movible (copia de las del campo de practica: perfil de material, audio y reacciones).</summary>
        private static void Crate(Transform map, Material material, string name, Vector3 position, Vector3 scale, float yaw)
        {
            var source = GameObject.Find("Crate_0");
            if (source == null) return;
            var old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
            var clone = Object.Instantiate(source);
            clone.name = name;
            clone.transform.SetParent(map, true);
            clone.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            clone.transform.localScale = scale;
            var rb = clone.GetComponent<Rigidbody>();
            if (rb != null) rb.mass = Mathf.Max(0.5f, scale.x * scale.y * scale.z * 16f);
            var respawn = clone.GetComponent<Respawnable>();
            if (respawn != null) Object.DestroyImmediate(respawn);
            clone.AddComponent<Respawnable>();
            Undo.RegisterCreatedObjectUndo(clone, name);
        }
    }
}
