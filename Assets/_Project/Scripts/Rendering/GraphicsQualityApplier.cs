using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SpellSimulator.Rendering
{
    /// <summary>
    /// Aplica <see cref="GraphicsQuality"/> al URP asset activo en runtime (render scale, MSAA, sombras) y al contorno de
    /// pantalla (_OutlineStrength global). Se aplica al cargar la escena y cada vez que cambia el nivel. En el Editor
    /// guarda los valores originales del asset y los restaura al salir de Play, para no ensuciar el asset.
    /// Los niveles de Quality de Unity (Mobile/PC) son por plataforma, no por calidad: no se tocan.
    /// </summary>
    public static class GraphicsQualityApplier
    {
        private static bool hooked;

#if UNITY_EDITOR
        private static UniversalRenderPipelineAsset savedAsset;
        private static float sScale, sDistance;
        private static int sCascades, sMsaa;
        private static bool sSoft;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (!hooked)
            {
                hooked = true;
                GraphicsQuality.Changed += Apply;
#if UNITY_EDITOR
                UnityEditor.EditorApplication.playModeStateChanged += OnPlayMode;
#endif
            }
            Apply(GraphicsQuality.Current);
        }

        public static void Apply(GraphicsQualityLevel level)
        {
            var p = GraphicsQuality.Get(level);
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
#if UNITY_EDITOR
                if (savedAsset != urp)
                {
                    Restore();
                    savedAsset = urp;
                    sScale = urp.renderScale; sDistance = urp.shadowDistance; sCascades = urp.shadowCascadeCount;
                    sMsaa = urp.msaaSampleCount; sSoft = urp.supportsSoftShadows;
                }
#endif
                urp.renderScale = p.RenderScale;
                urp.msaaSampleCount = p.MsaaSamples;
                urp.shadowDistance = p.ShadowDistance;
                urp.shadowCascadeCount = p.ShadowCascades;
            }
            OutlineRendererFeature.SetGlobalStrength(p.Outline ? 1f : 0f);
        }

#if UNITY_EDITOR
        private static void OnPlayMode(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode) Restore();
        }

        private static void Restore()
        {
            if (savedAsset == null) return;
            savedAsset.renderScale = sScale; savedAsset.shadowDistance = sDistance;
            savedAsset.shadowCascadeCount = sCascades; savedAsset.msaaSampleCount = sMsaa;
        }
#endif
    }
}
