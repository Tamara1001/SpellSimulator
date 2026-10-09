using NUnit.Framework;
using SpellSimulator.Output;

namespace SpellSimulator.Tests
{
    /// <summary>Hechizo de piedra: radio, tiempo de formacion y masa.</summary>
    public class RockMathTests
    {
        [Test]
        public void Radius_ClampedToLimits()
        {
            Assert.AreEqual(RockMath.MinRadius, RockMath.Radius(0.001f, 1f), 1e-5f);
            Assert.AreEqual(RockMath.MaxRadius, RockMath.Radius(50f, 1f), 1e-5f);
        }

        [Test]
        public void Radius_InkPowerEnlarges()
        {
            Assert.AreEqual(0.45f, RockMath.Radius(0.5f, 1f), 1e-4f);
            Assert.Greater(RockMath.Radius(0.5f, 4f), RockMath.Radius(0.5f, 1f));
            Assert.Less(RockMath.Radius(0.5f, 0.25f), RockMath.Radius(0.5f, 1f));
        }

        [Test]
        public void Radius_ZeroInkDoesNotBreak()
        {
            Assert.AreEqual(RockMath.MinRadius, RockMath.Radius(0.5f, 0f), 1e-5f);
        }

        [Test]
        public void FormSeconds_MonotonicAndLimited()
        {
            Assert.AreEqual(RockMath.MinFormSeconds, RockMath.FormSeconds(0.01f), 1e-5f);
            Assert.AreEqual(RockMath.MaxFormSeconds, RockMath.FormSeconds(5f), 1e-5f);
            float prev = 0f;
            for (float r = 0.08f; r <= 1.2f; r += 0.1f)
            {
                float s = RockMath.FormSeconds(r);
                Assert.GreaterOrEqual(s, prev);
                prev = s;
            }
        }

        [Test]
        public void Mass_ClampedAndGrows()
        {
            Assert.AreEqual(RockMath.MinMass, RockMath.Mass(0.01f), 1e-5f);
            Assert.AreEqual(3.86f, RockMath.Mass(0.08f), 0.05f);
            Assert.AreEqual(RockMath.MaxMass, RockMath.Mass(1.2f), 1e-5f);
            Assert.Greater(RockMath.Mass(0.5f), RockMath.Mass(0.3f));
            Assert.AreEqual(600f, RockMath.Mass(0.5f), 1e-3f);
        }

        [Test]
        public void OverLimit_TotalAndPerOwner()
        {
            Assert.IsFalse(RockMath.OverLimit(RockMath.MaxRocks, RockMath.MaxRocksPerOwner, true));
            Assert.IsTrue(RockMath.OverLimit(RockMath.MaxRocks + 1, 1, true));
            Assert.IsTrue(RockMath.OverLimit(6, RockMath.MaxRocksPerOwner + 1, true));
            Assert.IsFalse(RockMath.OverLimit(6, RockMath.MaxRocksPerOwner + 1, false)); // en solitario no rige el tope por duenio
        }
    }
}
