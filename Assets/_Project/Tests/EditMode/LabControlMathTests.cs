using NUnit.Framework;
using SpellSimulator.Lab;

namespace SpellSimulator.Tests
{
    public class LabControlMathTests
    {
        [Test]
        public void DeltaToValue_EscalaYRecorta()
        {
            Assert.AreEqual(5f, LabControlMath.DeltaToValue(0f, 0.5f, 0f, 10f, 1f), 1e-5f);
            Assert.AreEqual(10f, LabControlMath.DeltaToValue(8f, 5f, 0f, 10f, 1f));
            Assert.AreEqual(0f, LabControlMath.DeltaToValue(2f, -5f, 0f, 10f, 1f));
            Assert.AreEqual(3f, LabControlMath.DeltaToValue(3f, 1f, 0f, 10f, 0f)); // sin recorrido: no cambia
        }

        [Test]
        public void UnwrapAngle_NoSaltaAlCruzar360()
        {
            float a = 350f;
            a = LabControlMath.UnwrapAngle(a, 10f);
            Assert.AreEqual(370f, a, 1e-3f);
            a = LabControlMath.UnwrapAngle(a, 350f);
            Assert.AreEqual(350f, a, 1e-3f);
            a = LabControlMath.UnwrapAngle(10f, 350f);
            Assert.AreEqual(-10f, a, 1e-3f);
        }

        [Test]
        public void SnapToDetent_PegaALaMuescaMasCercana()
        {
            Assert.AreEqual(0f, LabControlMath.SnapToDetent(0.1f, 5));
            Assert.AreEqual(0.25f, LabControlMath.SnapToDetent(0.3f, 5), 1e-5f);
            Assert.AreEqual(1f, LabControlMath.SnapToDetent(2f, 5));
            Assert.AreEqual(0f, LabControlMath.SnapToDetent(0.7f, 1));
            Assert.AreEqual(3, LabControlMath.DetentIndex(0.76f, 5));
        }

        [Test]
        public void CrossedDetent_SoloAlCambiarDeMuesca()
        {
            Assert.IsFalse(LabControlMath.CrossedDetent(0.05f, 0.1f, 5));
            Assert.IsTrue(LabControlMath.CrossedDetent(0.1f, 0.2f, 5));
            Assert.IsTrue(LabControlMath.CrossedDetent(0.9f, 0.4f, 5));
            Assert.IsFalse(LabControlMath.CrossedDetent(0f, 1f, 1));
        }
    }
}
