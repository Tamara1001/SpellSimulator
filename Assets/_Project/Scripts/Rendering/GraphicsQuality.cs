using System;
using UnityEngine;

namespace SpellSimulator.Rendering
{
    public enum GraphicsQualityLevel { Low = 0, Medium = 1, High = 2 }

    /// <summary>
    /// Calidad grafica elegida en las Opciones del grimorio (Baja / Media / Alta). Logica pura: niveles, valores por nivel,
    /// default segun plataforma y ciclado. El guardado va en PlayerPrefs y la aplicacion a URP en
    /// <see cref="GraphicsQualityApplier"/>.
    /// </summary>
    public static class GraphicsQuality
    {
        public const string PrefKey = "gfxQuality";

        /// <summary>Valores que cambian con el nivel.</summary>
        public readonly struct Preset
        {
            public readonly float RenderScale, ShadowDistance, ParticleScale;
            public readonly int ShadowCascades, MsaaSamples;
            public readonly bool SoftShadows, Outline;

            public Preset(float renderScale, float shadowDistance, int cascades, bool softShadows, int msaa, bool outline, float particleScale)
            {
                RenderScale = renderScale; ShadowDistance = shadowDistance; ShadowCascades = cascades;
                SoftShadows = softShadows; MsaaSamples = msaa; Outline = outline; ParticleScale = particleScale;
            }
        }

        private static readonly Preset[] Presets =
        {
            new Preset(0.7f, 15f, 1, false, 1, false, 0.5f),
            new Preset(0.85f, 30f, 2, false, 2, true, 0.75f),
            new Preset(1f, 60f, 4, true, 4, true, 1f),
        };

        public static Preset Get(GraphicsQualityLevel level) => Presets[Clamp((int)level)];

        /// <summary>Default: WebGL y Android (Quest) = Media, el resto (PC) = Alta.</summary>
        public static GraphicsQualityLevel DefaultFor(RuntimePlatform platform) =>
            platform == RuntimePlatform.WebGLPlayer || platform == RuntimePlatform.Android
                ? GraphicsQualityLevel.Medium
                : GraphicsQualityLevel.High;

        /// <summary>Baja -> Media -> Alta -> Baja.</summary>
        public static GraphicsQualityLevel Next(GraphicsQualityLevel level) => (GraphicsQualityLevel)((Clamp((int)level) + 1) % 3);

        /// <summary>Nombre para el boton.</summary>
        public static string Label(GraphicsQualityLevel level)
        {
            switch (level)
            {
                case GraphicsQualityLevel.Low: return "Baja";
                case GraphicsQualityLevel.Medium: return "Media";
                default: return "Alta";
            }
        }

        /// <summary>Un valor guardado que no es un nivel valido cae al default.</summary>
        public static GraphicsQualityLevel Sanitize(int stored, GraphicsQualityLevel fallback) =>
            stored >= 0 && stored <= 2 ? (GraphicsQualityLevel)stored : fallback;

        private static int Clamp(int i) => Math.Max(0, Math.Min(2, i));

        // ---------------------------------------------------------------- estado guardado

        private static int cached = -1;

        /// <summary>Se dispara al cambiar el nivel (lo aplica <see cref="GraphicsQualityApplier"/>).</summary>
        public static event Action<GraphicsQualityLevel> Changed;

        public static GraphicsQualityLevel Current
        {
            get
            {
                if (cached < 0)
                    cached = (int)Sanitize(PlayerPrefs.GetInt(PrefKey, -1), DefaultFor(Application.platform));
                return (GraphicsQualityLevel)cached;
            }
            set
            {
                cached = Clamp((int)value);
                PlayerPrefs.SetInt(PrefKey, cached);
                Changed?.Invoke((GraphicsQualityLevel)cached);
            }
        }

        /// <summary>Escala de maxParticles para el nivel actual (ElementVfx.Apply).</summary>
        public static float ParticleScale => Get(Current).ParticleScale;
    }
}
