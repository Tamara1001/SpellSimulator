using NUnit.Framework;
using SpellSimulator.World;

namespace SpellSimulator.Tests
{
    public class RuneProfilesTests
    {
        [Test]
        public void Stone_IsFragileAndNotResistant()
        {
            var p = RuneProfiles.Get(RuneKind.Stone);
            Assert.AreEqual(2, p.Durability);
            Assert.AreEqual(5f, p.CooldownSeconds, 1e-5f);
            Assert.IsFalse(p.Waterproof);
            Assert.IsFalse(p.FireProof);
            Assert.AreEqual(0f, p.Glow, 1e-5f);
        }

        [Test]
        public void Metal_ResistsWaterAndFire()
        {
            var p = RuneProfiles.Get(RuneKind.Metal);
            Assert.AreEqual(5, p.Durability);
            Assert.AreEqual(4f, p.CooldownSeconds, 1e-5f);
            Assert.IsTrue(p.Waterproof);
            Assert.IsTrue(p.FireProof);
            Assert.AreEqual(0f, p.Glow, 1e-5f);
        }

        [Test]
        public void Gold_LastsLongest_CoolsFastest_AndGlows()
        {
            var p = RuneProfiles.Get(RuneKind.Gold);
            Assert.AreEqual(10, p.Durability);
            Assert.AreEqual(1.5f, p.CooldownSeconds, 1e-5f);
            Assert.IsTrue(p.Waterproof && p.FireProof);
            Assert.Greater(p.Glow, 0f);
            Assert.Less(p.CooldownSeconds, RuneProfiles.Get(RuneKind.Metal).CooldownSeconds);
            Assert.Greater(p.Durability, RuneProfiles.Get(RuneKind.Metal).Durability);
        }

        [Test]
        public void Wear_AccumulatesUntilExhausted()
        {
            int durability = RuneProfiles.Get(RuneKind.Stone).Durability;
            int wear = 0;
            Assert.IsFalse(RuneProfiles.IsExhausted(wear, durability));
            wear = RuneProfiles.AddWear(wear, durability);
            Assert.AreEqual(0.5f, RuneProfiles.WearFraction(wear, durability), 1e-5f);
            Assert.IsFalse(RuneProfiles.IsExhausted(wear, durability));
            wear = RuneProfiles.AddWear(wear, durability);
            Assert.IsTrue(RuneProfiles.IsExhausted(wear, durability));
            Assert.AreEqual(1f, RuneProfiles.WearFraction(wear, durability), 1e-5f);
        }

        [Test]
        public void Wear_NeverExceedsDurability()
        {
            Assert.AreEqual(2, RuneProfiles.AddWear(2, 2));
            Assert.AreEqual(1f, RuneProfiles.WearFraction(5, 2), 1e-5f);
        }
    }
}
