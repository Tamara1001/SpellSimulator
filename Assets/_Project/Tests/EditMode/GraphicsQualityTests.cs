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
    }
}
