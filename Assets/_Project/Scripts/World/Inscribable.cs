using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;

namespace SpellSimulator.World
{
    /// <summary>
    /// Superficie del entorno donde se puede dibujar un sello (el piso, las paredes de la torre, cajas, pesas...).
    /// Al empezar un trazo sobre ella, la herramienta recibe una "calcomania" de tinta (un lienzo transparente pegado
    /// a la superficie, del tamaño de decalSize); si ya hay una bajo el rayo se reutiliza. El hechizo sale por la cara
    /// que mira hacia afuera de la superficie. Ver <see cref="InscribedDecal"/>.
    /// </summary>
    public class Inscribable : MonoBehaviour
    {
        public const int MaxDecals = 10;

        [SerializeField, Tooltip("Lado (m) de la calcomania que se crea al dibujar.")]
        private float decalSize = 1.2f;
        [SerializeField, Tooltip("Resolucion por metro de la calcomania (textura del lienzo).")]
        private int pixelsPerMeter = 300;
        [SerializeField] private Material decalMaterial;
        [SerializeField] private Material brushMaterial;
        [SerializeField] private SpellCaster caster;

        private static readonly List<InscribedDecal> decals = new List<InscribedDecal>();

        public static int DecalCount => decals.Count;

        public void Configure(float size, Material decal, Material brush, SpellCaster spellCaster)
        {
            decalSize = size;
            pixelsPerMeter = 300;
            decalMaterial = decal;
            brushMaterial = brush;
            caster = spellCaster;
        }

        /// <summary>Crea la calcomania en el punto golpeado. viewDirection = hacia donde mira quien dibuja.</summary>
        public DrawingCanvas CreateDecal(RaycastHit hit, Vector3 viewDirection, float sizeOverride = 0f)
        {
            // Un sello chico en una calcomania enorme no se lee (el anillo tiene un radio minimo relativo al lienzo):
            // las estampas piden una calcomania a su medida.
            float size = sizeOverride > 0f ? sizeOverride : decalSize;
            if (decalMaterial == null || brushMaterial == null) return null;

            Vector3 normal = hit.normal;
            Vector3 top = Vector3.ProjectOnPlane(viewDirection, normal);
            // Piso: el borde de arriba se aleja de quien mira. Pared: el borde de arriba es arriba.
            if (Mathf.Abs(normal.y) < 0.6f || top.sqrMagnitude < 1e-4f) top = Vector3.up;
            Quaternion rotation = Quaternion.LookRotation(-normal, top);

            var go = new GameObject("InkDecal") { layer = LayerMask.NameToLayer("Canvas") };
            go.SetActive(false); // los componentes se despiertan al activarlo, con todo ya configurado
            go.transform.SetPositionAndRotation(hit.point + normal * 0.006f, rotation);
            go.transform.localScale = new Vector3(size, size, 0.02f);

            go.AddComponent<MeshFilter>().sharedMesh = Quad();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = decalMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.AddComponent<BoxCollider>().size = Vector3.one;

            var canvas = go.AddComponent<DrawingCanvas>();
            canvas.Prepare(Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(pixelsPerMeter * size, 384f)), 128, 1024), brushMaterial, new Color(0f, 0f, 0f, 0f), 0.02f, EmissionSide.InkSide);
            var decal = go.AddComponent<InscribedDecal>();
            decal.Init(canvas, caster, hit.collider.transform, size);
            decal.SetOwner(this);
            go.SetActive(true);

            Register(decal);
            return canvas;
        }

        private static void Register(InscribedDecal decal)
        {
            decals.Add(decal);
            while (decals.Count > MaxDecals)
            {
                if (decals[0] != null) Destroy(decals[0].gameObject);
                decals.RemoveAt(0);
            }
        }

        internal static void Unregister(InscribedDecal decal) => decals.Remove(decal);

        /// <summary>Borra todas las calcomanias (reinicio del mundo).</summary>
        public static void ClearAll()
        {
            for (int i = decals.Count - 1; i >= 0; i--)
                if (decals[i] != null) Destroy(decals[i].gameObject);
            decals.Clear();
        }

        private static Mesh quad;

        private static Mesh Quad()
        {
            if (quad != null) return quad;
            // Misma orientacion que el lienzo de papel: cara visible hacia -Z, bounds de -0.5 a 0.5.
            quad = new Mesh { name = "InkDecalQuad" };
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            quad.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            quad.RecalculateBounds();
            return quad;
        }
    }
}
