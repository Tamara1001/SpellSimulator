using UnityEngine;
using UnityEngine.Rendering;
using SpellSimulator.Canvas;

namespace SpellSimulator.World
{
    /// <summary>
    /// Crea en juego un lienzo o una libreta a partir de un <see cref="CanvasSpec"/> (restocker): clona la plantilla de papel, le da el tamaño
    /// del preset, la emision por la cara con tinta, y, si es libreta, hojas y tapa. Misma receta que el
    /// generador del mapa (CanvasDesigns), pero con cubos primitivos para que corra fuera del editor.
    /// </summary>
    public static class CanvasFactory
    {
        public static GameObject Spawn(GameObject paperTemplate, CanvasSpec spec, Vector3 position, Quaternion rotation, Material cover)
        {
            spec = CanvasRules.Sanitize(spec);
            Vector2 d = CanvasRules.Dimensions(spec);
            GameObject go = Object.Instantiate(paperTemplate, position, rotation);
            go.SetActive(true);
            go.name = (spec.kind == CanvasKind.Notebook ? "Libreta_" : "Lienzo_") + spec.size;
            go.transform.localScale = new Vector3(d.x, d.y, paperTemplate.transform.localScale.z);

            // La plantilla es un objeto del mapa: lo que no corresponde a un lienzo nuevo se le quita.
            foreach (var r in go.GetComponentsInChildren<Respawnable>(true)) Object.Destroy(r);

            var drawing = go.GetComponent<DrawingCanvas>();
            if (drawing != null)
            {
                drawing.SetEmissionSide(EmissionSide.InkSide);
                drawing.SetInkSign(1f);
                drawing.SetReversedByEasel(false);
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
                if (spec.shape == CanvasShape.Round && go.TryGetComponent(out MeshFilter pageMesh)) pageMesh.sharedMesh = DiscMesh();
                var pages = go.GetComponent<NotebookPages>();
                if (pages == null) pages = go.AddComponent<NotebookPages>();
                pages.SetPageCount(spec.pages);
                AddCover(go.transform, spec, d, cover);
            }
            return go;
        }

        private static Mesh discMesh;

        /// <summary>Disco unitario (diametro 1, en el plano XY, cara visible hacia -Z, UV de 0 a 1 como el quad): las hojas de la libreta circular.</summary>
        private static Mesh DiscMesh()
        {
            if (discMesh != null) return discMesh;
            const int sides = 40;
            var vertices = new Vector3[sides + 1];
            var normals = new Vector3[sides + 1];
            var uv = new Vector2[sides + 1];
            var triangles = new int[sides * 3];
            vertices[0] = Vector3.zero;
            uv[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                var v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.5f;
                vertices[i + 1] = new Vector3(v.x, v.y, 0f);
                uv[i + 1] = v + new Vector2(0.5f, 0.5f);
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % sides + 1;
                triangles[i * 3 + 2] = i + 1;
            }
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.back;
            discMesh = new Mesh { name = "NotebookDisc", vertices = vertices, normals = normals, uv = uv, triangles = triangles };
            discMesh.RecalculateBounds();
            return discMesh;
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
