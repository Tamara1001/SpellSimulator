using UnityEngine;
using UnityEngine.Rendering;
using SpellSimulator.Canvas;

namespace SpellSimulator.World
{
    /// <summary>
    /// Arma el marco de cara de disparo de un lienzo (dorado = frente, azul = espalda, con flecha y un boton en la esquina) con
    /// cubos primitivos: sirve igual en el editor (generador del mapa) y en juego (restocker). El lienzo es un quad 1×1 escalado a
    /// su tamaño, asi que todo se modela en metros dentro de un envoltorio que cancela esa escala. La cara visible mira a -Z.
    /// </summary>
    public static class CanvasFrameBuilder
    {
        private const float Lift = -0.004f;
        private const int RoundSegments = 24;

        public static CanvasFrame Attach(DrawingCanvas canvas, Material gold, Material blue, CanvasShape shape)
        {
            Transform ct = canvas.transform;
            Vector3 s = ct.localScale;
            float w = Mathf.Abs(s.x), h = Mathf.Abs(s.y);
            var wrapper = new GameObject("FaceFrame").transform;
            wrapper.SetParent(ct, false);
            wrapper.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);

            float t = Mathf.Clamp(0.02f * Mathf.Min(w, h), 0.008f, 0.03f);
            float k = Mathf.Clamp(Mathf.Min(w, h) / 0.5f, 0.3f, 1.2f);
            GameObject front = Variant(wrapper, "Front", gold, w, h, t, shape, -1f, k);
            GameObject back = Variant(wrapper, "Back", blue, w, h, t, shape, 1f, k);

            var frame = wrapper.gameObject.AddComponent<CanvasFrame>();
            frame.Configure(canvas, front, back);

            // Boton de la esquina superior derecha, fuera del papel (no estorba al dibujar).
            var button = new GameObject("FaceButton");
            button.transform.SetParent(wrapper, false);
            Vector2 corner = shape == CanvasShape.Round ? new Vector2(w * 0.5f * 0.7071f + 0.02f * k, h * 0.5f * 0.7071f + 0.02f * k) : new Vector2(w * 0.5f + t + 0.03f * k, h * 0.5f - 0.02f * k);
            button.transform.localPosition = new Vector3(corner.x, corner.y, -0.015f);
            var box = button.AddComponent<BoxCollider>();
            box.size = new Vector3(0.06f, 0.05f, 0.04f) * k;
            box.isTrigger = true;
            button.AddComponent<CanvasFaceButton>().Configure(frame);
            return frame;
        }

        private static GameObject Variant(Transform parent, string name, Material material, float w, float h, float t, CanvasShape shape, float arrowDir, float k)
        {
            var root = new GameObject("Frame_" + name).transform;
            root.SetParent(parent, false);
            if (shape == CanvasShape.Round)
            {
                float r = w * 0.5f + t * 0.5f;
                float step = 2f * Mathf.PI * r / RoundSegments;
                for (int i = 0; i < RoundSegments; i++)
                {
                    float a = i * 2f * Mathf.PI / RoundSegments;
                    Cube(root, material, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, Lift),
                        Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f), new Vector3(t, step * 1.1f, t));
                }
            }
            else
            {
                Cube(root, material, new Vector3(0f, h * 0.5f + t * 0.5f, Lift), Quaternion.identity, new Vector3(w + 2f * t, t, t));
                Cube(root, material, new Vector3(0f, -h * 0.5f - t * 0.5f, Lift), Quaternion.identity, new Vector3(w + 2f * t, t, t));
                Cube(root, material, new Vector3(-w * 0.5f - t * 0.5f, 0f, Lift), Quaternion.identity, new Vector3(t, h, t));
                Cube(root, material, new Vector3(w * 0.5f + t * 0.5f, 0f, Lift), Quaternion.identity, new Vector3(t, h, t));
            }
            // Flecha hacia donde sale el hechizo (-Z = por la cara visible, +Z = por la de atras), arriba a la izquierda.
            Vector2 at = shape == CanvasShape.Round ? new Vector2(-w * 0.35f, h * 0.35f) : new Vector2(-w * 0.5f + 0.05f * k, h * 0.5f + t + 0.01f * k);
            float len = 0.09f * k;
            Cube(root, material, new Vector3(at.x, at.y, Lift + arrowDir * len * 0.5f), Quaternion.identity, new Vector3(0.012f * k, 0.012f * k, len));
            Cube(root, material, new Vector3(at.x, at.y, Lift + arrowDir * len), Quaternion.Euler(0f, 45f, 0f), new Vector3(0.035f * k, 0.035f * k, 0.035f * k));
            // Pestaña del boton, a juego.
            Vector2 tab = shape == CanvasShape.Round ? new Vector2(w * 0.5f * 0.7071f + 0.02f * k, h * 0.5f * 0.7071f + 0.02f * k) : new Vector2(w * 0.5f + t + 0.03f * k, h * 0.5f - 0.02f * k);
            Cube(root, material, new Vector3(tab.x, tab.y, -0.012f), Quaternion.identity, new Vector3(0.05f, 0.04f, 0.02f) * k);
            return root.gameObject;
        }

        private static void Cube(Transform parent, Material material, Vector3 position, Quaternion rotation, Vector3 size)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Collider c = go.GetComponent<Collider>();
            if (Application.isPlaying) Object.Destroy(c); else Object.DestroyImmediate(c);
            go.name = "Part";
            Transform tr = go.transform;
            tr.SetParent(parent, false);
            tr.localPosition = position;
            tr.localRotation = rotation;
            tr.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }
}
