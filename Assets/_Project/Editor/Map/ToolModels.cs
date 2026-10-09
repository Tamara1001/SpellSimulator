using UnityEditor;
using UnityEngine;
using SpellSimulator.Core;
using SpellSimulator.World;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>
    /// Modelos de las herramientas y el tintero (lowpoly, en metros). Reemplazan los cilindros escalados: pluma de
    /// plumin con un cristal que muestra la tinta, pincel con virola y cerdas, goma con envoltorio, trapo doblado y
    /// tinteros (FIJO de piedra / PORTATIL de vidrio con corcho, ver MakeInkwell). Eje Y a lo largo de la herramienta: la punta de la pluma y el pincel mira a -Y;
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
            // El tintero plantilla del campo (del que se clonan los de la torre) es FIJO; el resto conserva su configuracion.
            var template = GameObject.Find("Inkwell");
            if (template != null && template.GetComponent<World.Inkwell>() != null) MakeInkwell(template, body, InkKind.Normal, false);
            foreach (var well in Object.FindObjectsByType<World.Inkwell>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (well.gameObject != template) Inkwell(well.gameObject, body, well.Kind, well.Portable);
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

        // ---------------------------------------------------------------- tinteros

        private static readonly Color StoneLight = new Color(0.68f, 0.64f, 0.57f);
        private static readonly Color StoneDark = new Color(0.48f, 0.45f, 0.41f);
        private static readonly Color Brass = new Color(0.78f, 0.6f, 0.28f);
        private static readonly Color Glaze = new Color(0.16f, 0.13f, 0.2f);
        private static readonly Color GlassTint = new Color(0.74f, 0.84f, 0.88f);
        private static readonly Color CorkColor = new Color(0.66f, 0.49f, 0.31f);
        private static readonly Color Leather = new Color(0.45f, 0.27f, 0.15f);
        private static readonly Color LeatherDark = new Color(0.32f, 0.18f, 0.1f);
        private static readonly Color Quill = new Color(0.95f, 0.93f, 0.86f);

        private const string GlowMaterialPath = "Assets/_Project/Materials/InkwellGlow.mat";

        /// <summary>
        /// Configura un tintero (clase y familia, via SerializedObject porque los campos son privados) y le arma el modelo:
        /// FIJO = mueble pesado de piedra, kinematico; PORTATIL = frasco chico con corcho y etiqueta de cuero, con fisica.
        /// </summary>
        public static void MakeInkwell(GameObject go, Material body, InkKind kind, bool portable)
        {
            var well = go.GetComponent<World.Inkwell>();
            if (well != null)
            {
                var so = new SerializedObject(well);
                so.FindProperty("kind").enumValueIndex = (int)kind;
                so.FindProperty("portable").boolValue = portable;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            Inkwell(go, body, kind, portable);
        }

        private static void Inkwell(GameObject go, Material body, InkKind kind, bool portable)
        {
            var old = go.transform.Find("InkwellGlow");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            if (portable) PortableInkwell(go, body, kind);
            else FixedInkwell(go, body, kind);
        }

        /// <summary>
        /// Tintero FIJO: pedestal octogonal de piedra (dos escalones con fleje y remaches de laton), un tintero grande de
        /// ceramica esmaltada con boca de laton y la tinta a ras, y una pluma de ganso clavada. ~20 cm de ancho: se lee como mueble.
        /// </summary>
        private static void FixedInkwell(GameObject go, Material body, InkKind kind)
        {
            var b = new LowPolyMesh();
            Matrix4x4 id = Matrix4x4.identity;
            Color ink = InkKindInfo.Color(kind);
            b.Frustum(id, 0.1f, 0.096f, 0f, 0.026f, 8, StoneDark, true, true);                // zocalo
            b.Frustum(id, 0.0975f, 0.0975f, 0.016f, 0.021f, 8, Brass, false);                 // fleje
            b.Frustum(id, 0.084f, 0.078f, 0.026f, 0.042f, 8, StoneLight, true);              // escalon
            b.Frustum(id, 0.056f, 0.066f, 0.042f, 0.074f, 8, Glaze, false);                  // panza del tintero
            b.Frustum(id, 0.066f, 0.05f, 0.074f, 0.09f, 8, Glaze, false);                    // hombro
            b.Frustum(id, 0.054f, 0.054f, 0.09f, 0.098f, 8, Brass, true);                    // boca de laton
            b.Frustum(id, 0.043f, 0.043f, 0.098f, 0.1f, 8, ink, true);                       // tinta a ras
            // Cuatro remaches de laton en el zocalo.
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 90f + 22.5f) * Mathf.Deg2Rad;
                var at = new Vector3(Mathf.Cos(a) * 0.099f, 0.008f, Mathf.Sin(a) * 0.099f);
                b.Box(Matrix4x4.TRS(at, Quaternion.Euler(0f, -i * 90f - 22.5f, 0f), Vector3.one), new Vector3(0.006f, 0.008f, 0.012f), Brass);
            }
            // Pluma de ganso clavada en la tinta, inclinada hacia afuera.
            Matrix4x4 quill = Matrix4x4.TRS(new Vector3(0.012f, 0.094f, 0.004f), Quaternion.Euler(8f, 0f, -18f), Vector3.one);
            b.Frustum(quill, 0.0028f, 0.0018f, 0f, 0.17f, 5, Quill, true);
            b.Box(quill * Matrix4x4.TRS(new Vector3(0.008f, 0.12f, 0f), Quaternion.Euler(0f, 0f, -4f), Vector3.one), new Vector3(0.016f, 0.1f, 0.0025f), Quill);
            b.Box(quill * Matrix4x4.TRS(new Vector3(-0.005f, 0.13f, 0f), Quaternion.Euler(0f, 0f, 3f), Vector3.one), new Vector3(0.01f, 0.08f, 0.0022f), Color.Lerp(Quill, StoneLight, 0.35f));

            var rb = go.GetComponent<Rigidbody>();
            SetMesh(go, body, b, "InkwellFixed" + kind);
            var capsule = go.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.direction = 1;
                capsule.radius = 0.1f;
                capsule.height = 0.2f;
                capsule.center = new Vector3(0f, 0.1f, 0f);
            }
            if (rb != null)
            {
                rb.mass = 12f;
                rb.isKinematic = true;   // mobiliario: no se empuja con fisica
            }
        }

        /// <summary>
        /// Tintero PORTATIL: frasco chico redondeado (la mitad de abajo muestra la tinta de su clase), corcho y una etiqueta
        /// de cuero colgada del cuello. Se distinguen sin color: Poderosa = anillos emisivos + banda dorada + estrella en la
        /// etiqueta; Resistente = ondas grabadas en el vidrio y una gota sobre el corcho; Normal = liso.
        /// </summary>
        private static void PortableInkwell(GameObject go, Material body, InkKind kind)
        {
            var b = new LowPolyMesh();
            Matrix4x4 id = Matrix4x4.identity;
            Color ink = InkKindInfo.Color(kind);
            Color glass = Color.Lerp(GlassTint, ink, 0.18f);
            b.Frustum(id, 0.026f, 0.034f, 0f, 0.008f, 10, ink, false, true);                 // base redondeada
            b.Frustum(id, 0.034f, 0.036f, 0.008f, 0.04f, 10, ink, false);                     // tinta
            b.Frustum(id, 0.036f, 0.031f, 0.04f, 0.054f, 10, glass, false);                   // vidrio vacio
            b.Frustum(id, 0.031f, 0.015f, 0.054f, 0.064f, 10, glass, false);                  // hombro
            b.Frustum(id, 0.013f, 0.013f, 0.064f, 0.075f, 8, glass, false);                   // cuello
            b.Frustum(id, 0.0165f, 0.0165f, 0.073f, 0.078f, 8, glass, true);                  // labio
            b.Frustum(id, 0.0145f, 0.0128f, 0.078f, 0.097f, 8, CorkColor, true);              // corcho
            b.Frustum(id, 0.0142f, 0.0142f, 0.066f, 0.069f, 8, LeatherDark, false);           // cordon
            // Etiqueta de cuero colgando del cordon (al costado +X).
            Matrix4x4 tag = Matrix4x4.TRS(new Vector3(0.039f, 0.046f, 0f), Quaternion.Euler(0f, 0f, 6f), Vector3.one);
            b.Box(Matrix4x4.TRS(new Vector3(0.026f, 0.063f, 0f), Quaternion.Euler(0f, 0f, -52f), Vector3.one), new Vector3(0.0025f, 0.02f, 0.0025f), LeatherDark);
            b.Box(tag, new Vector3(0.004f, 0.026f, 0.02f), Leather);

            switch (kind)
            {
                case InkKind.Powerful:
                    b.Frustum(id, 0.0168f, 0.0168f, 0.0705f, 0.0735f, 8, Gold, false);        // banda dorada en el cuello
                    // Estrella de cuatro puntas en la etiqueta.
                    b.Box(tag * Matrix4x4.TRS(new Vector3(0.0025f, 0f, 0f), Quaternion.Euler(45f, 0f, 0f), Vector3.one), new Vector3(0.002f, 0.012f, 0.004f), Gold);
                    b.Box(tag * Matrix4x4.TRS(new Vector3(0.0025f, 0f, 0f), Quaternion.Euler(-45f, 0f, 0f), Vector3.one), new Vector3(0.002f, 0.012f, 0.004f), Gold);
                    break;
                case InkKind.Resistant:
                    // Ondas grabadas: dos aros finos claros sobre la tinta.
                    Color wave = Color.Lerp(ink, Color.white, 0.6f);
                    b.Frustum(id, 0.0368f, 0.0368f, 0.017f, 0.0195f, 10, wave, false);
                    b.Frustum(id, 0.0368f, 0.0368f, 0.027f, 0.0295f, 10, wave, false);
                    // Gota sobre el corcho (bulbo + punta) y otra gotita en la etiqueta.
                    b.Frustum(id, 0.006f, 0.0075f, 0.097f, 0.101f, 6, ink, false);
                    b.Frustum(id, 0.0075f, 0f, 0.101f, 0.115f, 6, ink, false);
                    b.Frustum(tag * Matrix4x4.Translate(new Vector3(0.0028f, -0.004f, 0f)), 0.0045f, 0f, 0f, 0.01f, 5, wave, false, true);
                    break;
            }

            var rb = go.GetComponent<Rigidbody>();
            SetMesh(go, body, b, "InkwellPortable" + kind);
            if (kind == InkKind.Powerful) AddGlowRing(go, ink);
            var capsule = go.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.direction = 1;
                capsule.radius = 0.037f;
                capsule.height = 0.1f;
                capsule.center = new Vector3(0f, 0.05f, 0f);
            }
            if (rb != null)
            {
                rb.mass = 0.3f;
                rb.isKinematic = false;
            }
        }

        /// <summary>Destello de la tinta poderosa: dos anillos con material emisivo alrededor de la panza (hijo sin collider).</summary>
        private static void AddGlowRing(GameObject go, Color ink)
        {
            var ring = new LowPolyMesh();
            Color c = Color.Lerp(ink, Color.white, 0.25f);
            ring.Frustum(Matrix4x4.identity, 0.0372f, 0.0372f, 0.03f, 0.036f, 10, c, false);
            ring.Frustum(Matrix4x4.identity, 0.0372f, 0.0372f, 0.012f, 0.0155f, 10, c, false);
            var child = new GameObject("InkwellGlow");
            child.transform.SetParent(go.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = MapBuilder.SaveMesh(ring.Build("InkwellGlowRing"));
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath) ?? CreateGlowMaterial(ink);
        }

        private static Material CreateGlowMaterial(Color ink)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "InkwellGlow" };
            Color bright = Color.Lerp(ink, Color.white, 0.3f);
            m.SetColor("_BaseColor", bright);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", bright * 2.2f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            AssetDatabase.CreateAsset(m, GlowMaterialPath);
            return m;
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
