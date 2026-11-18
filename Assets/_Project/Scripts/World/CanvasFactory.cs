using UnityEngine;
using UnityEngine.Rendering;
using SpellSimulator.Canvas;

namespace SpellSimulator.World
{
    /// <summary>
    /// Crea en juego un lienzo o una libreta a partir de un <see cref="CanvasSpec"/> (restocker): clona la plantilla de papel, le da el tamaño
    /// del preset, la emision por la cara con tinta, el marco de cara de disparo y, si es libreta, hojas y tapa. Misma receta que el
    /// generador del mapa (CanvasDesigns), pero con cubos primitivos para que corra fuera del editor.
    /// </summary>
    public static class CanvasFactory
    {
        public static GameObject Spawn(GameObject paperTemplate, CanvasSpec spec, Vector3 position, Quaternion rotation, Material frameGold, Material frameBlue, Material cover)
        {
            spec = CanvasRules.Sanitize(spec);
            Vector2 d = CanvasRules.Dimensions(spec);
            GameObject go = Object.Instantiate(paperTemplate, position, rotation);
            go.SetActive(true);
            go.name = (spec.kind == CanvasKind.Notebook ? "Libreta_" : "Lienzo_") + spec.size;
            go.transform.localScale = new Vector3(d.x, d.y, paperTemplate.transform.localScale.z);

            // La plantilla es un objeto del mapa: lo que no corresponde a un lienzo nuevo se le quita.
            foreach (var r in go.GetComponentsInChildren<Respawnable>(true)) Object.Destroy(r);
            foreach (var old in go.GetComponentsInChildren<CanvasFrame>(true)) Object.Destroy(old.gameObject);

            var drawing = go.GetComponent<DrawingCanvas>();
            if (drawing != null)
            {
                drawing.SetEmissionSide(EmissionSide.InkSide);
                drawing.SetInkSign(CanvasRules.InkSign(spec.face));
                CanvasFrameBuilder.Attach(drawing, frameGold, frameBlue, spec.shape);
            }

            var physical = go.GetComponent<PhysicalCanvas>();
            if (physical != null) physical.SetObjectName(spec.kind == CanvasKind.Notebook ? "libreta" : "lienzo");

            var body = go.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = false;
                body.useGravity = true;
            }

            if (spec.kind == CanvasKind.Notebook)
            {
                var pages = go.GetComponent<NotebookPages>();
                if (pages == null) pages = go.AddComponent<NotebookPages>();
                pages.SetPageCount(spec.pages);
                AddCover(go.transform, spec, d, cover);
            }
            return go;
        }

        /// <summary>Tapa de cuero detras de las hojas y espiral a la izquierda (circular: disco).</summary>
        private static void AddCover(Transform canvas, CanvasSpec spec, Vector2 size, Material material)
        {
            var wrapper = new GameObject("Cover").transform;
            wrapper.SetParent(canvas, false);
            Vector3 s = canvas.localScale;
            wrapper.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
            if (spec.shape == CanvasShape.Round)
            {
                Part(wrapper, PrimitiveType.Cylinder, material, new Vector3(0f, 0f, 0.007f), Quaternion.Euler(90f, 0f, 0f), new Vector3(size.x + 0.03f, 0.005f, size.y + 0.03f));
                return;
            }
            Part(wrapper, PrimitiveType.Cube, material, new Vector3(0f, 0f, 0.007f), Quaternion.identity, new Vector3(size.x + 0.03f, size.y + 0.03f, 0.01f));
            for (float y = -size.y / 2f + 0.03f; y < size.y / 2f - 0.02f; y += 0.045f)
                Part(wrapper, PrimitiveType.Cube, material, new Vector3(-size.x / 2f - 0.004f, y, 0f), Quaternion.identity, new Vector3(0.022f, 0.012f, 0.016f));
        }

        private static void Part(Transform parent, PrimitiveType type, Material material, Vector3 position, Quaternion rotation, Vector3 size)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "Part";
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            t.localRotation = rotation;
            t.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
