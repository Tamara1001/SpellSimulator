using NUnit.Framework;
using SpellSimulator.World;

namespace SpellSimulator.Tests
{
    public class RecycleMathTests
    {
        [Test]
        public void Cost_ExhaustedIsOne_FreshIsTwo()
        {
            foreach (RuneKind kind in new[] { RuneKind.Stone, RuneKind.Metal, RuneKind.Gold })
            {
                Assert.AreEqual(1, RecycleMath.Cost(kind, true));
                Assert.AreEqual(2, RecycleMath.Cost(kind, false));
            }
        }

        [Test]
        public void CanRecycle_OneExhaustedIsEnough()
            => Assert.IsTrue(RecycleMath.CanRecycle(1, 0, RuneKind.Stone));

        [Test]
        public void CanRecycle_SingleFreshIsRejected()
            => Assert.IsFalse(RecycleMath.CanRecycle(0, 1, RuneKind.Metal));

        [Test]
        public void CanRecycle_TwoFreshAreAccepted()
            => Assert.IsTrue(RecycleMath.CanRecycle(0, 2, RuneKind.Gold));

        [Test]
        public void CanRecycle_NothingIsRejected()
            => Assert.IsFalse(RecycleMath.CanRecycle(0, 0, RuneKind.Stone));

        [Test]
        public void Outputs_IsClosedEconomy()
        {
            Assert.AreEqual(3, RecycleMath.Outputs(RuneKind.Stone, 3, 0));
            Assert.AreEqual(2, RecycleMath.Outputs(RuneKind.Stone, 0, 5));
            Assert.AreEqual(3, RecycleMath.Outputs(RuneKind.Stone, 1, 4));
            Assert.AreEqual(0, RecycleMath.Outputs(RuneKind.Stone, 0, 1));
        }

        [Test]
        public void FreshToConsume_AlwaysEven()
        {
            Assert.AreEqual(0, RecycleMath.FreshToConsume(RuneKind.Stone, 1));
            Assert.AreEqual(2, RecycleMath.FreshToConsume(RuneKind.Stone, 2));
            Assert.AreEqual(2, RecycleMath.FreshToConsume(RuneKind.Stone, 3));
            Assert.AreEqual(4, RecycleMath.FreshToConsume(RuneKind.Stone, 5));
        }

        [Test]
        public void Timing_DeliversAfterRecycleSeconds()
        {
            float ready = RecycleMath.ReadyAt(10f);
            Assert.AreEqual(18f, ready, 1e-4f);
            Assert.IsFalse(RecycleMath.IsReady(17.9f, ready));
            Assert.IsTrue(RecycleMath.IsReady(18f, ready));
        }
    }
}
