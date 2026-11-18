using NUnit.Framework;
using SpellSimulator.Output;

namespace SpellSimulator.Tests
{
    /// <summary>Pruebas EditMode de los extras visuales: estabilidad (temblor, chispas, parpadeo) y radio de la region.</summary>
    public class SpellVisualExtrasMathTests
    {
        [Test]
        public void Jitter_TrazoEstableNoTiembla()
        {
            Assert.AreEqual(0f, SpellVisualExtrasMath.Jitter(1f));
            Assert.AreEqual(0f, SpellVisualExtrasMath.Jitter(SpellVisualExtrasMath.StableThreshold));
        }

        [Test]
        public void Jitter_CreceAlBajarLaEstabilidad()
        {
            float medio = SpellVisualExtrasMath.Jitter(0.5f);
            float bajo = SpellVisualExtrasMath.Jitter(0.2f);
            Assert.Greater(medio, 0f);
            Assert.Greater(bajo, medio);
            Assert.AreEqual(1f, SpellVisualExtrasMath.Jitter(0f), 1e-4f);
            Assert.AreEqual(1f, SpellVisualExtrasMath.Jitter(-1f), 1e-4f);
        }

        [Test]
        public void Flicker_SoloBajaConInestabilidad()
        {
            Assert.AreEqual(1f, SpellVisualExtrasMath.Flicker(0f, 1f));
            Assert.AreEqual(0.4f, SpellVisualExtrasMath.Flicker(1f, 1f), 1e-4f);
            Assert.AreEqual(1f, SpellVisualExtrasMath.Flicker(1f, 0f));
        }

        [Test]
        public void CrackleCount_ApagadoSiEsEstableYCreceConElJitter()
        {
            Assert.AreEqual(0, SpellVisualExtrasMath.CrackleCount(0f));
            Assert.Greater(SpellVisualExtrasMath.CrackleCount(0.8f), SpellVisualExtrasMath.CrackleCount(0.1f));
        }

        [Test]
        public void RegionRadius_EscalaConElSelloYTieneMinimo()
        {
            Assert.AreEqual(SpellVisualExtrasMath.RegionRadiusFactor * 0.5f, SpellVisualExtrasMath.RegionRadius(0.5f), 1e-4f);
            Assert.Greater(SpellVisualExtrasMath.RegionRadius(0f), 0f);
        }
    }
}
