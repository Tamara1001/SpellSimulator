using NUnit.Framework;
using SpellSimulator.World;

namespace SpellSimulator.Tests
{
    public class SupplyMathTests
    {
        [Test]
        public void ShouldSpawn_WhenBelowLimitAndCooldownElapsed()
            => Assert.IsTrue(SupplyMath.ShouldSpawn(0, 1, 60f, 60f));

        [Test]
        public void ShouldSpawn_RejectsBeforeCooldown()
            => Assert.IsFalse(SupplyMath.ShouldSpawn(0, 1, 59f, 60f));

        [Test]
        public void ShouldSpawn_RejectsWhenFull()
        {
            Assert.IsFalse(SupplyMath.ShouldSpawn(1, 1, 999f, 60f));
            Assert.IsFalse(SupplyMath.ShouldSpawn(3, 2, 999f, 60f));
        }

        [Test]
        public void ShouldSpawn_PartiallyFilledPointStillRefills()
            => Assert.IsTrue(SupplyMath.ShouldSpawn(1, 3, 10f, 10f));

        [Test]
        public void SecondsRemaining_CountsDownAndClampsAtZero()
        {
            Assert.AreEqual(45f, SupplyMath.SecondsRemaining(0, 1, 15f, 60f), 1e-4f);
            Assert.AreEqual(0f, SupplyMath.SecondsRemaining(0, 1, 90f, 60f), 1e-4f);
        }

        [Test]
        public void SecondsRemaining_IsMinusOneWhenFull()
            => Assert.AreEqual(-1f, SupplyMath.SecondsRemaining(2, 2, 5f, 60f), 1e-4f);
    }
}
