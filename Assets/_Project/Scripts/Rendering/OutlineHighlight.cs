using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SpellSimulator.Learning;
using SpellSimulator.World;

namespace SpellSimulator.Rendering
{
    /// <summary>
    /// Marca un objeto como "resaltado": le agrega un contorno fino de color calido (casco invertido, shader
    /// SpellSimulator/OutlineHighlight en Resources/) ademas del contorno cartoon de pantalla, que no se toca.
    /// Crea un hijo "ContornoResaltado" por cada MeshFilter (sin colliders, sin sombras). Se aplica solo a libros y a
    /// herramientas sueltas del mundo (pluma/pincel, goma, tintero, estampas) al cargar la escena (<see cref="AutoApply"/>).
    /// </summary>
    public sealed class OutlineHighlight : MonoBehaviour
    {
        public const string ShaderName = "SpellSimulator/OutlineHighlight";
        public const string HullName = "ContornoResaltado";

        public Color color = new Color(1f, 0.8f, 0.5f, 1f);
        [Tooltip("Ancho del contorno en metros de mundo.")]
        public float width = 0.0035f;

        private Material material;
        private readonly List<GameObject> hulls = new List<GameObject>();

        // Valores para el proximo componente (Awake corre dentro de AddComponent).
        private static float nextWidth = -1f;
        private static Color nextColor;

        /// <summary>Contorno leve de las herramientas sueltas.</summary>
        public const float ToolWidth = 0.0018f;
        /// <summary>Contorno marcado de los libros.</summary>
        public const float BookWidth = 0.0075f;

        private void Awake()
        {
            if (nextWidth > 0f) { width = nextWidth; color = nextColor; nextWidth = -1f; }
            var shader = Shader.Find(ShaderName);
            if (shader == null) return;
            material = new Material(shader) { name = "OutlineHighlight (runtime)", hideFlags = HideFlags.DontSave };
            material.SetColor("_Color", color);
            material.SetFloat("_Width", width);

            var filters = GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                var mf = filters[i];
                if (mf.sharedMesh == null || mf.gameObject.name == HullName) continue;
                var src = mf.GetComponent<MeshRenderer>();
                if (src == null) continue;
                var go = new GameObject(HullName) { layer = mf.gameObject.layer };
                go.transform.SetParent(mf.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var mr = go.AddComponent<MeshRenderer>();
                int subs = Mathf.Max(1, mf.sharedMesh.subMeshCount);
                var mats = new Material[subs];
                for (int s = 0; s < subs; s++) mats[s] = material;
                mr.sharedMaterials = mats;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                hulls.Add(go);
            }
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }

        /// <summary>Muestra u oculta el contorno (p. ej. mientras se sostiene el objeto).</summary>
        public void SetVisible(bool visible)
        {
            for (int i = 0; i < hulls.Count; i++) if (hulls[i] != null) hulls[i].SetActive(visible);
        }

        /// <summary>Agrega el resaltado a un objeto (idempotente).</summary>
        public static OutlineHighlight Mark(GameObject go) => Mark(go, 0.0035f, new Color(1f, 0.8f, 0.5f, 1f));

        public static OutlineHighlight Mark(GameObject go, float width, Color color)
        {
            var h = go.GetComponent<OutlineHighlight>();
            if (h != null) return h;
            nextWidth = width;
            nextColor = color;
            return go.AddComponent<OutlineHighlight>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoApply()
        {
            // Una sola pasada al cargar: libros, herramientas, tinteros y estampas.
            foreach (var b in Object.FindObjectsByType<LibraryBook>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) Mark(b.gameObject, BookWidth, new Color(1f, 0.86f, 0.5f, 1f));
            foreach (var t in Object.FindObjectsByType<PhysicalTool>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) Mark(t.gameObject, ToolWidth, new Color(0.85f, 0.72f, 0.55f, 1f));
            foreach (var i in Object.FindObjectsByType<Inkwell>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) Mark(i.gameObject, ToolWidth, new Color(0.85f, 0.72f, 0.55f, 1f));
            foreach (var s in Object.FindObjectsByType<Stamp>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) Mark(s.gameObject, ToolWidth, new Color(0.85f, 0.72f, 0.55f, 1f));
        }
    }
}
