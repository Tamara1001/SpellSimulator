using TMPro;
using UnityEditor;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Core;
using SpellSimulator.World;
using ValleyShape = SpellSimulator.World.Map.ValleyShape;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>
    /// Cuatro puestos al aire libre en la entrada del Archivo (Docs/Archivo/ARCHIVO_PLAN.md): la «Tienda de plumas» (cinco lapiceros con plumas
    /// reales de cada tamaño), el «Almacen de lienzos» (nueve pilas de lienzos reales), el puesto de tintas (frascos portatiles) y el de sellos
    /// (estampas S, M y L). Todo lo que se exhibe es un objeto real que se agarra y se repone (<see cref="DisplayStock"/>, <see cref="CanvasPile"/>).
    /// La logica de sacar pinceles y lienzos a medida sigue en un solo <see cref="CanvasRestocker"/> (raiz «Restocker»). Armado con primitivas; el jugador se para del lado -Z de cada puesto
    /// (marco local: frente -Z, fondo +Z) y los puestos se giran para mirar hacia la torre.
    /// </summary>
    public static class MapRestocker
    {
        private const string RootName = "Restocker";
        private static readonly Quaternion Lying = Quaternion.Euler(90f, 0f, 0f);

        /// <summary>Centro del Archivo en el mundo (XZ). Ver ValleyShape (Archivo r 9) y Docs/MAPA_FLUJO_PLAN.md §18.</summary>
        public static readonly Vector2 ArchiveCenter = ValleyShape.ArchiveCenter;
        /// <summary>Posicion de cada puesto respecto del centro del Archivo (x, z mundo): al lado de la torre (+Z), a la entrada.</summary>
        private static readonly Vector2 PenShopLocal = new Vector2(-7.5f, 5.5f);
        private static readonly Vector2 CanvasShopLocal = new Vector2(7.5f, 5.5f);
        /// <summary>Los dos puestos de adentro (tintas y sellos): mas cerca de la torre y menos girados, en arco con los de afuera.</summary>
        private static readonly Vector2 InkShopLocal = new Vector2(-2.6f, 6.6f);
        private static readonly Vector2 StampShopLocal = new Vector2(2.6f, 6.6f);
        private const float InnerShopYaw = 12f;
        /// <summary>Guinada extra (grados) sobre el giro base de 180: con 0 el frente del puesto mira a +Z mundo (hacia la torre, de donde llega el jugador).</summary>
        private const float ShopYaw = 25f;

        private const float CounterTop = 0.93f;

        private static TMP_FontAsset SignFont => SpellSimulator.EditorTools.FontBuilder.LoadBookFont();

        private struct Palette
        {
            public Material wood, darkWood, plank, metal, brass, cream, board, cloudA, cloudB, lantern, leather, teal, green, blue;
        }

        public static void Build(Transform map, Vector3 towerOrigin)
        {
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            var paper = GameObject.Find("PaperCanvas");
            PhysicalTool pen = null;
            foreach (var t in Object.FindObjectsByType<PhysicalTool>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.Kind == PhysicalTool.ToolKind.Pen && t.GetComponentInParent<CanvasRestocker>() == null) { pen = t; break; }
            if (paper == null || pen == null)
            {
                Debug.LogWarning("[MapRestocker] faltan PaperCanvas o una pluma de plantilla: no se arman los puestos.");
                return;
            }

            var p = new Palette
            {
                wood = CanvasFrameMaterials.Solid("ShopWoodV2", new Color(0.46f, 0.31f, 0.19f)),   // madera del Archivo (columnas y vigas), no mas amarillenta
                darkWood = CanvasFrameMaterials.Solid("ShopDarkWoodV2", new Color(0.30f, 0.20f, 0.12f)),
                plank = CanvasFrameMaterials.Solid("ShopPlankV2", new Color(0.58f, 0.42f, 0.26f)),
                metal = CanvasFrameMaterials.Solid("ShopMetal", new Color(0.62f, 0.62f, 0.66f)),
                brass = CanvasFrameMaterials.Solid("ShopBrass", new Color(0.86f, 0.66f, 0.25f)),
                cream = CanvasFrameMaterials.Solid("ShopCream", new Color(0.95f, 0.9f, 0.76f)),
                board = CanvasFrameMaterials.Solid("ShopBoard", new Color(0.16f, 0.26f, 0.22f)),
                cloudA = CanvasFrameMaterials.Solid("ShopClothRed", new Color(0.72f, 0.24f, 0.28f)),
                cloudB = CanvasFrameMaterials.Solid("ShopClothCreamV2", new Color(0.95f, 0.91f, 0.80f)),
                lantern = CanvasFrameMaterials.Solid("ShopLantern", new Color(1f, 0.82f, 0.4f)),
                leather = CanvasFrameMaterials.Solid("CanvasCoverLeather", new Color(0.36f, 0.2f, 0.26f)),
                teal = CanvasFrameMaterials.Solid("ShopTeal", new Color(0.2f, 0.55f, 0.62f)),
                green = CanvasFrameMaterials.Solid("ShopGreen", new Color(0.3f, 0.66f, 0.36f)),
                blue = CanvasFrameMaterials.Solid("ShopClothBlue", new Color(0.24f, 0.4f, 0.7f)),
            };

            var root = new GameObject(RootName);
            root.transform.SetParent(map, false);
            root.transform.position = MapBuilder.Ground(ArchiveCenter.x, ArchiveCenter.y);
            var component = root.AddComponent<CanvasRestocker>();

            Transform penShop = Shop(root.transform, "Tienda de plumas", PenShopLocal, ShopYaw);
            Transform canvasShop = Shop(root.transform, "Almacen de lienzos", CanvasShopLocal, -ShopYaw);
            Transform inkShop = Shop(root.transform, "Puesto de tintas", InkShopLocal, InnerShopYaw);
            Transform stampShop = Shop(root.transform, "Puesto de sellos", StampShopLocal, -InnerShopYaw);
            ClearVegetation(map, penShop.position, canvasShop.position, inkShop.position, stampShop.position);

            // Una plantilla desactivada por pincel del catalogo, clonada de la herramienta del campo con su modelo propio (sin Respawnable).
            var penTemplates = new PhysicalTool[BrushRules.Catalog.Length];
            var tipDepths = new float[penTemplates.Length];
            for (int i = 0; i < penTemplates.Length; i++)
            {
                var source = GameObject.Find(ToolModels.TierNames[Mathf.Min(i, ToolModels.TierNames.Length - 1)]);
                if (source == null || !source.TryGetComponent(out PhysicalTool _))
                {
                    Debug.LogWarning("[MapRestocker] falta la herramienta de plantilla " + ToolModels.TierNames[i] + ": no se arman los puestos.");
                    Object.DestroyImmediate(root);
                    return;
                }
                var clone = Object.Instantiate(source, root.transform);
                clone.name = ToolModels.TierNames[i] + "Template";
                foreach (var r in clone.GetComponentsInChildren<Respawnable>(true)) Object.DestroyImmediate(r);
                if (!clone.TryGetComponent(out BrushTier tier)) tier = clone.AddComponent<BrushTier>();
                tier.Configure(i);
                clone.transform.localPosition = Vector3.zero;
                clone.SetActive(false);
                penTemplates[i] = clone.GetComponent<PhysicalTool>();
                var tip = clone.transform.Find("RaycastTool");
                tipDepths[i] = tip != null ? Mathf.Max(0.05f, -tip.localPosition.y) : 0.1f;
            }

            // ---- Tienda de plumas: cinco lapiceros, cada uno con sus plumas reales (se agarran y se reponen) ----
            // Toldos: crema con un acento apagado por puesto, los mismos tonos que los estandartes de la rotonda (verde, terracota, azul, ocre).
            Frame(penShop, p, ShopCloth("ShopClothSage", new Color(0.46f, 0.58f, 0.43f)), p.cloudB, "PLUMAS Y PINCELES");
            TMP_Text brushReadout = Text(penShop, "BrushReadout", new Vector3(0f, 1.72f, 0.42f), "Agarrá la que quieras\nse repone sola", 0.065f, new Color(0.95f, 0.9f, 0.7f), null, new Vector2(1.5f, 0.45f));
            Color[] cupColors =
            {
                new Color(0.78f, 0.3f, 0.3f), new Color(0.3f, 0.5f, 0.78f), new Color(0.35f, 0.65f, 0.4f), new Color(0.85f, 0.65f, 0.25f), new Color(0.6f, 0.38f, 0.7f),
            };
            int[] cupCounts = { 5, 5, 5, 4, 3 };
            for (int i = 0; i < BrushRules.Catalog.Length; i++)
            {
                BrushSpec b = BrushRules.Catalog[i];
                float x = -1.0f + i * 0.5f;
                PenCup(penShop, i, penTemplates[i], tipDepths[i], cupCounts[Mathf.Min(i, cupCounts.Length - 1)], new Vector3(x, CounterTop, -0.2f), cupColors[i % cupColors.Length]);
                Plaque(penShop, p, $"Plaque{i}", new Vector3(x, 0.62f, -0.556f), $"{b.name}\n{Mathf.RoundToInt(b.widthMeters * 1000f)} mm", 0.44f);
            }
            Transform brushOut = Anchor(penShop, "BrushOutput", new Vector3(0f, CounterTop + 0.14f, -0.42f), Quaternion.identity);

            // ---- Almacen de lienzos ----
            const float canvasShopWidth = 6.5f, canvasCounterWidth = 3.9f;
            Frame(canvasShop, p, ShopCloth("ShopClothTerra", new Color(0.74f, 0.45f, 0.32f)), p.cloudB, "LIENZOS Y LIBRETAS", canvasShopWidth, canvasCounterWidth);
            Text(canvasShop, "Note", new Vector3(0f, 1.72f, 0.42f), "Agarrá el que quieras\nse repone solo", 0.065f, new Color(0.95f, 0.9f, 0.7f), null, new Vector2(1.5f, 0.45f));
            PaperRolls(canvasShop, p);
            Piles(canvasShop, component, p, canvasShopWidth, canvasCounterWidth);
            Transform canvasOut = Anchor(canvasShop, "CanvasOutput", new Vector3(-0.6f, 1.3f, -0.95f), Quaternion.Euler(0f, 180f, 0f));

            // ---- Puesto de tintas: frascos portatiles reales (E los guarda como cartucho de 5 usos) ----
            var wellSource = GameObject.Find("Inkwell");
            if (wellSource != null && wellSource.TryGetComponent(out MeshRenderer wellRenderer))
                InkStall(inkShop, p, root.transform, wellSource, wellRenderer.sharedMaterial);

            // ---- Puesto de sellos: las tres estampas vacias S, M y L ----
            StampStall(stampShop, p, root.transform);

            component.Configure(penTemplates, paper, p.leather, brushOut, canvasOut, null, brushReadout);
            EditorUtility.SetDirty(component);
            Undo.RegisterCreatedObjectUndo(root, RootName);
        }

        // ---------------------------------------------------------------- estructura comun del puesto

        private static Transform Shop(Transform root, string name, Vector2 local, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            Vector3 world = MapBuilder.Ground(ArchiveCenter.x + local.x, ArchiveCenter.y + local.y);
            // El frente del puesto es -Z local; con guinada 180 el frente mira a +Z mundo (hacia la torre, de donde llega el jugador).
            go.transform.SetPositionAndRotation(world, Quaternion.Euler(0f, 180f + yaw, 0f));
            return go.transform;
        }

        /// <summary>Tela de toldo con un color apagado (material guardado por nombre).</summary>
        private static Material ShopCloth(string name, Color color) => CanvasFrameMaterials.Solid(name, color);

        /// <summary>Mostrador, trastienda, postes, toldo rayado con flecos, farol y cartel colgante.</summary>
        private static void Frame(Transform shop, Palette p, Material clothA, Material clothB, string title, float width = 2.8f, float counterWidth = -1f)
        {
            if (counterWidth < 0f) counterWidth = width - 0.2f;
            Part(shop, PrimitiveType.Cube, "Counter", p.wood, new Vector3(0f, 0.45f, -0.3f), new Vector3(counterWidth, 0.9f, 0.5f), solid: true);
            Part(shop, PrimitiveType.Cube, "CounterTop", p.darkWood, new Vector3(0f, 0.915f, -0.3f), new Vector3(counterWidth + 0.12f, 0.03f, 0.62f), solid: true);
            int slats = Mathf.RoundToInt(counterWidth / 0.43f);
            float slatWidth = counterWidth / slats;
            for (int i = 0; i < slats; i++) // tablones verticales del frente
                Part(shop, PrimitiveType.Cube, "Slat" + i, i % 2 == 0 ? p.plank : p.wood, new Vector3(-counterWidth * 0.5f + slatWidth * (i + 0.5f), 0.45f, -0.555f), new Vector3(slatWidth - 0.02f, 0.84f, 0.015f));
            Part(shop, PrimitiveType.Cube, "BackWall", p.plank, new Vector3(0f, 1.2f, 0.5f), new Vector3(width, 2.4f, 0.08f), solid: true);
            Part(shop, PrimitiveType.Cube, "Batten0", p.darkWood, new Vector3(0f, 0.7f, 0.455f), new Vector3(width, 0.06f, 0.02f));
            Part(shop, PrimitiveType.Cube, "Batten1", p.darkWood, new Vector3(0f, 2.05f, 0.455f), new Vector3(width, 0.06f, 0.02f));
            Part(shop, PrimitiveType.Cube, "ReadoutBoard", p.board, new Vector3(0f, 1.72f, 0.44f), new Vector3(1.6f, 0.5f, 0.03f));
            Part(shop, PrimitiveType.Cube, "ReadoutFrame", p.darkWood, new Vector3(0f, 1.72f, 0.46f), new Vector3(1.7f, 0.6f, 0.02f));

            foreach (float x in new[] { -(width * 0.5f - 0.02f), width * 0.5f - 0.02f })
            {
                Part(shop, PrimitiveType.Cylinder, "PostFront", p.darkWood, new Vector3(x, 1.2f, -0.62f), new Vector3(0.1f, 1.2f, 0.1f), solid: true);
                Part(shop, PrimitiveType.Cylinder, "PostBack", p.darkWood, new Vector3(x, 1.35f, 0.5f), new Vector3(0.1f, 1.35f, 0.1f), solid: true);
            }

            // Si el mostrador no ocupa todo el frente (bahias laterales), postes extra que las separan.
            if (counterWidth < width - 0.5f)
                foreach (float x in new[] { -(counterWidth * 0.5f + 0.06f), counterWidth * 0.5f + 0.06f })
                    Part(shop, PrimitiveType.Cylinder, "PostBay", p.darkWood, new Vector3(x, 1.2f, -0.62f), new Vector3(0.1f, 1.2f, 0.1f), solid: true);

            // Toldo: placas rayadas inclinadas hacia el frente (el frente queda mas bajo) y flecos triangulares.
            var roof = new GameObject("Awning").transform;
            roof.SetParent(shop, false);
            roof.localPosition = new Vector3(0f, 2.56f, -0.06f);
            roof.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            int stripes = Mathf.Max(8, Mathf.RoundToInt((width + 0.3f) / 0.4f) / 2 * 2);
            float stripeWidth = (width + 0.3f) / stripes;
            for (int i = 0; i < stripes; i++)
                Part(roof, PrimitiveType.Cube, "Stripe" + i, i % 2 == 0 ? clothA : clothB, new Vector3(-(width + 0.3f) * 0.5f + stripeWidth * (i + 0.5f), 0f, 0f), new Vector3(stripeWidth, 0.04f, 1.32f));
            for (int i = 0; i < stripes; i++) // flecos: rombos colgando del borde delantero
                Part(shop, PrimitiveType.Cube, "Fringe" + i, i % 2 == 0 ? clothA : clothB, new Vector3(-(width + 0.3f) * 0.5f + stripeWidth * (i + 0.5f), 2.3f, -0.7f),
                    new Vector3(stripeWidth * 0.62f, stripeWidth * 0.62f, 0.02f), euler: new Vector3(0f, 0f, 45f));

            // Farol: cadena y esfera calida (sin luz real: cuesta en Quest).
            Part(shop, PrimitiveType.Cylinder, "LanternChain", p.metal, new Vector3(width * 0.5f - 0.4f, 2.34f, -0.55f), new Vector3(0.015f, 0.12f, 0.015f));
            Part(shop, PrimitiveType.Sphere, "Lantern", p.lantern, new Vector3(width * 0.5f - 0.4f, 2.12f, -0.55f), new Vector3(0.2f, 0.26f, 0.2f));
            Part(shop, PrimitiveType.Cube, "LanternCap", p.darkWood, new Vector3(width * 0.5f - 0.4f, 2.27f, -0.55f), new Vector3(0.13f, 0.03f, 0.13f));

            // Cartel colgante con el nombre.
            Part(shop, PrimitiveType.Cylinder, "SignChainL", p.metal, new Vector3(-0.62f, 2.12f, -0.8f), new Vector3(0.012f, 0.22f, 0.012f));
            Part(shop, PrimitiveType.Cylinder, "SignChainR", p.metal, new Vector3(0.62f, 2.12f, -0.8f), new Vector3(0.012f, 0.22f, 0.012f));
            Part(shop, PrimitiveType.Cube, "Sign", p.darkWood, new Vector3(0f, 1.94f, -0.8f), new Vector3(1.5f, 0.3f, 0.04f));
            Text(shop, "SignText", new Vector3(0f, 1.94f, -0.83f), title, 0.095f, p.brass.color, null, new Vector2(1.5f, 0.3f), sign: true);
        }

        // ---------------------------------------------------------------- tienda de plumas

        /// <summary>
        /// Un lapicero de ceramica con <paramref name="count"/> plumas REALES paradas (punta abajo, inclinadas hacia afuera): se agarran como cualquier pluma y el
        /// hueco repone otra (<see cref="DisplayStock"/>). Cuanto mas grueso el pincel, mas grande el lapicero.
        /// </summary>
        private static void PenCup(Transform shop, int index, PhysicalTool template, float tipDepth, int count, Vector3 baseLocal, Color cupColor)
        {
            var cup = new GameObject("Cup" + index);
            cup.transform.SetParent(shop, false);
            cup.transform.localPosition = baseLocal;
            Material cupMat = CanvasFrameMaterials.Solid("ShopCup" + index, cupColor);
            Material cream = CanvasFrameMaterials.Solid("ShopCream", new Color(0.95f, 0.9f, 0.76f));
            Material dark = CanvasFrameMaterials.Solid("ShopCupInside", new Color(0.12f, 0.08f, 0.07f));
            float rc = 0.09f + 0.018f * index;
            Part(cup.transform, PrimitiveType.Cylinder, "Body", cupMat, new Vector3(0f, 0.085f, 0f), new Vector3(rc * 2f, 0.085f, rc * 2f));
            Part(cup.transform, PrimitiveType.Cylinder, "Band", cream, new Vector3(0f, 0.065f, 0f), new Vector3(rc * 2f + 0.006f, 0.012f, rc * 2f + 0.006f));
            Part(cup.transform, PrimitiveType.Cylinder, "Rim", cream, new Vector3(0f, 0.172f, 0f), new Vector3(rc * 2f + 0.014f, 0.003f, rc * 2f + 0.014f));
            Part(cup.transform, PrimitiveType.Cylinder, "Mouth", dark, new Vector3(0f, 0.1745f, 0f), new Vector3(rc * 2f - 0.02f, 0.002f, rc * 2f - 0.02f));

            const float tilt = 7f;
            float ring = rc * 0.55f;
            var slots = new Transform[count];
            for (int k = 0; k < count; k++)
            {
                float a = (k / (float)count + index * 0.07f) * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                slots[k] = Anchor(cup.transform, "Slot" + k, dir * ring + new Vector3(0f, 0.04f + tipDepth * Mathf.Cos(tilt * Mathf.Deg2Rad), 0f),
                    Quaternion.AngleAxis(tilt, Vector3.Cross(Vector3.up, dir)));
            }
            var stock = cup.AddComponent<DisplayStock>();
            stock.Configure(template.gameObject, slots);
            EditorUtility.SetDirty(stock);
        }

        // ---------------------------------------------------------------- puesto de tintas

        /// <summary>Tres bandejas de frascos (normal, poderosa, resistente): cada frasco es un tintero portatil real; con E se guarda como cartucho y se repone.</summary>
        private static void InkStall(Transform shop, Palette p, Transform templateParent, GameObject wellSource, Material body)
        {
            Frame(shop, p, ShopCloth("ShopClothNavy", new Color(0.28f, 0.38f, 0.55f)), p.cloudB, "TINTAS");
            Text(shop, "Note", new Vector3(0f, 1.72f, 0.42f), "Agarrá un frasco\nse guarda como cartucho", 0.065f, new Color(0.95f, 0.9f, 0.7f), null, new Vector2(1.5f, 0.45f));
            // Estante del fondo con frascos grandes de adorno.
            Part(shop, PrimitiveType.Cube, "JarShelf", p.darkWood, new Vector3(0f, 1.3f, 0.38f), new Vector3(2.3f, 0.04f, 0.2f));
            Part(shop, PrimitiveType.Cube, "JarBracketL", p.darkWood, new Vector3(-1.05f, 1.22f, 0.4f), new Vector3(0.04f, 0.14f, 0.16f));
            Part(shop, PrimitiveType.Cube, "JarBracketR", p.darkWood, new Vector3(1.05f, 1.22f, 0.4f), new Vector3(0.04f, 0.14f, 0.16f));
            for (int i = 0; i < 9; i++)
            {
                Color c = InkKindInfo.Color((InkKind)(i % 3));
                float x = -1.0f + i * 0.25f;
                Part(shop, PrimitiveType.Cylinder, "Jar" + i, CanvasFrameMaterials.Solid("ShopJarInk" + (i % 3), c), new Vector3(x, 1.38f, 0.38f), new Vector3(0.1f, 0.06f, 0.1f));
                Part(shop, PrimitiveType.Cylinder, "JarNeck" + i, p.cream, new Vector3(x, 1.45f, 0.38f), new Vector3(0.04f, 0.02f, 0.04f));
            }

            string[] labels = { "Tinta\nla de siempre", "Poderosa\n×2,5 de potencia", "Resistente\nsirve bajo el agua" };
            for (int kind = 0; kind < 3; kind++)
            {
                var template = Object.Instantiate(wellSource, templateParent);
                template.name = "Ink" + (InkKind)kind + "Template";
                foreach (var r in template.GetComponentsInChildren<Respawnable>(true)) Object.DestroyImmediate(r);
                ToolModels.MakeInkwell(template, body, (InkKind)kind, true);
                template.transform.localPosition = Vector3.zero;
                template.SetActive(false);

                float x = -0.85f + kind * 0.85f;
                Transform[] slots = Tray(shop, p, "InkTray" + kind, x, 0.62f, 3, 0.19f);
                var stockOwner = new GameObject("InkStock" + kind);
                stockOwner.transform.SetParent(shop, false);
                var stock = stockOwner.AddComponent<DisplayStock>();
                stock.Configure(template, slots);
                EditorUtility.SetDirty(stock);
                Plaque(shop, p, "InkPlaque" + kind, new Vector3(x, 0.62f, -0.556f), labels[kind], 0.7f);
            }
        }

        // ---------------------------------------------------------------- puesto de sellos

        /// <summary>Las tres estampas vacias del campo (S, M, L) en bandejas: 3 chicas, 2 medianas y 1 grande; se cargan con un glifo desde el grimorio.</summary>
        private static void StampStall(Transform shop, Palette p, Transform templateParent)
        {
            Frame(shop, p, ShopCloth("ShopClothOchre", new Color(0.8f, 0.62f, 0.3f)), p.cloudB, "SELLOS");
            Text(shop, "Note", new Vector3(0f, 1.72f, 0.42f), "Se cargan con un glifo\ndesde el grimorio (T)", 0.065f, new Color(0.95f, 0.9f, 0.7f), null, new Vector2(1.5f, 0.45f));
            (string id, string label, float x, int count, float spacing, float width)[] rows =
            {
                ("Stamp_S_0", "Sello S\n8 cm", -0.92f, 3, 0.14f, 0.52f),
                ("Stamp_M_1", "Sello M\n20 cm", -0.12f, 2, 0.3f, 0.76f),
                ("Stamp_L_2", "Sello L\n34 cm", 0.78f, 1, 0.3f, 0.5f),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                var source = GameObject.Find(rows[i].id);
                if (source == null) { Debug.LogWarning("[MapRestocker] falta la estampa " + rows[i].id + ": el puesto de sellos queda incompleto."); continue; }
                var template = Object.Instantiate(source, templateParent);
                template.name = "Stamp" + i + "Template";
                foreach (var r in template.GetComponentsInChildren<Respawnable>(true)) Object.DestroyImmediate(r);
                template.transform.localPosition = Vector3.zero;
                template.SetActive(false);

                Transform[] slots = Tray(shop, p, "StampTray" + i, rows[i].x, rows[i].width, rows[i].count, rows[i].spacing);
                var stockOwner = new GameObject("StampStock" + i);
                stockOwner.transform.SetParent(shop, false);
                var stock = stockOwner.AddComponent<DisplayStock>();
                stock.Configure(template, slots);
                EditorUtility.SetDirty(stock);
                Plaque(shop, p, "StampPlaque" + i, new Vector3(rows[i].x, 0.62f, -0.556f), rows[i].label, 0.5f);
            }
        }

        /// <summary>Bandeja baja de madera sobre el mostrador con <paramref name="count"/> huecos en fila; devuelve los huecos.</summary>
        private static Transform[] Tray(Transform shop, Palette p, string name, float x, float width, int count, float spacing)
        {
            const float height = 0.05f;
            Part(shop, PrimitiveType.Cube, name, p.darkWood, new Vector3(x, CounterTop + height * 0.5f, -0.3f), new Vector3(width, height, 0.36f));
            Part(shop, PrimitiveType.Cube, name + "_Inlay", p.cream, new Vector3(x, CounterTop + height + 0.001f, -0.3f), new Vector3(width - 0.06f, 0.002f, 0.3f));
            var slots = new Transform[count];
            for (int k = 0; k < count; k++)
                slots[k] = Anchor(shop, name + "_Slot" + k, new Vector3(x + (k - (count - 1) * 0.5f) * spacing, CounterTop + height + 0.002f, -0.3f), Quaternion.identity);
            return slots;
        }

        // ---------------------------------------------------------------- almacen de lienzos

        private static void PaperRolls(Transform shop, Palette p)
        {
            // Estante a la izquierda con rollos de papel (se ve el circulo de la punta) y cintas de colores.
            Part(shop, PrimitiveType.Cube, "RollShelf", p.darkWood, new Vector3(-0.85f, 1.3f, 0.34f), new Vector3(1.1f, 0.04f, 0.3f));
            Color[] ribbons = { new Color(0.78f, 0.3f, 0.3f), new Color(0.3f, 0.5f, 0.78f), new Color(0.85f, 0.65f, 0.25f), new Color(0.35f, 0.65f, 0.4f) };
            for (int i = 0; i < 4; i++)
            {
                float x = -1.2f + i * 0.23f;
                Part(shop, PrimitiveType.Cylinder, "Roll" + i, p.cream, new Vector3(x, 1.43f, 0.34f), new Vector3(0.19f, 0.15f, 0.19f), euler: new Vector3(90f, 0f, 0f));
                Part(shop, PrimitiveType.Cylinder, "RollBand" + i, CanvasFrameMaterials.Solid("ShopRibbon" + i, ribbons[i]), new Vector3(x, 1.43f, 0.34f), new Vector3(0.2f, 0.025f, 0.2f), euler: new Vector3(90f, 0f, 0f));
            }
            // Cajon inferior con rollos asomando.
            Part(shop, PrimitiveType.Cube, "RollCrate", p.wood, new Vector3(-0.95f, 1.0f, 0.3f), new Vector3(0.9f, 0.14f, 0.3f));
            for (int i = 0; i < 3; i++)
                Part(shop, PrimitiveType.Cylinder, "CrateRoll" + i, p.cream, new Vector3(-1.2f + i * 0.25f, 1.1f, 0.3f), new Vector3(0.16f, 0.13f, 0.16f), euler: new Vector3(90f, 0f, 0f));
        }

        /// <summary>Las nueve pilas: tres libretas (8 hojas) y cuatro hojas sueltas sobre el mostrador, y las dos hojas L en peanas del piso, en las bahias laterales bajo el toldo.</summary>
        private static void Piles(Transform shop, CanvasRestocker owner, Palette p, float width, float counterWidth)
        {
            CanvasSpec[] counter =
            {
                CanvasSpec.Notebook(CanvasSize.S), CanvasSpec.Notebook(CanvasSize.M), CanvasSpec.Notebook(CanvasSize.L),
                CanvasSpec.Sheet(CanvasSize.S, CanvasShape.Square), CanvasSpec.Sheet(CanvasSize.S, CanvasShape.Rect),
                CanvasSpec.Sheet(CanvasSize.M, CanvasShape.Square), CanvasSpec.Sheet(CanvasSize.M, CanvasShape.Rect),
            };
            const float gap = 0.13f;
            float total = -gap;
            foreach (CanvasSpec s in counter) total += CanvasRules.Dimensions(s).x + gap;
            float x = -total * 0.5f;
            for (int i = 0; i < counter.Length; i++)
            {
                float w = CanvasRules.Dimensions(counter[i]).x;
                Pile(shop, owner, p, "Pile" + i, counter[i], new Vector3(x + w * 0.5f, CounterTop, -0.3f), 0.1f, true);
                x += w + gap;
            }
            float sideX = counterWidth * 0.5f + (width - counterWidth) * 0.25f; // centro de cada bahia, bajo el mismo toldo
            Pile(shop, owner, p, "PileLSquare", CanvasSpec.Sheet(CanvasSize.L, CanvasShape.Square), new Vector3(-sideX, 0f, -0.1f), 0.12f, false);
            Pile(shop, owner, p, "PileLRect", CanvasSpec.Sheet(CanvasSize.L, CanvasShape.Rect), new Vector3(sideX, 0f, -0.1f), 0.12f, false);
        }

        /// <summary>Una peana con pila de papel (o de libretas) y el lienzo real de exhibicion flotando encima; rotulo al frente.</summary>
        private static void Pile(Transform shop, CanvasRestocker owner, Palette p, string name, CanvasSpec spec, Vector3 baseLocal, float footHeight, bool onCounter)
        {
            Vector2 d = CanvasRules.Dimensions(spec);
            var pile = new GameObject(name);
            pile.transform.SetParent(shop, false);
            pile.transform.localPosition = baseLocal;
            bool notebook = spec.kind == CanvasKind.Notebook;
            Material stackMat = notebook ? p.leather : p.cream;
            Part(pile.transform, PrimitiveType.Cube, "Foot", p.darkWood, new Vector3(0f, footHeight * 0.5f, 0f), new Vector3(d.x + 0.06f, footHeight, 0.3f));
            float stackHeight = notebook ? 0.09f : 0.06f;
            if (spec.shape == CanvasShape.Round)
                Part(pile.transform, PrimitiveType.Cylinder, "Stack", stackMat, new Vector3(0f, footHeight + stackHeight * 0.5f, 0f), new Vector3(d.x, stackHeight * 0.5f, d.x));
            else
                Part(pile.transform, PrimitiveType.Cube, "Stack", stackMat, new Vector3(0f, footHeight + stackHeight * 0.5f, 0f), new Vector3(d.x, stackHeight, 0.24f));
            if (!notebook) Part(pile.transform, PrimitiveType.Cube, "StackEdge", p.blue, new Vector3(0f, footHeight + stackHeight * 0.5f, -0.121f), new Vector3(d.x, stackHeight * 0.3f, 0.004f));

            const float hover = 0.14f;
            float centerY = footHeight + stackHeight + hover + d.y * 0.5f;
            Transform anchor = Anchor(pile.transform, "DisplayAnchor", new Vector3(0f, centerY, 0f), Quaternion.identity);
            string label = (notebook ? "Libreta " : "Hoja ") + spec.size + (spec.shape == CanvasShape.Rect && !notebook ? " rect." : "") + $"\n{Mathf.RoundToInt(d.x * 100f)}×{Mathf.RoundToInt(d.y * 100f)} cm";
            if (onCounter)
                Plaque(shop, p, name + "_Plaque", new Vector3(baseLocal.x, 0.62f, -0.556f), label, Mathf.Max(0.42f, d.x + 0.06f));
            else
                Plaque(pile.transform, p, name + "_Plaque", new Vector3(0f, 0.2f, -0.24f), label, 0.6f);
            var comp = pile.AddComponent<CanvasPile>();
            comp.Configure(owner, anchor, spec);
            EditorUtility.SetDirty(comp);
        }

        private static void Plaque(Transform shop, Palette p, string name, Vector3 local, string text, float width)
        {
            bool twoLines = text.Contains("\n");
            float h = twoLines ? 0.2f : 0.14f;
            Part(shop, PrimitiveType.Cube, name, p.darkWood, local, new Vector3(width, h, 0.012f));
            Text(shop, name + "_Text", local + new Vector3(0f, 0f, -0.012f), text, twoLines ? 0.065f : 0.1f, p.cream.color, null, new Vector2(width, h));
        }

        private static GameObject Part(Transform parent, PrimitiveType type, string name, Material material, Vector3 local, Vector3 size, Vector3? euler = null, bool solid = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!solid) { var c = go.GetComponent<Collider>(); if (c != null) Object.DestroyImmediate(c); }
            return go;
        }

        private static Transform Anchor(Transform parent, string name, Vector3 local, Quaternion rotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = rotation;
            return go.transform;
        }

        private static TMP_Text Text(Transform parent, string name, Vector3 local, string text, float fontSize, Color color, Quaternion? rotation, Vector2 size, bool sign = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            var label = go.AddComponent<TextMeshPro>();
            if (sign && SignFont != null) label.font = SignFont; // titulos de cartel en Lora; el resto usa la fuente por defecto (Nunito)
            label.rectTransform.sizeDelta = size;
            label.enableAutoSizing = false;
            label.fontSize = fontSize * 10f;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.text = text;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return label;
        }

        /// <summary>Saca arboles y peñascos que caigan dentro de los puestos (radio 2,6 m alrededor de cada uno).</summary>
        private static void ClearVegetation(Transform map, params Vector3[] shops)
        {
            foreach (string groupName in new[] { "Trees", "Rocks" })
            {
                Transform group = map.Find(groupName);
                if (group == null) continue;
                for (int i = group.childCount - 1; i >= 0; i--)
                {
                    Vector3 pos = group.GetChild(i).position;
                    foreach (Vector3 at in shops)
                    {
                        if (new Vector2(pos.x - at.x, pos.z - at.z).magnitude >= 2.6f) continue;
                        Object.DestroyImmediate(group.GetChild(i).gameObject);
                        break;
                    }
                }
            }
        }
    }
}
