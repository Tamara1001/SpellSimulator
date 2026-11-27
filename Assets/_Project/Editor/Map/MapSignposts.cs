using UnityEditor;
using UnityEngine;
using SpellSimulator.World.Map;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>
    /// Postes con flechas de una palabra (paso 9, D31): datos en <see cref="Signposts"/>. Cada poste es una sola malla estática
    /// (poste + flechas con punta, colores por vértice) con un collider en el poste; cada flecha lleva su palabra en las dos
    /// caras. Se arma después de MapStructures para apoyarse sobre el piso real (el del Archivo es más alto que el terreno).
    /// </summary>
    public static class MapSignposts
    {
        private const float PostHeight = 2.5f, PostWidth = 0.13f;
        private const float TopArrowY = 2.2f, ArrowStep = 0.27f;
        private const float ArrowLength = 1.1f, ArrowHeight = 0.21f, ArrowThickness = 0.045f, TipLength = 0.2f;
        /// <summary>Cuánto entra la flecha detrás del poste (las tablas salen del poste, no flotan al lado).</summary>
        private const float ArrowBack = 0.08f;
        private static readonly Color PostWood = new Color(0.33f, 0.21f, 0.13f);
        private static readonly Color CapWood = new Color(0.26f, 0.16f, 0.1f);

        public static void Build(Transform map, Material material)
        {
            Physics.SyncTransforms(); // el piso del Archivo se acaba de crear: que los rayos lo vean
            var root = new GameObject("Signposts").transform;
            root.SetParent(map, false);
            foreach (Signposts.Post post in Signposts.All) BuildPost(root, material, post);
        }

        private static void BuildPost(Transform root, Material material, Signposts.Post post)
        {
            Vector3 ground = GroundAt(post.Position);
            var b = new LowPolyMesh();
            b.Box(Matrix4x4.Translate(new Vector3(0f, PostHeight * 0.5f, 0f)), new Vector3(PostWidth, PostHeight, PostWidth), PostWood);
            b.Frustum(Matrix4x4.Translate(new Vector3(0f, PostHeight, 0f)), PostWidth * 0.75f, 0.02f, 0f, 0.16f, 4, CapWood);

            var go = MapBuilder.Place("Signpost_" + post.Id, root, null, material, ground, Quaternion.identity);
            for (int i = 0; i < post.Arrows.Length; i++)
            {
                Signposts.Arrow arrow = post.Arrows[i];
                float yaw = Signposts.Yaw(post.Position, arrow.Target);
                float y = TopArrowY - i * ArrowStep;
                var frame = Matrix4x4.TRS(new Vector3(0f, y, 0f), Quaternion.Euler(0f, yaw, 0f), Vector3.one);
                Arrow(b, frame, arrow.Color);
                Labels(go.transform, arrow, yaw, y);
            }
            go.GetComponent<MeshFilter>().sharedMesh = MapBuilder.SaveMesh(b.Build("Signpost_" + post.Id));
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, PostHeight * 0.5f, 0f);
            col.size = new Vector3(PostWidth + 0.05f, PostHeight, PostWidth + 0.05f);
            Undo.RegisterCreatedObjectUndo(go, go.name);
        }

        /// <summary>Tabla con punta, a lo largo de +Z del marco (sale del poste hacia el destino).</summary>
        private static void Arrow(LowPolyMesh b, Matrix4x4 frame, Color color)
        {
            Color edge = color * 0.8f;
            edge.a = 1f;
            float z0 = -ArrowBack, z1 = ArrowLength - TipLength;
            b.Box(frame * Matrix4x4.Translate(new Vector3(0f, 0f, (z0 + z1) * 0.5f)), new Vector3(ArrowThickness, ArrowHeight, z1 - z0), color, edge);

            // Punta: prisma triangular (dos caras, tres bordes), con las dos vueltas para no depender del sentido de las caras.
            float hx = ArrowThickness * 0.5f, hy = ArrowHeight * 0.5f + 0.04f, tip = ArrowLength;
            Vector3 P(float x, float yy, float z) => frame.MultiplyPoint3x4(new Vector3(x, yy, z));
            foreach (float sx in new[] { -hx, hx })
            {
                Vector3 a = P(sx, hy, z1), c = P(sx, -hy, z1), t = P(sx, 0f, tip);
                b.Tri(a, c, t, color);
                b.Tri(a, t, c, color);
            }
            DoubleQuad(b, P(-hx, hy, z1), P(hx, hy, z1), P(hx, 0f, tip), P(-hx, 0f, tip), edge);
            DoubleQuad(b, P(-hx, -hy, z1), P(hx, -hy, z1), P(hx, 0f, tip), P(-hx, 0f, tip), edge);
            DoubleQuad(b, P(-hx, hy, z1), P(hx, hy, z1), P(hx, -hy, z1), P(-hx, -hy, z1), edge);
        }

        private static void DoubleQuad(LowPolyMesh b, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color color)
        {
            b.Quad(p0, p1, p2, p3, color);
            b.Quad(p3, p2, p1, p0, color);
        }

        /// <summary>La palabra en las dos caras de la tabla (TextMeshPro se lee mirando hacia su +Z).</summary>
        private static void Labels(Transform parent, Signposts.Arrow arrow, float yaw, float y)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            // Centro del texto corrido hacia la punta: el poste tapa los primeros centimetros de la tabla.
            float mid = (ArrowLength - TipLength + PostWidth) * 0.5f;
            foreach (float side in new[] { 1f, -1f })
            {
                var labelGo = new GameObject("Label_" + arrow.Word + (side > 0f ? "_R" : "_L"));
                labelGo.transform.SetParent(parent, false);
                labelGo.transform.localPosition = rot * new Vector3(side * (ArrowThickness * 0.5f + 0.004f), y, mid);
                labelGo.transform.localRotation = rot * Quaternion.LookRotation(new Vector3(-side, 0f, 0f));
                var label = labelGo.AddComponent<TMPro.TextMeshPro>();
                var font = FontBuilder.LoadBookFont();
                if (font != null) label.font = font;
                label.rectTransform.sizeDelta = new Vector2(ArrowLength - TipLength - PostWidth - 0.08f, ArrowHeight - 0.03f);
                label.alignment = TMPro.TextAlignmentOptions.Center;
                label.enableAutoSizing = true;
                label.fontSizeMin = 0.1f;
                label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                label.fontSizeMax = 1.4f;
                label.fontStyle = TMPro.FontStyles.Bold;
                label.color = Signposts.TextOn(arrow.Color);
                label.text = arrow.Word;
            }
        }

        /// <summary>Piso real bajo el poste (colliders que no son trigger); si no hay nada, el terreno de ValleyShape.</summary>
        private static Vector3 GroundAt(Vector2 p)
        {
            Vector3 fallback = MapBuilder.Ground(p.x, p.y);
            var from = new Vector3(p.x, fallback.y + 8f, p.y);
            foreach (RaycastHit hit in Physics.RaycastAll(from, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.point.y > fallback.y - 0.5f && hit.point.y < fallback.y + 1f && hit.point.y > from.y - 12f)
                    fallback.y = Mathf.Max(fallback.y, hit.point.y);
            return fallback;
        }
    }
}
