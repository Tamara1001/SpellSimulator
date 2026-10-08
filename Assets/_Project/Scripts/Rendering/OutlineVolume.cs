using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SpellSimulator.Rendering
{
    /// <summary>
    /// Override de Volume para el contorno entintado (OutlineRendererFeature). Sin ningun Volume que lo pise vale el
    /// default (intensidad 1 = contorno normal); un Volume con intensidad 0 lo apaga en esa zona y la feature ni siquiera
    /// encola el pase. Color y grosor tambien se pueden pisar por Volume.
    /// </summary>
    [System.Serializable]
    [VolumeComponentMenu("SpellSimulator/Outline")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class OutlineVolume : VolumeComponent
    {
        /// <summary>Multiplicador del contorno (0 apaga).</summary>
        public ClampedFloatParameter intensity = new ClampedFloatParameter(1f, 0f, 1f);
        /// <summary>Si esta overrideado, reemplaza el color de tinta de la feature.</summary>
        public ColorParameter inkColor = new ColorParameter(new UnityEngine.Color(0.16f, 0.09f, 0.14f, 0.9f));
        /// <summary>Multiplicador del grosor configurado en la feature.</summary>
        public ClampedFloatParameter thicknessScale = new ClampedFloatParameter(1f, 0.25f, 3f);
    }
}
