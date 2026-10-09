using NUnit.Framework;
using SpellSimulator.Core;

namespace SpellSimulator.Tests
{
    public class InkCycleHintTests
    {
        [Test]
        public void CanCycle_NeedsBeltAndTwoKinds()
        {
            var stock = new InkCartridgeStock();
            Assert.IsFalse(stock.CanCycle, "Sin cinturon ni segunda clase.");
            stock.SetBelt(true);
            Assert.IsFalse(stock.CanCycle, "Con cinturon pero una sola clase.");
            Assert.IsTrue(stock.TryAdd(InkKind.Powerful));
            Assert.IsTrue(stock.CanCycle);
            stock.SetBelt(false);
            Assert.IsFalse(stock.CanCycle);
        }
    }
}
