using NUnit.Framework;
using SpellSimulator.World;

namespace SpellSimulator.Tests
{
    public class RuneJoinMathTests
    {
        private const float Threshold = 3f;

        [Test]
        public void CanJoin_BothArmedAndSoftContact()
            => Assert.IsTrue(RuneJoinMath.CanJoin(true, true, false, false, false, false, 0.5f, Threshold));

        [Test]
        public void CanJoin_RejectsIfNotArmed()
        {
            Assert.IsFalse(RuneJoinMath.CanJoin(false, true, false, false, false, false, 0.5f, Threshold));
            Assert.IsFalse(RuneJoinMath.CanJoin(true, false, false, false, false, false, 0.5f, Threshold));
        }

        [Test]
        public void CanJoin_RejectsExhaustedRunes()
        {
            Assert.IsFalse(RuneJoinMath.CanJoin(true, true, true, false, false, false, 0.5f, Threshold));
            Assert.IsFalse(RuneJoinMath.CanJoin(true, true, false, true, false, false, 0.5f, Threshold));
        }

        [Test]
        public void CanJoin_RejectsRecentlyFiredByImpact()
            => Assert.IsFalse(RuneJoinMath.CanJoin(true, true, false, false, true, false, 0.5f, Threshold));

        [Test]
        public void CanJoin_RejectsHardImpact()
            => Assert.IsFalse(RuneJoinMath.CanJoin(true, true, false, false, false, false, Threshold, Threshold));

        [Test]
        public void Timer_AccumulatesAndResets()
        {
            float held = RuneJoinMath.Advance(0f, 0.2f, true);
            held = RuneJoinMath.Advance(held, 0.2f, true);
            Assert.IsTrue(RuneJoinMath.Completed(held, 0.4f - 1e-4f));
            Assert.AreEqual(0f, RuneJoinMath.Advance(held, 0.02f, false), 1e-6f);
        }

        [Test]
        public void IsBig_LargerRadiusWins_TieGoesToLowerId()
        {
            Assert.IsTrue(RuneJoinMath.IsBig(0.3f, 0.2f, 9, 1));
            Assert.IsFalse(RuneJoinMath.IsBig(0.2f, 0.3f, 1, 9));
            Assert.IsTrue(RuneJoinMath.IsBig(0.2f, 0.2f, 1, 9));
            Assert.IsFalse(RuneJoinMath.IsBig(0.2f, 0.2f, 9, 1));
        }

        [Test]
        public void IsLeader_ExactlyOneOfThePair()
            => Assert.AreNotEqual(RuneJoinMath.IsLeader(3, 7), RuneJoinMath.IsLeader(7, 3));
    }
}
