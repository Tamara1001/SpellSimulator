using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SpellSimulator.Rendering;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Look anime/cel: agrega OutlineRendererFeature a cada UniversalRendererData que usa el proyecto (los de los
    /// UniversalRenderPipelineAsset de GraphicsSettings y de cada nivel de QualitySettings) y ajusta el material
    /// estilizado (bandas mas definidas y rim cel). Idempotente: si la feature ya esta, solo actualiza su configuracion.
    /// En los renderers moviles (Quest) va la ruta barata: sin normales (sin prepase DepthNormals).
    /// Menu: SpellSimulator > Rendering > Setup Outlines.
    /// </summary>
    public static class RenderingSetup
    {
        private const string StylizedPath = "Assets/_Project/Materials/Stylized.mat";

        [MenuItem("SpellSimulator/Rendering/Setup Outlines")]
        public static string Apply()
        {
            var shader = Shader.Find(OutlineRendererFeature.ShaderName);
            if (shader == null) return "Falta el shader " + OutlineRendererFeature.ShaderName;

            var log = new List<string>();
            foreach (var pair in UsedRendererData())
            {
                bool mobile = IsMobile(pair.Key, pair.Value);
                log.Add(AddOrUpdate(pair.Value, shader, mobile));
            }
            log.Add(ApplyCelLook());
            AssetDatabase.SaveAssets();
            string result = string.Join("\n", log);
            Debug.Log("[Rendering] " + result);
            return result;
        }

        /// <summary>Bandas un poco mas nitidas y rim cel suave en el material compartido del mundo.</summary>
        public static string ApplyCelLook()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(StylizedPath);
            if (m == null) return "Sin " + StylizedPath;
            m.SetFloat("_Bands", 3f);
            m.SetFloat("_Softness", 0.06f);
            m.SetFloat("_RimStrength", 0.12f);
            m.SetFloat("_RimSharpness", 1f);
            m.SetFloat("_RimCut", 0.66f);
            EditorUtility.SetDirty(m);
            return "Stylized.mat: bandas 3 (suavidad 0.06), rim cel.";
        }

        private static string AddOrUpdate(UniversalRendererData data, Shader shader, bool mobile)
        {
            OutlineRendererFeature feature = null;
            foreach (var f in data.rendererFeatures)
                if (f is OutlineRendererFeature o) { feature = o; break; }

            bool created = feature == null;
            if (created)
            {
                feature = ScriptableObject.CreateInstance<OutlineRendererFeature>();
                feature.name = "Outline";
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

                var so = new SerializedObject(data);
                var features = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                int index = features.arraySize;
                features.arraySize++;
                features.GetArrayElementAtIndex(index).objectReferenceValue = feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(index).longValue = localId;
                so.ApplyModifiedProperties();
            }

            var s = feature.settings;
            s.shader = shader;
            s.useNormals = !mobile;
            s.thickness = mobile ? 1.1f : 1.25f;
            s.inkColor = new Color(0.16f, 0.09f, 0.14f, 0.9f);
            s.depthThreshold = 0.12f;
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
            data.SetDirty();
            return $"{data.name}: contorno {(created ? "agregado" : "actualizado")} ({(mobile ? "barato, sin normales" : "con normales")}).";
        }

        private static bool IsMobile(UniversalRenderPipelineAsset asset, UniversalRendererData data)
        {
            string n = (asset.name + " " + data.name).ToLowerInvariant();
            return n.Contains("mobile") || n.Contains("quest") || n.Contains("android");
        }

        /// <summary>Renderer Data de los RP asset de GraphicsSettings y de todos los niveles de calidad (sin repetir).</summary>
        private static List<KeyValuePair<UniversalRenderPipelineAsset, UniversalRendererData>> UsedRendererData()
        {
            var assets = new List<UniversalRenderPipelineAsset>();
            void AddAsset(RenderPipelineAsset rp)
            {
                if (rp is UniversalRenderPipelineAsset urp && !assets.Contains(urp)) assets.Add(urp);
            }
            AddAsset(GraphicsSettings.defaultRenderPipeline);
            for (int i = 0; i < QualitySettings.count; i++) AddAsset(QualitySettings.GetRenderPipelineAssetAt(i));

            var result = new List<KeyValuePair<UniversalRenderPipelineAsset, UniversalRendererData>>();
            var seen = new HashSet<UniversalRendererData>();
            foreach (var asset in assets)
            {
                var list = new SerializedObject(asset).FindProperty("m_RendererDataList");
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue is UniversalRendererData data && seen.Add(data))
                        result.Add(new KeyValuePair<UniversalRenderPipelineAsset, UniversalRendererData>(asset, data));
                }
            }
            return result;
        }
    }
}
