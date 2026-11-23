using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Rendering;

namespace SpellSimulator.Tests
{
    public class GraphicsQualityTests
    {
        [Test]
        public void Default_PorPlataforma()
        {
            Assert.AreEqual(GraphicsQualityLevel.Medium, GraphicsQuality.DefaultFor(RuntimePlatform.WebGLPlayer));
            Assert.AreEqual(GraphicsQualityLevel.Medium, GraphicsQuality.DefaultFor(RuntimePlatform.Android));
            Assert.AreEqual(GraphicsQualityLevel.High, GraphicsQuality.DefaultFor(RuntimePlatform.WindowsPlayer));
        }

        [Test]
        public void Next_CicloBajaMediaAlta()
        {
            Assert.AreEqual(GraphicsQualityLevel.Medium, GraphicsQuality.Next(GraphicsQualityLevel.Low));
            Assert.AreEqual(GraphicsQualityLevel.High, GraphicsQuality.Next(GraphicsQualityLevel.Medium));
            Assert.AreEqual(GraphicsQualityLevel.Low, GraphicsQuality.Next(GraphicsQualityLevel.High));
        }

        [Test]
        public void Presets_CrecenConElNivel()
        {
            var lo = GraphicsQuality.Get(GraphicsQualityLevel.Low);
            var mid = GraphicsQuality.Get(GraphicsQualityLevel.Medium);
            var hi = GraphicsQuality.Get(GraphicsQualityLevel.High);
            Assert.AreEqual(0.7f, lo.RenderScale, 1e-4f);
            Assert.AreEqual(0.85f, mid.RenderScale, 1e-4f);
            Assert.AreEqual(1f, hi.RenderScale, 1e-4f);
            Assert.AreEqual(15f, lo.ShadowDistance);
            Assert.AreEqual(60f, hi.ShadowDistance);
            Assert.AreEqual(1, lo.ShadowCascades);
            Assert.AreEqual(4, hi.MsaaSamples);
            Assert.IsFalse(lo.Outline);
            Assert.IsTrue(mid.Outline);
            Assert.Less(lo.ParticleScale, mid.ParticleScale);
            Assert.AreEqual(1f, hi.ParticleScale);
        }

        [Test]
        public void Sanitize_ValorInvalidoUsaElFallback()
        {
            Assert.AreEqual(GraphicsQualityLevel.High, GraphicsQuality.Sanitize(-1, GraphicsQualityLevel.High));
            Assert.AreEqual(GraphicsQualityLevel.High, GraphicsQuality.Sanitize(7, GraphicsQualityLevel.High));
            Assert.AreEqual(GraphicsQualityLevel.Low, GraphicsQuality.Sanitize(0, GraphicsQualityLevel.High));
        }

        [Test]
        public void Label_EnEspanol()
        {
            Assert.AreEqual("Baja", GraphicsQuality.Label(GraphicsQualityLevel.Low));
            Assert.AreEqual("Alta", GraphicsQuality.Label(GraphicsQualityLevel.High));
        }

        [Test]
        public void ParticleBudget_CalculoPuro_DevuelveValoresEsperadosYEnRango()
        {
            // Perfiles de PC (isVr = false)
            Assert.AreEqual(0.5f, ParticleBudget.CalculateScale(GraphicsQualityLevel.Low, false), 1e-4f);
            Assert.AreEqual(0.75f, ParticleBudget.CalculateScale(GraphicsQualityLevel.Medium, false), 1e-4f);
            Assert.AreEqual(1.0f, ParticleBudget.CalculateScale(GraphicsQualityLevel.High, false), 1e-4f);

            // VR / Quest (isVr = true) siempre devuelve VrScale (0.5f)
            Assert.AreEqual(0.5f, ParticleBudget.CalculateScale(GraphicsQualityLevel.Low, true), 1e-4f);
            Assert.AreEqual(0.5f, ParticleBudget.CalculateScale(GraphicsQualityLevel.Medium, true), 1e-4f);
            Assert.AreEqual(0.5f, ParticleBudget.CalculateScale(GraphicsQualityLevel.High, true), 1e-4f);

            // Rango 0..1 en todos los casos
            foreach (GraphicsQualityLevel level in System.Enum.GetValues(typeof(GraphicsQualityLevel)))
            {
                float pcScale = ParticleBudget.CalculateScale(level, false);
                float vrScale = ParticleBudget.CalculateScale(level, true);
                Assert.GreaterOrEqual(pcScale, 0f);
                Assert.LessOrEqual(pcScale, 1f);
                Assert.GreaterOrEqual(vrScale, 0f);
                Assert.LessOrEqual(vrScale, 1f);
            }
        }

        [Test]
        public void ParticleBudget_TopeGlobal_DevuelveValoresEsperados()
        {
            Assert.AreEqual(2500, ParticleBudget.CalculateMaxAlive(GraphicsQualityLevel.Low, false));
            Assert.AreEqual(4500, ParticleBudget.CalculateMaxAlive(GraphicsQualityLevel.Medium, false));
            Assert.AreEqual(8000, ParticleBudget.CalculateMaxAlive(GraphicsQualityLevel.High, false));
            Assert.AreEqual(2000, ParticleBudget.CalculateMaxAlive(GraphicsQualityLevel.Low, true));
            Assert.AreEqual(2000, ParticleBudget.CalculateMaxAlive(GraphicsQualityLevel.Medium, true));
            Assert.AreEqual(2000, ParticleBudget.CalculateMaxAlive(GraphicsQualityLevel.High, true));
        }

        [Test]
        public void PostProcessPolicy_VrDesactivaEfectosDePantallaCompletaYConservaBloomYColor()
        {
            Assert.IsFalse(PostProcessPolicy.IsActive(PostProcessEffect.Vignette, true));
            Assert.IsFalse(PostProcessPolicy.IsActive(PostProcessEffect.MotionBlur, true));
            Assert.IsFalse(PostProcessPolicy.IsActive(PostProcessEffect.DepthOfField, true));
            Assert.IsFalse(PostProcessPolicy.IsActive(PostProcessEffect.FilmGrain, true));
            Assert.IsFalse(PostProcessPolicy.IsActive(PostProcessEffect.LensDistortion, true));
            Assert.IsFalse(PostProcessPolicy.IsActive(PostProcessEffect.ChromaticAberration, true));
            Assert.IsTrue(PostProcessPolicy.IsActive(PostProcessEffect.Bloom, true));
            Assert.IsTrue(PostProcessPolicy.IsActive(PostProcessEffect.Tonemapping, true));
            Assert.IsTrue(PostProcessPolicy.IsActive(PostProcessEffect.ColorAdjustments, true));
            foreach (PostProcessEffect effect in System.Enum.GetValues(typeof(PostProcessEffect)))
                Assert.IsTrue(PostProcessPolicy.IsActive(effect, false), effect.ToString());
        }
    }
}
