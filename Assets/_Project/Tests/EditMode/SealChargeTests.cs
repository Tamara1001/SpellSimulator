using NUnit.Framework;
using SpellSimulator.Casting;

namespace SpellSimulator.Tests
{
    public class SealChargeTests
    {
        [Test]
        public void Duration_AtLimits()
        {
            Assert.AreEqual(SealCharge.MinSeconds, SealCharge.Duration(SealCharge.SmallRadius), 1e-5f);
            Assert.AreEqual(SealCharge.MaxSeconds, SealCharge.Duration(SealCharge.LargeRadius), 1e-5f);
        }

        [Test]
        public void Duration_MidPoint_IsAverage()
        {
            float mid = (SealCharge.SmallRadius + SealCharge.LargeRadius) * 0.5f;
            Assert.AreEqual((SealCharge.MinSeconds + SealCharge.MaxSeconds) * 0.5f, SealCharge.Duration(mid), 1e-5f);
        }

        [Test]
        public void Duration_IsClamped()
        {
            Assert.AreEqual(SealCharge.MinSeconds, SealCharge.Duration(0f), 1e-5f);
            Assert.AreEqual(SealCharge.MinSeconds, SealCharge.Duration(-1f), 1e-5f);
            Assert.AreEqual(SealCharge.MaxSeconds, SealCharge.Duration(10f), 1e-5f);
        }

        [Test]
        public void Duration_IsMonotonic()
        {
            float prev = SealCharge.Duration(0f);
            for (float r = 0.01f; r <= 0.7f; r += 0.01f)
            {
                float d = SealCharge.Duration(r);
                Assert.GreaterOrEqual(d, prev - 1e-6f);
                prev = d;
            }
        }
    }
}
