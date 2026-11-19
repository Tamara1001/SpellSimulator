using TMPro;
using UnityEditor;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Core;
using SpellSimulator.World;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>
    /// Dos puestos al aire libre en la entrada del Archivo (Docs/Archivo/ARCHIVO_PLAN.md): la «Tienda de plumas» (cinco tarros con un pincel
    /// cada uno: apuntar y clic saca ese pincel) y el «Almacen de lienzos» (rollos, pilas de lienzos y un tablero de pedido inclinado con el
    /// selector tipo / tamaño / forma / hojas y el boton SACAR). Los dos comparten un solo <see cref="CanvasRestocker"/> (en la raiz
    /// «Restocker»), asi que la logica y las pruebas no cambian. Armado con primitivas; el jugador se para del lado -Z de cada puesto
    /// (marco local: frente -Z, fondo +Z) y los puestos se giran para mirar hacia la torre.
    /// </summary>
    public static class MapRestocker
    {
        private const string RootName = "Restocker";
        private static readonly Quaternion Lying = Quaternion.Euler(90f, 0f, 0f);

        /// <summary>Centro del Archivo en el mundo (XZ). Ver ValleyShape (Archivo r 9) y Docs/MAPA_FLUJO_PLAN.md §18.</summary>
        public static readonly Vector2 ArchiveCenter = new Vector2(0f, -46f);
        /// <summary>Posicion de cada puesto respecto del centro del Archivo (x, z mundo): al lado de la torre (+Z), a la entrada.</summary>
        private static readonly Vector2 PenShopLocal = new Vector2(-7.5f, 5.5f);
        private static readonly Vector2 CanvasShopLocal = new Vector2(7.5f, 5.5f);
        /// <summary>Guinada extra (grados) sobre el giro base de 180: con 0 el frente del puesto mira a +Z mundo (hacia la torre, de donde llega el jugador).</summary>
        private const float ShopYaw = 25f;

        private const float CounterTop = 0.93f;

        private static TMP_FontAsset SignFont => SpellSimulator.EditorTools.FontBuilder.LoadSignFont();

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
                wood = CanvasFrameMaterials.Solid("ShopWood", new Color(0.56f, 0.38f, 0.22f)),
                darkWood = CanvasFrameMaterials.Solid("ShopDarkWood", new Color(0.36f, 0.23f, 0.14f)),
                plank = CanvasFrameMaterials.Solid("ShopPlank", new Color(0.68f, 0.5f, 0.3f)),
                metal = CanvasFrameMaterials.Solid("ShopMetal", new Color(0.62f, 0.62f, 0.66f)),
                brass = CanvasFrameMaterials.Solid("ShopBrass", new Color(0.86f, 0.66f, 0.25f)),
                cream = CanvasFrameMaterials.Solid("ShopCream", new Color(0.95f, 0.9f, 0.76f)),
                board = CanvasFrameMaterials.Solid("ShopBoard", new Color(0.16f, 0.26f, 0.22f)),
                cloudA = CanvasFrameMaterials.Solid("ShopClothRed", new Color(0.72f, 0.24f, 0.28f)),
                cloudB = CanvasFrameMaterials.Solid("ShopClothCream", new Color(0.93f, 0.86f, 0.7f)),
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
            ClearVegetation(map, penShop.position, canvasShop.position);

            // Plantilla de pluma: clon desactivado, sin Respawnable (cada pincel sacado es nuevo).
            var penClone = Object.Instantiate(pen.gameObject, root.transform);
            penClone.name = "PenTemplate";
            foreach (var r in penClone.GetComponentsInChildren<Respawnable>(true)) Object.DestroyImmediate(r);
            penClone.transform.localPosition = Vector3.zero;
            penClone.SetActive(false);

            // ---- Tienda de plumas ----
            Frame(penShop, p, p.cloudA, p.cloudB, "PLUMAS Y PINCELES");
            TMP_Text brushReadout = Text(penShop, "BrushReadout", new Vector3(0f, 1.72f, 0.42f), "Tocá un tarro para sacar un pincel", 0.065f, new Color(0.95f, 0.9f, 0.7f), null, new Vector2(1.5f, 0.45f));
            ShelfWithInk(penShop, p);
            Color[] cupColors =
            {
                new Color(0.78f, 0.3f, 0.3f), new Color(0.3f, 0.5f, 0.78f), new Color(0.35f, 0.65f, 0.4f), new Color(0.85f, 0.65f, 0.25f), new Color(0.6f, 0.38f, 0.7f),
            };
            for (int i = 0; i < BrushRules.Catalog.Length; i++)
            {
                BrushSpec b = BrushRules.Catalog[i];
                float t = BrushRules.Catalog.Length > 1 ? i / (float)(BrushRules.Catalog.Length - 1) : 0f;
                float x = -0.96f + i * 0.48f;
                PenJar(penShop, component, p, i, b, new Vector3(x, CounterTop, -0.2f), t, cupColors[i % cupColors.Length]);
                Plaque(penShop, p, $"Plaque{i}", new Vector3(x, 0.62f, -0.556f), $"{Mathf.RoundToInt(b.widthMeters * 1000f)} mm", 0.28f);
            }
            Transform brushOut = Anchor(penShop, "BrushOutput", new Vector3(0f, CounterTop + 0.14f, -0.42f), Quaternion.identity);

            // ---- Almacen de lienzos ----
            const float canvasShopWidth = 3.9f;
            Frame(canvasShop, p, p.blue, p.cloudB, "LIENZOS Y LIBRETAS", canvasShopWidth);
            Text(canvasShop, "Note", new Vector3(0f, 1.72f, 0.42f), "Agarrá el que quieras\nse repone solo", 0.065f, new Color(0.95f, 0.9f, 0.7f), null, new Vector2(1.5f, 0.45f));
            PaperRolls(canvasShop, p);
            Piles(canvasShop, component, p, canvasShopWidth);
            Transform canvasOut = Anchor(canvasShop, "CanvasOutput", new Vector3(-0.6f, 1.3f, -0.95f), Quaternion.Euler(0f, 180f, 0f));

            component.Configure(penClone.GetComponent<PhysicalTool>(), paper, p.leather, brushOut, canvasOut, null, brushReadout);
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

        /// <summary>Mostrador, trastienda, postes, toldo rayado con flecos, farol y cartel colgante.</summary>
        private static void Frame(Transform shop, Palette p, Material clothA, Material clothB, string title, float width = 2.8f)
        {
            Part(shop, PrimitiveType.Cube, "Counter", p.wood, new Vector3(0f, 0.45f, -0.3f), new Vector3(width - 0.2f, 0.9f, 0.5f), solid: true);
            Part(shop, PrimitiveType.Cube, "CounterTop", p.darkWood, new Vector3(0f, 0.915f, -0.3f), new Vector3(width - 0.08f, 0.03f, 0.62f), solid: true);
            int slats = Mathf.RoundToInt((width - 0.2f) / 0.43f);
            float slatWidth = (width - 0.2f) / slats;
            for (int i = 0; i < slats; i++) // tablones verticales del frente
                Part(shop, PrimitiveType.Cube, "Slat" + i, i % 2 == 0 ? p.plank : p.wood, new Vector3(-(width - 0.2f) * 0.5f + slatWidth * (i + 0.5f), 0.45f, -0.555f), new Vector3(slatWidth - 0.02f, 0.84f, 0.015f));
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

            // Toldo: placas rayadas inclinadas hacia el frente (el frente queda mas bajo) y flecos triangulares.
            var roof = new GameObject("Awning").transform;
            roof.SetParent(shop, false);
            roof.localPosition = new Vector3(0f, 2.56f, -0.06f);
            roof.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            const int stripes = 8;
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

        /// <summary>Un tarro de ceramica con un pincel: el tarro entero es el boton (apuntar y clic saca ese pincel).</summary>
        private static void PenJar(Transform shop, CanvasRestocker owner, Palette p, int index, BrushSpec spec, Vector3 baseLocal, float t, Color cupColor)
        {
            var jar = new GameObject("Jar" + index);
            jar.transform.SetParent(shop, false);
            jar.transform.localPosition = baseLocal;
            Material cup = CanvasFrameMaterials.Solid("ShopCup" + index, cupColor);
            Material shaft = CanvasFrameMaterials.Solid("ShopShaft" + index, Color.Lerp(new Color(0.85f, 0.6f, 0.3f), new Color(0.35f, 0.45f, 0.7f), t));
            Material bristle = CanvasFrameMaterials.Solid("ShopBristle" + index, Color.Lerp(new Color(0.12f, 0.1f, 0.12f), new Color(0.82f, 0.74f, 0.55f), t));

            Part(jar.transform, PrimitiveType.Cylinder, "Cup", cup, new Vector3(0f, 0.07f, 0f), new Vector3(0.2f, 0.07f, 0.2f));
            Part(jar.transform, PrimitiveType.Cylinder, "CupRim", p.cream, new Vector3(0f, 0.14f, 0f), new Vector3(0.215f, 0.008f, 0.215f));
            float d = Mathf.Lerp(0.014f, 0.042f, t);
            Part(jar.transform, PrimitiveType.Cylinder, "Shaft", shaft, new Vector3(0f, 0.3f, 0f), new Vector3(d, 0.17f, d));
            Part(jar.transform, PrimitiveType.Cylinder, "Ferrule", p.metal, new Vector3(0f, 0.475f, 0f), new Vector3(d * 1.25f, 0.035f, d * 1.25f));
            switch (spec.tip)
            {
                case BrushTip.Flat:
                    Part(jar.transform, PrimitiveType.Cube, "Tip", bristle, new Vector3(0f, 0.53f, 0f), new Vector3(d * 1.7f, 0.07f, d * 0.45f));
                    break;
                case BrushTip.Fine:
                    Part(jar.transform, PrimitiveType.Sphere, "Tip", bristle, new Vector3(0f, 0.525f, 0f), new Vector3(d * 0.9f, 0.07f, d * 0.9f));
                    break;
                default:
                    Part(jar.transform, PrimitiveType.Sphere, "Tip", bristle, new Vector3(0f, 0.53f, 0f), new Vector3(d * 1.2f, 0.09f, d * 1.2f));
                    break;
            }

            var box = jar.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.3f, 0f);
            box.size = new Vector3(0.3f, 0.6f, 0.3f);
            var button = jar.AddComponent<RestockerButton>();
            button.Configure(owner, RestockerAction.Brush, index, "Sacar " + spec.name);
            EditorUtility.SetDirty(button);
        }

        private static void ShelfWithInk(Transform shop, Palette p)
        {
            Part(shop, PrimitiveType.Cube, "InkShelf", p.darkWood, new Vector3(0f, 1.3f, 0.38f), new Vector3(2.3f, 0.04f, 0.2f));
            Part(shop, PrimitiveType.Cube, "InkBracketL", p.darkWood, new Vector3(-1.05f, 1.22f, 0.4f), new Vector3(0.04f, 0.14f, 0.16f));
            Part(shop, PrimitiveType.Cube, "InkBracketR", p.darkWood, new Vector3(1.05f, 1.22f, 0.4f), new Vector3(0.04f, 0.14f, 0.16f));
            Color[] inks = { new Color(0.2f, 0.2f, 0.45f), new Color(0.6f, 0.15f, 0.2f), new Color(0.15f, 0.45f, 0.3f), new Color(0.15f, 0.12f, 0.12f) };
            for (int i = 0; i < inks.Length; i++)
            {
                float x = -0.95f + i * 0.28f;
                Part(shop, PrimitiveType.Cylinder, "Ink" + i, CanvasFrameMaterials.Solid("ShopInk" + i, inks[i]), new Vector3(x, 1.38f, 0.38f), new Vector3(0.09f, 0.06f, 0.09f));
                Part(shop, PrimitiveType.Cylinder, "InkNeck" + i, p.cream, new Vector3(x, 1.45f, 0.38f), new Vector3(0.035f, 0.02f, 0.035f));
            }
            // Pinceles de adorno colgando de clavos, a la derecha del estante.
            for (int i = 0; i < 3; i++)
            {
                float x = 0.35f + i * 0.28f;
                Part(shop, PrimitiveType.Cylinder, "HangBrush" + i, p.wood, new Vector3(x, 1.42f, 0.42f), new Vector3(0.025f, 0.12f, 0.025f), euler: new Vector3(0f, 0f, 8f));
                Part(shop, PrimitiveType.Sphere, "HangTip" + i, p.darkWood, new Vector3(x - 0.035f, 1.29f, 0.42f), new Vector3(0.04f, 0.07f, 0.04f));
            }
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

        /// <summary>Las nueve pilas: tres libretas (8 hojas) y cuatro hojas sueltas sobre el mostrador, y las dos hojas L en peanas del piso a los lados.</summary>
        private static void Piles(Transform shop, CanvasRestocker owner, Palette p, float width)
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
            float sideX = width * 0.5f + 0.9f;
            Pile(shop, owner, p, "PileLSquare", CanvasSpec.Sheet(CanvasSize.L, CanvasShape.Square), new Vector3(-sideX, 0f, -0.2f), 0.12f, false);
            Pile(shop, owner, p, "PileLRect", CanvasSpec.Sheet(CanvasSize.L, CanvasShape.Rect), new Vector3(sideX, 0f, -0.2f), 0.12f, false);
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
            if (sign && SignFont != null) label.font = SignFont; // titulos de cartel en Caveat Brush; el resto usa la fuente por defecto (Nunito)
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
        private static void ClearVegetation(Transform map, Vector3 a, Vector3 b)
        {
            foreach (string groupName in new[] { "Trees", "Rocks" })
            {
                Transform group = map.Find(groupName);
                if (group == null) continue;
                for (int i = group.childCount - 1; i >= 0; i--)
                {
                    Vector3 pos = group.GetChild(i).position;
                    if (new Vector2(pos.x - a.x, pos.z - a.z).magnitude < 2.6f || new Vector2(pos.x - b.x, pos.z - b.z).magnitude < 2.6f)
                        Object.DestroyImmediate(group.GetChild(i).gameObject);
                }
            }
        }
    }
}
