using NUnit.Framework;
using SpellSimulator.Output;

namespace SpellSimulator.Tests
{
    /// <summary>Pruebas EditMode de float (suelto, se mece) y levitation (firme, altura fija).</summary>
    public class LevitationMathTests
    {
        [Test]
        public void HoverHeight_FloatSubeMasQueLevitation()
        {
            Assert.Greater(LevitationMath.HoverHeight(true, 0.3f, 1f), LevitationMath.HoverHeight(false, 0.3f, 1f));
            Assert.Greater(LevitationMath.HoverHeight(false, 0.3f, 2f), LevitationMath.HoverHeight(false, 0.3f, 0.5f));
        }

        [Test]
        public void GravityCancel_LivianoCasIngravidoYPesadoMenos()
        {
            Assert.AreEqual(1f, LevitationMath.GravityCancel(false, 5f), 1e-4f);
            Assert.Greater(LevitationMath.GravityCancel(true, 5f), 0.9f);
            Assert.Less(LevitationMath.GravityCancel(true, 400f), 0.2f);
            Assert.Greater(LevitationMath.GravityCancel(false, 100f), LevitationMath.GravityCancel(true, 100f));
        }

        [Test]
        public void VerticalAcceleration_EnReposoSoloSostieneElPeso()
        {
            float a = LevitationMath.VerticalAcceleration(false, 0.5f, 0f, 0.5f, 9.81f, 1f, 0f);
            Assert.AreEqual(9.81f, a, 1e-3f);
        }

        [Test]
        public void VerticalAcceleration_MasFuerteSiEstaPorDebajoDeLaAltura()
        {
            float low = LevitationMath.VerticalAcceleration(false, 0.1f, 0f, 0.5f, 9.81f, 1f, 0f);
            float at = LevitationMath.VerticalAcceleration(false, 0.5f, 0f, 0.5f, 9.81f, 1f, 0f);
            Assert.Greater(low, at);
        }

        [Test]
        public void VerticalAcceleration_LevitationEsMasFirmeQueFloat()
        {
            float lev = LevitationMath.VerticalAcceleration(false, 0.1f, 0f, 0.5f, 0f, 1f, 0f);
            float flo = LevitationMath.VerticalAcceleration(true, 0.1f, 0f, 0.5f, 0f, 1f, 0f);
            Assert.Greater(lev, flo);
        }

        [Test]
        public void LateralAcceleration_VuelveAlEjeYLevitationMasFuerte()
        {
            Assert.Less(LevitationMath.LateralAcceleration(false, 1f, 0f), 0f);
            Assert.Less(LevitationMath.LateralAcceleration(false, 1f, 0f), LevitationMath.LateralAcceleration(true, 1f, 0f));
        }
    }
}
