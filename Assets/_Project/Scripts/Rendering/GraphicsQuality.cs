using System;
using UnityEngine;
using SpellSimulator.Player;

namespace SpellSimulator.Rendering
{
    public enum GraphicsQualityLevel { Low = 0, Medium = 1, High = 2 }

    /// <summary>Efectos de pantalla completa cuyo estado se decide segun la plataforma.</summary>
    public enum PostProcessEffect { Vignette, MotionBlur, DepthOfField, FilmGrain, LensDistortion, ChromaticAberration, Bloom, Tonemapping, ColorAdjustments }

    /// <summary>Politica pura de efectos de post-procesado por plataforma.</summary>
    public static class PostProcessPolicy
    {
        /// <summary>En PC conserva el estado del perfil; en VR desactiva efectos costosos o de confort.</summary>
        public static bool IsActive(PostProcessEffect effect, bool isVr)
        {
            if (!isVr) return true;
            switch (effect)
            {
                case PostProcessEffect.Vignette:
                case PostProcessEffect.MotionBlur:
                case PostProcessEffect.DepthOfField:
                case PostProcessEffect.FilmGrain:
                case PostProcessEffect.LensDistortion:
                case PostProcessEffect.ChromaticAberration:
                    return false;
                default:
                    return true;
            }
        }
    }

    /// <summary>
    /// Presupuesto y multiplicadores de particulas segun perfil de calidad y visor VR (VFX-02).
    /// Constantes aprobadas: Quest/PCVR = 0.5 (tope 2.000); PC Baja = 0.5 (tope 2.500);
    /// PC Media = 0.75 (tope 4.500); PC Alta = 1.0 (tope 8.000).
    /// </summary>
    public static class ParticleBudget
    {
        public const float VrScale = 0.5f;
        public const int VrMaxAlive = 2000;

        public const float LowScale = 0.5f;
        public const int LowMaxAlive = 2500;

        public const float MediumScale = 0.75f;
        public const int MediumMaxAlive = 4500;

        public const float HighScale = 1.0f;
        public const int HighMaxAlive = 8000;

        /// <summary>Calculo estatico y puro del multiplicador segun perfil y plataforma VR.</summary>
        public static float CalculateScale(GraphicsQualityLevel level, bool isVr)
        {
            if (isVr) return VrScale;
            switch (level)
            {
                case GraphicsQualityLevel.Low: return LowScale;
                case GraphicsQualityLevel.Medium: return MediumScale;
                case GraphicsQualityLevel.High: return HighScale;
                default: return HighScale;
            }
        }

        public static float CalculateScale(GraphicsQualityLevel level) => CalculateScale(level, false);

        /// <summary>Calculo estatico y puro del tope global de particulas vivas segun perfil y VR.</summary>
        public static int CalculateMaxAlive(GraphicsQualityLevel level, bool isVr)
        {
            if (isVr) return VrMaxAlive;
            switch (level)
            {
                case GraphicsQualityLevel.Low: return LowMaxAlive;
                case GraphicsQualityLevel.Medium: return MediumMaxAlive;
                case GraphicsQualityLevel.High: return HighMaxAlive;
                default: return HighMaxAlive;
            }
        }

        public static int CalculateMaxAlive(GraphicsQualityLevel level) => CalculateMaxAlive(level, false);

        /// <summary>Multiplicador de particulas activo (0..1) segun perfil actual y VR.</summary>
        public static float Scale => CalculateScale(GraphicsQuality.Current, PlatformDetector.IsVR);

        /// <summary>Tope global de particulas vivas para el perfil actual y VR.</summary>
        public static int MaxAlive => CalculateMaxAlive(GraphicsQuality.Current, PlatformDetector.IsVR);
    }

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
            new Preset(0.7f, 15f, 1, false, 1, false, ParticleBudget.LowScale),
            new Preset(0.85f, 30f, 2, false, 2, true, ParticleBudget.MediumScale),
            new Preset(1f, 60f, 4, true, 4, true, ParticleBudget.HighScale),
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
        public static float ParticleScale => ParticleBudget.Scale;
    }
}
