using NUnit.Framework;
using SpellSimulator.Learning;

namespace SpellSimulator.Tests
{
    public class GrimoireOptionsTests
    {
        [Test]
        public void StepVolume_SubeYBajaDeADiezPorCiento()
        {
            Assert.AreEqual(0.6f, GrimoireOptions.StepVolume(0.5f, 1), 1e-4f);
            Assert.AreEqual(0.4f, GrimoireOptions.StepVolume(0.5f, -1), 1e-4f);
        }

        [Test]
        public void StepVolume_SeQuedaEnCeroYUno()
        {
            Assert.AreEqual(1f, GrimoireOptions.StepVolume(1f, 1), 1e-4f);
            Assert.AreEqual(0f, GrimoireOptions.StepVolume(0f, -1), 1e-4f);
            Assert.AreEqual(0f, GrimoireOptions.StepVolume(0.04f, -1), 1e-4f);
        }

        [Test]
        public void StepVolume_RedondeaADecimas()
        {
            Assert.AreEqual(0.8f, GrimoireOptions.StepVolume(0.72f, 1), 1e-4f);
        }

        [Test]
        public void StepSensitivity_RespetaElRango()
        {
            Assert.AreEqual(1.1f, GrimoireOptions.StepSensitivity(1f, 1), 1e-4f);
            Assert.AreEqual(0.3f, GrimoireOptions.StepSensitivity(0.3f, -1), 1e-4f);
            Assert.AreEqual(2.5f, GrimoireOptions.StepSensitivity(2.5f, 1), 1e-4f);
        }

        [Test]
        public void Formatos()
        {
            Assert.AreEqual("80 %", GrimoireOptions.Percent(0.8f));
            Assert.AreEqual("0 %", GrimoireOptions.Percent(-1f));
            Assert.AreEqual("1,2x", GrimoireOptions.Multiplier(1.2f));
            Assert.AreEqual("0,3x", GrimoireOptions.Multiplier(0.3f));
        }
    }
}
