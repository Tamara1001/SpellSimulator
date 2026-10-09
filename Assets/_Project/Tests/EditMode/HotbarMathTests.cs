using NUnit.Framework;
using SpellSimulator.Player;

namespace SpellSimulator.Tests
{
    public class HotbarMathTests
    {
        [Test]
        public void SlotCount_DependeDelCinturon()
        {
            Assert.AreEqual(3, HotbarMath.SlotCount(false));
            Assert.AreEqual(5, HotbarMath.SlotCount(true));
        }

        [Test]
        public void FirstFreeSlot_RespetaElLimite()
        {
            var free = new[] { false, false, false, true, true };
            Assert.AreEqual(-1, HotbarMath.FirstFreeSlot(free, 3));
            Assert.AreEqual(3, HotbarMath.FirstFreeSlot(free, 5));
            Assert.AreEqual(-1, HotbarMath.FirstFreeSlot(null, 5));
        }

        [Test]
        public void ClampKey_IgnoraLoQueNoEntra()
        {
            Assert.AreEqual(2, HotbarMath.ClampKey(2, 3));
            Assert.AreEqual(-1, HotbarMath.ClampKey(3, 3));
            Assert.AreEqual(4, HotbarMath.ClampKey(4, 5));
            Assert.AreEqual(-1, HotbarMath.ClampKey(-1, 5));
        }
    }
}
