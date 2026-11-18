using TMPro;
using UnityEditor;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Core;
using SpellSimulator.World;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>
    /// Mueble «restocker» (Bloque C de Docs/LIENZOS_PLAN.md): un archivero con la consola arriba. Izquierda, cinco pinceles del catalogo
    /// (<see cref="BrushRules"/>); derecha, el selector de lienzo / libreta (tipo, tamaño, forma, hojas) y «Sacar». Armado con primitivas
    /// y colliders; se busca un sitio libre de la biblioteca de la torre (el Archivo de la pradera todavia no esta construido: cuando
    /// exista, basta con mover este objeto). El jugador se para del lado -Z mirando a +Z.
    /// </summary>
    public static class MapRestocker
    {
        private const string RootName = "Restocker";
        private static readonly Quaternion Lying = Quaternion.Euler(90f, 0f, 0f);

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
                Debug.LogWarning("[MapRestocker] faltan PaperCanvas o una pluma de plantilla: no se arma el restocker.");
                return;
            }

            Vector3 spot = FindSpot(towerOrigin, out Quaternion rotation);
            var root = new GameObject(RootName);
            root.transform.SetParent(map, false);
            root.transform.SetPositionAndRotation(spot, rotation);

            Material wood = CanvasFrameMaterials.Solid("RestockerWood", new Color(0.42f, 0.28f, 0.18f));
            Material dark = CanvasFrameMaterials.Solid("RestockerDark", new Color(0.3f, 0.19f, 0.12f));
            Material metal = CanvasFrameMaterials.Solid("RestockerMetal", new Color(0.62f, 0.62f, 0.66f));
            Material brushMat = CanvasFrameMaterials.Solid("RestockerBrush", new Color(0.2f, 0.15f, 0.3f));
            Material selectMat = CanvasFrameMaterials.Solid("RestockerSelect", new Color(0.25f, 0.55f, 0.65f));
            Material giveMat = CanvasFrameMaterials.Solid("RestockerGive", new Color(0.3f, 0.65f, 0.35f));
            Material leather = CanvasFrameMaterials.Solid("CanvasCoverLeather", new Color(0.36f, 0.2f, 0.26f));

            // Cuerpo: archivero bajo, losa de la consola y respaldo alto con repisas (los colliders de los cubos quedan: es solido).
            Cube(root.transform, "Body", wood, new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.9f, 0.6f));
            Cube(root.transform, "Top", dark, new Vector3(0f, 0.915f, 0f), new Vector3(1.66f, 0.03f, 0.66f));
            Cube(root.transform, "Hutch", wood, new Vector3(0f, 1.5f, 0.25f), new Vector3(1.6f, 1.1f, 0.1f));
            Cube(root.transform, "ShelfCap", dark, new Vector3(0f, 2.07f, 0.22f), new Vector3(1.66f, 0.04f, 0.16f));
            for (int i = 0; i < 3; i++)
                Cube(root.transform, "Drawer" + i, dark, new Vector3(-0.52f + i * 0.52f, 0.45f, -0.305f), new Vector3(0.46f, 0.7f, 0.02f));
            for (int i = 0; i < 3; i++)
                Cube(root.transform, "Handle" + i, metal, new Vector3(-0.52f + i * 0.52f, 0.65f, -0.325f), new Vector3(0.12f, 0.025f, 0.025f));

            var component = root.AddComponent<CanvasRestocker>();

            // Plantilla de pluma: clon desactivado, sin Respawnable (cada pincel sacado es nuevo).
            var penClone = Object.Instantiate(pen.gameObject, root.transform);
            penClone.name = "PenTemplate";
            foreach (var r in penClone.GetComponentsInChildren<Respawnable>(true)) Object.DestroyImmediate(r);
            penClone.transform.localPosition = Vector3.zero;
            penClone.SetActive(false);

            Transform brushOut = Anchor(root.transform, "BrushOutput", new Vector3(-0.45f, 1.02f, -0.12f), Quaternion.identity);
            Transform canvasOut = Anchor(root.transform, "CanvasOutput", new Vector3(0.45f, 1.25f, -0.85f), Quaternion.Euler(0f, 180f, 0f));

            // Pantallas en el respaldo (el texto mira a -Z, hacia quien se para frente al mueble).
            Text(root.transform, "BrushTitle", new Vector3(-0.4f, 1.95f, 0.195f), "PINCELES", 0.12f, Color.white);
            Text(root.transform, "CanvasTitle", new Vector3(0.4f, 1.95f, 0.195f), "LIENZOS Y LIBRETAS", 0.12f, Color.white);
            TMP_Text brushReadout = Text(root.transform, "BrushReadout", new Vector3(-0.4f, 1.55f, 0.195f), "Elegí un pincel", 0.1f, new Color(0.95f, 0.85f, 0.6f));
            TMP_Text canvasReadout = Text(root.transform, "CanvasReadout", new Vector3(0.4f, 1.55f, 0.195f), "", 0.1f, new Color(0.95f, 0.85f, 0.6f));

            // Botones de pinceles (izquierda): el rotulo es el grosor en mm.
            for (int i = 0; i < BrushRules.Catalog.Length; i++)
            {
                BrushSpec b = BrushRules.Catalog[i];
                Button(root.transform, component, $"Brush{i}", brushMat, new Vector3(-0.70f + i * 0.14f, 0.945f, -0.05f), 0.12f, $"{Mathf.RoundToInt(b.widthMeters * 1000f)}",
                    RestockerAction.Brush, i, "Sacar " + b.name);
            }
            Text(root.transform, "BrushHint", new Vector3(-0.42f, 0.935f, -0.2f), "mm de trazo", 0.07f, Color.white, Lying);

            // Selector de lienzo (derecha).
            string[] labels = { "Tipo", "Tamaño", "Forma", "Hojas −", "Hojas +" };
            RestockerAction[] actions = { RestockerAction.Kind, RestockerAction.Size, RestockerAction.Shape, RestockerAction.PagesLess, RestockerAction.PagesMore };
            string[] prompts = { "Cambiar: lienzo / libreta", "Cambiar el tamaño (S, M, L)", "Cambiar la forma (cuadrado / rectangular)", "Una hoja menos", "Una hoja más" };
            for (int i = 0; i < labels.Length; i++)
                Button(root.transform, component, "Select" + i, selectMat, new Vector3(0.02f + i * 0.17f, 0.945f, 0.1f), 0.155f, labels[i], actions[i], 0, prompts[i], 0.04f);
            Button(root.transform, component, "Dispense", giveMat, new Vector3(0.43f, 0.945f, -0.12f), 0.3f, "SACAR", RestockerAction.DispenseCanvas, 0, "Sacar el lienzo o la libreta elegidos");

            component.Configure(penClone.GetComponent<PhysicalTool>(), paper, CanvasFrameMaterials.Gold, CanvasFrameMaterials.Blue, leather, brushOut, canvasOut, canvasReadout, brushReadout);
            EditorUtility.SetDirty(component);
            Undo.RegisterCreatedObjectUndo(root, RootName);
        }

        /// <summary>Primer sitio libre de la biblioteca (a la derecha de la mesa de trabajo, mirando al centro de la torre) donde entra el mueble.</summary>
        private static Vector3 FindSpot(Vector3 towerOrigin, out Quaternion rotation)
        {
            Vector3 anchor = towerOrigin + TowerProps.LibraryBench;
            Vector3[] candidates =
            {
                new Vector3(2.2f, 0f, 0f), new Vector3(2.2f, 0f, 1.2f), new Vector3(2.4f, 0f, -1.2f), new Vector3(-2.4f, 0f, 0f),
                new Vector3(0f, 0f, 1.8f), new Vector3(3.0f, 0f, 0.6f), new Vector3(-3.0f, 0f, 1.0f), new Vector3(0f, 0f, -2.2f),
            };
            Physics.SyncTransforms();
            Vector3 best = anchor + candidates[0];
            foreach (Vector3 c in candidates)
            {
                Vector3 p = anchor + c;
                Vector3 flat = new Vector3(p.x - towerOrigin.x, 0f, p.z - towerOrigin.z);
                if (flat.magnitude > TowerBuilder.InnerRadius - 1.4f || flat.sqrMagnitude < 0.01f) continue;
                Quaternion r = Quaternion.LookRotation(flat.normalized); // mira hacia afuera: el jugador queda del lado del centro
                if (Physics.CheckBox(p + Vector3.up * 1.2f, new Vector3(0.95f, 1.1f, 0.6f), r, ~0, QueryTriggerInteraction.Ignore)) continue;
                best = p;
                rotation = r;
                return best;
            }
            Vector3 f2 = new Vector3(best.x - towerOrigin.x, 0f, best.z - towerOrigin.z);
            rotation = Quaternion.LookRotation(f2.sqrMagnitude > 0.01f ? f2.normalized : Vector3.forward);
            Debug.LogWarning("[MapRestocker] ningun sitio libre cerca de la mesa de trabajo: se usa el primero (revisar a mano).");
            return best;
        }

        private static GameObject Cube(Transform parent, string name, Material material, Vector3 local, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
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

        private static TMP_Text Text(Transform parent, string name, Vector3 local, string text, float fontSize, Color color, Quaternion? rotation = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            var label = go.AddComponent<TextMeshPro>();
            label.rectTransform.sizeDelta = new Vector2(0.7f, 0.25f);
            label.enableAutoSizing = false;
            label.fontSize = fontSize * 10f;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.text = text;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return label;
        }

        private static void Button(Transform parent, CanvasRestocker owner, string name, Material material, Vector3 local, float width, string label,
            RestockerAction action, int argument, string prompt, float labelSize = 0.07f)
        {
            var go = Cube(parent, name, material, local, new Vector3(width, 0.03f, 0.12f));
            var box = go.GetComponent<BoxCollider>();
            box.size = new Vector3(1f, 2.2f, 1f);
            var button = go.AddComponent<RestockerButton>();
            button.Configure(owner, action, argument, prompt);
            EditorUtility.SetDirty(button);
            Text(parent, name + "_Label", local + new Vector3(0f, 0.02f, 0f), label, labelSize, Color.white, Lying);
        }
    }
}
