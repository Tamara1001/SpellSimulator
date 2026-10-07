using UnityEditor;
using UnityEngine;
using SpellSimulator.World;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>
    /// Modelos de las herramientas y el tintero (lowpoly, en metros). Reemplazan los cilindros escalados: pluma de
    /// plumin con un cristal que muestra la tinta, pincel con virola y cerdas, goma con envoltorio, trapo doblado y
    /// tintero de vidrio con corcho. Eje Y a lo largo de la herramienta: la punta de la pluma y el pincel mira a -Y;
    /// la de la goma y el trapo, a +Y (igual que antes, para no tocar el agarre).
    /// </summary>
    public static class ToolModels
    {
        private static readonly Color Gold = new Color(0.9f, 0.72f, 0.3f);
        private static readonly Color Silver = new Color(0.75f, 0.77f, 0.82f);

        public static void Apply(Material body)
        {
            foreach (var tool in Object.FindObjectsByType<PhysicalTool>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string n = tool.name;
                if (n.StartsWith("Pen")) Pen(tool.gameObject, body);
                else if (n.StartsWith("Brush")) Brush(tool.gameObject, body);
                else if (n.StartsWith("Eraser")) Eraser(tool.gameObject, body);
                else if (n.StartsWith("Rag")) Rag(tool.gameObject, body);
            }
            foreach (var well in Object.FindObjectsByType<Inkwell>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Inkwell(well.gameObject, body);
        }

        // ---------------------------------------------------------------- piezas

        private static void Pen(GameObject go, Material body)
        {
            var b = new LowPolyMesh();
            var wood = new Color(0.27f, 0.13f, 0.32f);
            b.Frustum(Matrix4x4.identity, 0.0012f, 0.0055f, -0.1f, -0.078f, 6, Silver, false);              // plumin
            b.Frustum(Matrix4x4.identity, 0.0068f, 0.0068f, -0.078f, -0.056f, 8, Gold, false);               // virola
            b.Frustum(Matrix4x4.identity, 0.0065f, 0.0088f, -0.056f, 0.088f, 8, wood, false);                // cuerpo
            foreach (float y in new[] { -0.056f, 0.01f, 0.07f })
                b.Frustum(Matrix4x4.identity, 0.0092f, 0.0092f, y, y + 0.004f, 8, Gold, false);              // anillos
            b.Frustum(Matrix4x4.identity, 0.0088f, 0.0035f, 0.088f, 0.102f, 8, Gold, true);                  // remate
            Setup(go, body, b, "ToolPen", 0.011f, 0.205f, 0.001f, new Vector3(0f, -0.1f, 0f));

            // Cristal de tinta (se tiñe segun lo que queda).
            var window = new LowPolyMesh();
            window.Frustum(Matrix4x4.identity, 0.0098f, 0.0098f, -0.03f, 0.02f, 8, Color.white, false);
            AddIndicator(go, window, "ToolPenInk");
        }

        private static void Brush(GameObject go, Material body)
        {
            var b = new LowPolyMesh();
            var wood = new Color(0.62f, 0.4f, 0.22f);
            b.Frustum(Matrix4x4.identity, 0.0004f, 0.0078f, -0.12f, -0.065f, 8, new Color(0.22f, 0.09f, 0.08f), false); // cerdas
            b.Frustum(Matrix4x4.identity, 0.0078f, 0.0072f, -0.065f, -0.03f, 8, Silver, false);                          // virola
            b.Frustum(Matrix4x4.identity, 0.0066f, 0.0055f, -0.03f, 0.11f, 8, wood, false);                              // mango
            b.Frustum(Matrix4x4.identity, 0.0058f, 0.0035f, 0.11f, 0.122f, 8, Gold, true);
            Setup(go, body, b, "ToolBrush", 0.011f, 0.245f, 0.001f, new Vector3(0f, -0.12f, 0f));

            var window = new LowPolyMesh();
            window.Frustum(Matrix4x4.identity, 0.0078f, 0.0078f, 0.035f, 0.075f, 8, Color.white, false);
            AddIndicator(go, window, "ToolBrushInk");
        }

        private static void Eraser(GameObject go, Material body)
        {
            var b = new LowPolyMesh();
            var pink = new Color(0.93f, 0.62f, 0.66f);
            b.Frustum(Matrix4x4.identity, 0.02f, 0.02f, -0.04f, 0.03f, 8, pink, false, true);
            b.Frustum(Matrix4x4.identity, 0.0212f, 0.0212f, -0.04f, 0.0f, 8, new Color(0.9f, 0.88f, 0.8f), false); // envoltorio
            b.Frustum(Matrix4x4.identity, 0.0214f, 0.0214f, -0.025f, -0.017f, 8, new Color(0.3f, 0.4f, 0.65f), false);
            b.Frustum(Matrix4x4.identity, 0.02f, 0.015f, 0.03f, 0.042f, 8, pink, true);
            Setup(go, body, b, "ToolEraser", 0.022f, 0.085f, 0.001f, new Vector3(0f, 0.044f, 0f));
        }

        private static void Rag(GameObject go, Material body)
        {
            var b = new LowPolyMesh();
            var cloth = new Color(0.92f, 0.87f, 0.74f);
            b.Box(Matrix4x4.TRS(new Vector3(0f, 0.01f, 0f), Quaternion.identity, Vector3.one), new Vector3(0.11f, 0.02f, 0.09f), cloth);
            b.Box(Matrix4x4.TRS(new Vector3(0f, 0.026f, 0f), Quaternion.Euler(0f, 22f, 0f), Vector3.one), new Vector3(0.1f, 0.02f, 0.08f), Color.Lerp(cloth, Color.white, 0.2f));
            b.Box(Matrix4x4.TRS(new Vector3(0f, 0.04f, 0f), Quaternion.Euler(0f, -14f, 0f), Vector3.one), new Vector3(0.08f, 0.018f, 0.07f), cloth);
            b.Box(Matrix4x4.TRS(new Vector3(0f, 0.0105f, 0.046f), Quaternion.identity, Vector3.one), new Vector3(0.11f, 0.012f, 0.004f), new Color(0.3f, 0.45f, 0.7f));
            Setup(go, body, b, "ToolRag", 0.055f, 0.06f, 0.025f, new Vector3(0f, 0.05f, 0f));
        }

        private static void Inkwell(GameObject go, Material body)
        {
            var b = new LowPolyMesh();
            b.Frustum(Matrix4x4.identity, 0.045f, 0.045f, 0f, 0.05f, 8, new Color(0.08f, 0.1f, 0.24f), false, true);         // vidrio con tinta
            b.Frustum(Matrix4x4.identity, 0.0462f, 0.0462f, 0.012f, 0.034f, 8, new Color(0.9f, 0.85f, 0.7f), false);          // etiqueta
            b.Frustum(Matrix4x4.identity, 0.045f, 0.02f, 0.05f, 0.066f, 8, new Color(0.14f, 0.16f, 0.32f), false);            // hombro
            b.Frustum(Matrix4x4.identity, 0.02f, 0.02f, 0.066f, 0.078f, 8, new Color(0.14f, 0.16f, 0.32f), false);            // cuello
            b.Frustum(Matrix4x4.identity, 0.019f, 0.017f, 0.078f, 0.1f, 8, new Color(0.6f, 0.45f, 0.3f), true);               // corcho
            var rb = go.GetComponent<Rigidbody>();
            SetMesh(go, body, b, "Inkwell");
            var capsule = go.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.direction = 1;
                capsule.radius = 0.045f;
                capsule.height = 0.1f;
                capsule.center = new Vector3(0f, 0.05f, 0f);
            }
            if (rb != null) rb.mass = 0.4f;
        }

        // ---------------------------------------------------------------- utilidades

        private static void SetMesh(GameObject go, Material body, LowPolyMesh model, string name)
        {
            go.transform.localScale = Vector3.one;
            go.GetComponent<MeshFilter>().sharedMesh = MapBuilder.SaveMesh(model.Build(name));
            go.GetComponent<MeshRenderer>().sharedMaterial = body;
        }

        private static void Setup(GameObject go, Material body, LowPolyMesh model, string name, float radius, float height, float centerY, Vector3 tip)
        {
            SetMesh(go, body, model, name);
            var capsule = go.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.direction = 1;
                capsule.radius = radius;
                capsule.height = height;
                capsule.center = new Vector3(0f, centerY, 0f);
            }
            var rayOrigin = go.transform.Find("RaycastTool");
            if (rayOrigin != null) rayOrigin.localPosition = tip;
        }

        private static void AddIndicator(GameObject go, LowPolyMesh model, string name)
        {
            var old = go.transform.Find("InkIndicator");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var child = new GameObject("InkIndicator");
            child.transform.SetParent(go.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = MapBuilder.SaveMesh(model.Build(name));
            // Material claro tenible por MaterialPropertyBlock (el del lapiz original).
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/InkIndicator.mat") ?? CreateIndicatorMaterial();
            var tool = go.GetComponent<PhysicalTool>();
            var so = new SerializedObject(tool);
            so.FindProperty("inkIndicator").objectReferenceValue = renderer;
            so.ApplyModifiedProperties();
        }

        private static Material CreateIndicatorMaterial()
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "InkIndicator" };
            m.SetColor("_BaseColor", new Color(0.12f, 0.08f, 0.06f));
            m.SetFloat("_Smoothness", 0.7f);
            AssetDatabase.CreateAsset(m, "Assets/_Project/Materials/InkIndicator.mat");
            return m;
        }
    }
}
