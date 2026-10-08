using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace SpellSimulator.Rendering
{
    /// <summary>
    /// Contorno entintado de pantalla (look anime/cel). Un pase fullscreen (RenderGraph) despues de los opacos y antes de
    /// los transparentes: detecta siluetas con el laplaciano de la profundidad inversa (cero en cualquier plano, aunque
    /// se lo mire rasante, asi el suelo lejano no se ensucia) y, si useNormals, aristas vivas por normales
    /// (_CameraNormalsTexture). Como va antes de los transparentes, el agua, las particulas y la UI se dibujan encima sin
    /// contorno; el cielo nunca se entinta (la tinta cae del lado cercano de la silueta).
    /// Apagado: Shader.SetGlobalFloat("_OutlineStrength", 0) (o SetGlobalStrength), un Volume con OutlineVolume.intensity
    /// = 0, o desactivando la feature en el Renderer Data. En los tres casos el pase no se encola (costo cero, ni copia de
    /// profundidad). Ruta barata (Quest): useNormals = false (sin prepase de normales; 5 lecturas de profundidad por pixel).
    /// Se agrega a los Renderer Data con SpellSimulator > Rendering > Setup Outlines.
    /// </summary>
    public sealed class OutlineRendererFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public sealed class Settings
        {
            [Tooltip("Shader Hidden/SpellSimulator/Outline (se referencia para que entre en la build).")]
            public Shader shader;
            [Tooltip("Color de la tinta (alfa = opacidad). Oscuro violaceo/marron, no negro puro.")]
            public Color inkColor = new Color(0.16f, 0.09f, 0.14f, 0.9f);
            [Tooltip("Grosor en pixeles a 1080p (se escala con la altura del render).")]
            [Range(0.5f, 3f)] public float thickness = 1.25f;
            [Tooltip("Factor de grosor a la distancia de fade (las lineas se afinan con la distancia).")]
            [Range(0.2f, 1f)] public float farThinning = 0.6f;
            [Tooltip("Salto relativo de profundidad que cuenta como silueta.")]
            [Range(0.01f, 1f)] public float depthThreshold = 0.12f;
            [Tooltip("Aristas por normales (requiere prepase DepthNormals: mas caro, recomendado solo en PC).")]
            public bool useNormals = true;
            [Tooltip("Coseno minimo entre normales vecinas antes de entintar (menor = solo aristas mas vivas).")]
            [Range(0f, 1f)] public float normalThreshold = 0.55f;
            [Tooltip("Opacidad de las aristas por normales respecto de las siluetas.")]
            [Range(0f, 1f)] public float normalStrength = 0.65f;
            [Tooltip("Distancia (m) desde la que el contorno empieza a desvanecerse.")]
            public float fadeStart = 25f;
            [Tooltip("Distancia (m) a la que el contorno queda en fadeMin.")]
            public float fadeEnd = 90f;
            [Range(0f, 1f)] public float fadeMin = 0.25f;
            public RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingTransparents;
        }

        public Settings settings = new Settings();

        public const string ShaderName = "Hidden/SpellSimulator/Outline";
        public const string GlobalStrengthName = "_OutlineStrength";
        private static readonly int GlobalStrengthId = Shader.PropertyToID(GlobalStrengthName);
        private static bool globalInitialized;

        private Material material;
        private OutlinePass pass;

        /// <summary>Atajo para el interruptor global (0 apaga, 1 normal).</summary>
        public static void SetGlobalStrength(float value) => Shader.SetGlobalFloat(GlobalStrengthId, value);

        public override void Create()
        {
            // Los globales sin asignar valen 0: se enciende una vez por dominio para que "sin tocar nada" haya contorno.
            if (!globalInitialized)
            {
                globalInitialized = true;
                Shader.SetGlobalFloat(GlobalStrengthId, 1f);
            }
            if (settings.shader == null) settings.shader = Shader.Find(ShaderName);
            if (material != null) CoreUtils.Destroy(material);
            material = settings.shader != null ? CoreUtils.CreateEngineMaterial(settings.shader) : null;
            pass = new OutlinePass { renderPassEvent = settings.passEvent };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null || pass == null) return;
            var type = renderingData.cameraData.cameraType;
            if (type == CameraType.Preview || type == CameraType.Reflection) return;
            if (renderingData.cameraData.renderType == CameraRenderType.Overlay) return;

            float strength = Shader.GetGlobalFloat(GlobalStrengthId);
            var volume = VolumeManager.instance.stack != null ? VolumeManager.instance.stack.GetComponent<OutlineVolume>() : null;
            if (volume != null) strength *= volume.intensity.value;
            if (strength <= 0.001f) return;

            pass.Setup(material, settings, volume, strength);
            pass.ConfigureInput(settings.useNormals
                ? ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal
                : ScriptableRenderPassInput.Depth);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
        }

        private sealed class OutlinePass : ScriptableRenderPass
        {
            private static readonly int InkColorId = Shader.PropertyToID("_InkColor");
            private static readonly int ParamsId = Shader.PropertyToID("_OutlineParams");
            private static readonly int FadeId = Shader.PropertyToID("_OutlineFade");
            private static readonly int IntensityId = Shader.PropertyToID("_OutlineIntensity");
            private const string NormalsKeyword = "_OUTLINE_NORMALS";
            private const float ReferenceHeight = 1080f;

            private Material material;
            private Vector4 parameters;

            private sealed class PassData
            {
                public TextureHandle source;
                public Material material;
            }

            public OutlinePass()
            {
                profilingSampler = new ProfilingSampler("Outline");
                // Hace falta leer el color de la camara: fuerza la textura intermedia si no la hubiera.
                requiresIntermediateTexture = true;
            }

            public void Setup(Material mat, Settings s, OutlineVolume volume, float strength)
            {
                material = mat;
                Color ink = volume != null && volume.inkColor.overrideState ? volume.inkColor.value : s.inkColor;
                float thickness = s.thickness * (volume != null ? volume.thicknessScale.value : 1f);
                material.SetColor(InkColorId, ink);
                material.SetFloat(IntensityId, strength);
                parameters = new Vector4(thickness, s.depthThreshold, s.normalThreshold, s.normalStrength);
                material.SetVector(FadeId, new Vector4(s.fadeStart, Mathf.Max(s.fadeEnd, s.fadeStart + 0.01f), s.fadeMin, s.farThinning));
                CoreUtils.SetKeyword(material, NormalsKeyword, s.useNormals);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer || material == null) return;

                TextureHandle source = resources.activeColorTexture;
                var desc = renderGraph.GetTextureDesc(source);
                desc.name = "_OutlineColor";
                desc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(desc);

                // El grosor se expresa a 1080p: se escala con la altura real del target (render scale, ojos de Quest).
                var cameraData = frameData.Get<UniversalCameraData>();
                float scale = cameraData.cameraTargetDescriptor.height / ReferenceHeight;
                var p = parameters;
                p.x = Mathf.Max(1f, p.x * scale);
                material.SetVector(ParamsId, p);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Outline", out var data, profilingSampler))
                {
                    data.source = source;
                    data.material = material;
                    builder.UseTexture(source);
                    if (resources.cameraDepthTexture.IsValid()) builder.UseTexture(resources.cameraDepthTexture);
                    if (resources.cameraNormalsTexture.IsValid()) builder.UseTexture(resources.cameraNormalsTexture);
                    builder.SetRenderAttachment(destination, 0);
                    builder.SetRenderFunc((PassData d, RasterGraphContext ctx) => Execute(d, ctx));
                }
                resources.cameraColor = destination;
            }

            private static void Execute(PassData data, RasterGraphContext context)
            {
                Blitter.BlitTexture(context.cmd, data.source, new Vector4(1f, 1f, 0f, 0f), data.material, 0);
            }
        }
    }
}
