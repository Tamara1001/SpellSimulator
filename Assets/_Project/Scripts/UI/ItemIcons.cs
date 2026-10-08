using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.UI
{
    /// <summary>
    /// Miniaturas 3D de los objetos para la barra de la HUD. Una vez por objeto (cacheada) clona su parte visible
    /// (solo MeshFilter/MeshRenderer, sin scripts ni colliders: el clon no se despierta porque nace bajo un padre
    /// inactivo), lo lleva a un escenario lejano en una capa dedicada y lo fotografia con una camara ortografica oculta
    /// a una RenderTexture de 128x128 con fondo transparente. Funciona en build y con objetos desactivados (guardados en
    /// la barra). Si el objeto no tiene nada que dibujar devuelve null y la HUD usa su color (IHudInfo.HudColor).
    /// </summary>
    public static class ItemIcons
    {
        public const int Size = 128;
        /// <summary>Capa sin nombre que no usa nadie: solo la ve la camara de miniaturas.</summary>
        public const int IconLayer = 31;

        private static readonly Vector3 StagePosition = new Vector3(0f, -4000f, 0f);
        private static readonly Dictionary<GameObject, RenderTexture> cache = new Dictionary<GameObject, RenderTexture>();
        private static readonly List<Component> components = new List<Component>(32);
        private static readonly List<Renderer> renderers = new List<Renderer>(16);
        private static readonly List<GameObject> stale = new List<GameObject>(8);

        private static GameObject stage;
        private static Camera iconCamera;

        /// <summary>Miniatura del objeto (la genera la primera vez). Null si no tiene renderers.</summary>
        public static Texture Get(GameObject item)
        {
            if (item == null) return null;
            if (cache.TryGetValue(item, out RenderTexture rt))
            {
                if (rt == null) return null;
                if (rt.IsCreated()) return rt;
            }
            PruneDestroyed();
            rt = Render(item, rt);
            cache[item] = rt;
            return rt;
        }

        /// <summary>Vuelve a fotografiar el objeto la proxima vez que se pida (cambio de forma o de material).</summary>
        public static void Invalidate(GameObject item)
        {
            if (item == null || !cache.TryGetValue(item, out RenderTexture rt)) return;
            cache.Remove(item);
            if (rt != null) { rt.Release(); Object.Destroy(rt); }
        }

        private static void PruneDestroyed()
        {
            stale.Clear();
            foreach (var pair in cache) if (pair.Key == null) stale.Add(pair.Key);
            foreach (var key in stale)
            {
                if (cache.TryGetValue(key, out RenderTexture rt) && rt != null) { rt.Release(); Object.Destroy(rt); }
                cache.Remove(key);
            }
        }

        private static RenderTexture Render(GameObject item, RenderTexture reuse)
        {
            EnsureStage();
            // El escenario (con sus luces) solo esta activo durante la foto: asi sus luces direccionales nunca compiten
            // con el sol de la escena por ser la luz principal de URP.
            stage.SetActive(true);
            var holder = new GameObject("IconClone");
            holder.SetActive(false);
            holder.transform.SetParent(stage.transform, false);
            GameObject clone = Object.Instantiate(item, holder.transform, false);
            try
            {
                clone.transform.localPosition = Vector3.zero;
                clone.transform.localRotation = Quaternion.identity;
                clone.transform.localScale = item.transform.lossyScale;
                clone.SetActive(true);
                Strip(clone);
                SetLayer(clone.transform);
                holder.SetActive(true);

                if (!CollectBounds(clone, out Bounds bounds)) return null;
                // Eje largo en diagonal, cara mas ancha hacia la camara y un poco de inclinacion para que se lea en 3D.
                clone.transform.localRotation = Frame(bounds.size);
                CollectBounds(clone, out bounds);

                float half = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.12f;
                if (half < 1e-4f) return null;
                float depth = bounds.extents.z + 1f;
                Transform cam = iconCamera.transform;
                cam.position = bounds.center - Vector3.forward * depth;
                cam.rotation = Quaternion.identity;
                iconCamera.orthographicSize = half;
                iconCamera.nearClipPlane = 0.01f;
                iconCamera.farClipPlane = depth * 2f + 0.5f;

                RenderTexture rt = reuse != null ? reuse : new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    name = "Icono " + item.name,
                    antiAliasing = 4,
                };
                if (!rt.IsCreated()) rt.Create();
                iconCamera.targetTexture = rt;
                bool fog = RenderSettings.fog;
                RenderSettings.fog = false;
                iconCamera.Render();
                RenderSettings.fog = fog;
                iconCamera.targetTexture = null;
                return rt;
            }
            finally
            {
                holder.SetActive(false);
                Object.Destroy(holder);
                stage.SetActive(false);
            }
        }

        /// <summary>Rotacion que deja el eje mas largo en diagonal y el mas corto mirando a la camara.</summary>
        private static Quaternion Frame(Vector3 size)
        {
            Vector3 x = Vector3.right, y = Vector3.up, z = Vector3.forward;
            // Ordenar ejes por tamaño: largo, medio, corto.
            float[] s = { size.x, size.y, size.z };
            Vector3[] axes = { x, y, z };
            for (int i = 0; i < 3; i++)
                for (int j = i + 1; j < 3; j++)
                    if (s[j] > s[i]) { (s[i], s[j]) = (s[j], s[i]); (axes[i], axes[j]) = (axes[j], axes[i]); }
            Quaternion align = Quaternion.Inverse(Quaternion.LookRotation(axes[2], axes[1]));
            bool elongated = s[0] > s[1] * 1.8f;
            Quaternion roll = Quaternion.AngleAxis(elongated ? 40f : 0f, Vector3.forward);
            return Quaternion.Euler(20f, -24f, 0f) * roll * align;
        }

        /// <summary>Deja solo lo que se dibuja: scripts, fisica, luces, audio y particulas afuera.</summary>
        private static void Strip(GameObject clone)
        {
            // Varias pasadas: un componente requerido por otro (RequireComponent) se puede quitar recien despues.
            for (int pass = 0; pass < 3; pass++)
            {
                clone.GetComponentsInChildren(true, components);
                bool removedAny = false;
                for (int i = components.Count - 1; i >= 0; i--)
                {
                    Component c = components[i];
                    if (c == null || c is Transform || c is MeshFilter || c is MeshRenderer || c is SkinnedMeshRenderer) continue;
                    if (pass == 0 && !(c is MonoBehaviour)) continue;
                    if (pass == 1 && (c is Rigidbody || c is Collider)) continue;
                    Object.DestroyImmediate(c);
                    removedAny = true;
                }
                if (!removedAny && pass == 2) break;
            }
            components.Clear();
        }

        private static void SetLayer(Transform t)
        {
            t.gameObject.layer = IconLayer;
            for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i));
        }

        private static bool CollectBounds(GameObject clone, out Bounds bounds)
        {
            bounds = default;
            clone.GetComponentsInChildren(false, renderers);
            bool any = false;
            foreach (Renderer r in renderers)
            {
                if (!r.enabled || r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
                if (r is MeshRenderer && (!r.TryGetComponent(out MeshFilter mf) || mf.sharedMesh == null)) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            renderers.Clear();
            return any;
        }

        private static void EnsureStage()
        {
            if (stage != null) return;
            stage = new GameObject("ItemIconStage");
            stage.hideFlags = HideFlags.HideAndDontSave;
            stage.transform.position = StagePosition;
            Object.DontDestroyOnLoad(stage);

            var camGo = new GameObject("IconCamera");
            camGo.transform.SetParent(stage.transform, false);
            iconCamera = camGo.AddComponent<Camera>();
            iconCamera.enabled = false;
            iconCamera.orthographic = true;
            iconCamera.clearFlags = CameraClearFlags.SolidColor;
            iconCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            iconCamera.cullingMask = 1 << IconLayer;
            iconCamera.allowHDR = false;
            iconCamera.allowMSAA = true;
            iconCamera.useOcclusionCulling = false;

            // Luz suave y calida de arriba a la izquierda + relleno frio: solo ilumina la capa de miniaturas.
            AddLight(stage.transform, Quaternion.Euler(38f, -40f, 0f), new Color(1f, 0.93f, 0.8f), 1.15f);
            AddLight(stage.transform, Quaternion.Euler(-10f, 150f, 0f), new Color(0.7f, 0.72f, 0.95f), 0.45f);
            stage.SetActive(false);
        }

        private static void AddLight(Transform parent, Quaternion rotation, Color color, float intensity)
        {
            var go = new GameObject("IconLight");
            go.transform.SetParent(parent, false);
            go.transform.rotation = rotation;
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            light.cullingMask = 1 << IconLayer;
        }
    }
}
