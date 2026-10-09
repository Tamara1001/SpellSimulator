using NUnit.Framework;
using SpellSimulator.Output;

namespace SpellSimulator.Tests
{
    /// <summary>Hechizo de cristal: radio de la esfera y duracion.</summary>
    public class CrystalMathTests
    {
        [Test]
        public void Radius_IsClampedToLimits()
        {
            Assert.AreEqual(CrystalMath.MinRadius, CrystalMath.Radius(0.01f, 1f), 1e-5f);
            Assert.AreEqual(CrystalMath.MaxRadius, CrystalMath.Radius(10f, 4f), 1e-5f);
            Assert.AreEqual(CrystalMath.MinRadius, CrystalMath.Radius(0.5f, -3f), 1e-5f);
        }

        [Test]
        public void Radius_GrowsWithInkPower()
        {
            Assert.AreEqual(2f, CrystalMath.Radius(0.5f, 1f), 1e-5f);
            Assert.AreEqual(4f, CrystalMath.Radius(0.5f, 4f), 1e-5f);
            Assert.Greater(CrystalMath.Radius(0.5f, 2f), CrystalMath.Radius(0.5f, 1f));
        }

        [Test]
        public void Seconds_IsMonotonicAndBounded()
        {
            Assert.AreEqual(CrystalMath.MinSeconds, CrystalMath.Seconds(0.1f), 1e-4f);
            Assert.AreEqual(CrystalMath.MinSeconds, CrystalMath.Seconds(CrystalMath.MinRadius), 1e-4f);
            Assert.AreEqual(CrystalMath.MaxSeconds, CrystalMath.Seconds(CrystalMath.MaxRadius), 1e-4f);
            Assert.AreEqual(CrystalMath.MaxSeconds, CrystalMath.Seconds(50f), 1e-4f);
            float prev = 0f;
            for (float r = 0.6f; r <= 8f; r += 0.5f)
            {
                float s = CrystalMath.Seconds(r);
                Assert.GreaterOrEqual(s, prev);
                prev = s;
            }
        }
    }
}
