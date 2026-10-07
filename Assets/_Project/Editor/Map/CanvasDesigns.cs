using UnityEditor;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.World;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>
    /// Variedad de lienzos (copias del papel del laboratorio con otra forma y otro vestido):
    /// cuadernos y lienzos grandes que se agarran y flotan al soltarlos, un pergamino que flota fijo en el aire,
    /// y soportes fijos: pizarra de pared, caballete y mesa de dibujo inclinada.
    /// Todos emiten por la cara con tinta (InkSide): dar vuelta la hoja cambia hacia donde apunta el hechizo.
    /// </summary>
    public static class CanvasDesigns
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

            // Todos los lienzos de papel emiten por la cara con tinta.
            foreach (var c in Object.FindObjectsByType<DrawingCanvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                SetInkSide(c.gameObject);

            // Biblioteca: cuadernos y un lienzo grande sobre la mesa de herramientas; pizarra en la pared libre.
            Vector3 bench = towerOrigin + new Vector3(0.4f, 0.82f, -2.35f);
            Grabbable(template, material, "Notebook_A", bench + new Vector3(-0.7f, 0.012f, 0f), Quaternion.Euler(90f, 12f, 0f), 0.24f, 0.32f, Dressing.Notebook);
            Grabbable(template, material, "Notebook_B", bench + new Vector3(-0.35f, 0.012f, 0.1f), Quaternion.Euler(90f, -20f, 0f), 0.2f, 0.28f, Dressing.Notebook);
            Grabbable(template, material, "BigCanvas_Library", towerOrigin + new Vector3(1.8f, 1.2f, 1.0f), Quaternion.Euler(0f, 250f, 0f), 0.9f, 1.2f, Dressing.Framed, startFloating: true);

            float wallAngle = 283f * Mathf.Deg2Rad;
            Vector3 outward = new Vector3(Mathf.Cos(wallAngle), 0f, Mathf.Sin(wallAngle));
            Vector3 boardAt = towerOrigin + outward * (TowerBuilder.InnerRadius - 0.1f) + Vector3.up * 1.7f;
            Fixed(template, material, "WallBoard_Library", boardAt, Quaternion.LookRotation(outward), 1.5f, 1.0f, Dressing.Framed);

            // Estudio (primer piso): mesa de dibujo inclinada, caballete y un pergamino flotante.
            DraftingTable(template, material, "DraftingTable", towerOrigin + new Vector3(-1.6f, floor, -1.2f), 20f);
            Easel(template, material, "Easel_Study", towerOrigin + new Vector3(1.5f, floor, -1.0f), 250f);
            Grabbable(template, material, "FloatingScroll_Study", towerOrigin + new Vector3(-0.2f, floor + 1.5f, -1.8f), Quaternion.Euler(0f, 180f, 0f), 0.8f, 1.0f, Dressing.Scroll, startFloating: true);

            // Campo de practica: caballete con un lienzo grande y un pergamino flotante.
            Easel(template, material, "Easel_Field", fieldOrigin + new Vector3(-9f, 0f, -2f), 200f);
            Grabbable(template, material, "FloatingScroll_Field", fieldOrigin + new Vector3(-9f, 1.3f, -5f), Quaternion.Euler(0f, 0f, 0f), 0.8f, 1.0f, Dressing.Scroll, startFloating: true);
        }

        private enum Dressing { None, Notebook, Framed, Scroll }

        // ---------------------------------------------------------------- piezas

        private static GameObject Spawn(GameObject template, string name, Vector3 position, Quaternion rotation, float width, float height)
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

        /// <summary>Lienzo que se agarra (y flota o se apoya al soltarlo).</summary>
        private static GameObject Grabbable(GameObject template, Material material, string name, Vector3 position, Quaternion rotation,
            float width, float height, Dressing dressing, bool startFloating = false)
        {
            var go = Spawn(template, name, position, rotation, width, height);
            var rb = go.GetComponent<Rigidbody>();
            rb.isKinematic = startFloating;
            rb.useGravity = !startFloating;
            var grab = go.GetComponent<PhysicalCanvas>();
            if (grab != null)
            {
                var so = new SerializedObject(grab);
                so.FindProperty("objectName").stringValue = name.StartsWith("Notebook") ? "cuaderno" : name.StartsWith("Floating") ? "pergamino" : "lienzo grande";
                so.FindProperty("startFloating").boolValue = startFloating;
                so.ApplyModifiedProperties();
            }
            Dress(go, material, dressing, width, height);
            if (go.GetComponent<Respawnable>() == null) go.AddComponent<Respawnable>();
            return go;
        }

        /// <summary>Lienzo fijo (no se agarra): pizarra, caballete, mesa.</summary>
        private static GameObject Fixed(GameObject template, Material material, string name, Vector3 position, Quaternion rotation,
            float width, float height, Dressing dressing)
        {
            var go = Spawn(template, name, position, rotation, width, height);
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
                case Dressing.Scroll:
                    // Pergamino: varillas arriba y abajo con remates dorados.
                    var rod = new Color(0.5f, 0.32f, 0.18f);
                    var gold = new Color(0.9f, 0.72f, 0.3f);
                    b.Box(Matrix4x4.TRS(new Vector3(0f, h / 2f + 0.02f, 0.004f), Quaternion.identity, Vector3.one), new Vector3(w + 0.1f, 0.035f, 0.035f), rod);
                    b.Box(Matrix4x4.TRS(new Vector3(0f, -h / 2f - 0.02f, 0.004f), Quaternion.identity, Vector3.one), new Vector3(w + 0.1f, 0.035f, 0.035f), rod);
                    foreach (float sx in new[] { -1f, 1f })
                        foreach (float sy in new[] { -1f, 1f })
                            b.Box(Matrix4x4.TRS(new Vector3(sx * (w / 2f + 0.06f), sy * (h / 2f + 0.02f), 0.004f), Quaternion.identity, Vector3.one), new Vector3(0.03f, 0.05f, 0.05f), gold);
                    break;
            }
            Finish(b, wrapper, material, $"Dressing_{dressing}_{canvas.name}");
        }

        // ---------------------------------------------------------------- soportes

        private static void Easel(GameObject template, Material material, string name, Vector3 ground, float yaw)
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
            Leg(new Vector3(-0.5f, 0f, 0.25f), new Vector3(-0.38f, 1.9f, 0f), Wood);
            Leg(new Vector3(0.5f, 0f, 0.25f), new Vector3(0.38f, 1.9f, 0f), Wood);
            Leg(new Vector3(0f, 0f, -0.9f), new Vector3(0f, 1.9f, -0.1f), DarkWood);
            b.Box(Matrix4x4.TRS(new Vector3(0f, 0.55f, 0f), Quaternion.identity, Vector3.one), new Vector3(1.0f, 0.06f, 0.07f), DarkWood); // reposa-lienzo
            b.Box(Matrix4x4.TRS(new Vector3(0f, 1.95f, 0.02f), Quaternion.identity, Vector3.one), new Vector3(0.9f, 0.05f, 0.06f), DarkWood);
            var mesh = MapBuilder.SaveMesh(b.Build($"Easel_{name}"));
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = material;
            // El lienzo se apoya en el reposa-lienzo, de cara a quien mira el caballete (-Z local del caballete).
            var canvas = Fixed(template, material, name + "_Canvas", root.transform.TransformPoint(new Vector3(0f, 1.3f, -0.05f)),
                root.transform.rotation * Quaternion.Euler(-6f, 180f, 0f), 0.9f, 1.2f, Dressing.None);
            canvas.transform.SetParent(root.transform, true);
            Undo.RegisterCreatedObjectUndo(root, name);
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
                root.transform.rotation * Quaternion.Euler(-20f, 0f, 0f) * Quaternion.Euler(-90f, 0f, 0f) * Quaternion.Euler(180f, 0f, 0f), 1.1f, 0.7f, Dressing.None);
            canvas.transform.SetParent(root.transform, true);
            Undo.RegisterCreatedObjectUndo(root, name);
        }
    }
}
