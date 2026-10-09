using NUnit.Framework;
using SpellSimulator.Learning;

namespace SpellSimulator.Tests
{
    public class PauseMenuMathTests
    {
        [Test]
        public void IsArmed_FalseWhenDisarmed() =>
            Assert.IsFalse(PauseMenuMath.IsArmed(PauseMenuMath.Disarmed, 10f));

        [Test]
        public void IsArmed_TrueWithinWindow() =>
            Assert.IsTrue(PauseMenuMath.IsArmed(10f, 12.9f));

        [Test]
        public void IsArmed_FalseAfterExpiry() =>
            Assert.IsFalse(PauseMenuMath.IsArmed(10f, 13.1f));

        [Test]
        public void IsArmed_FalseIfClockWentBack() =>
            Assert.IsFalse(PauseMenuMath.IsArmed(10f, 5f));

        [Test]
        public void ResetLabel_ChangesWhenArmed()
        {
            Assert.AreEqual("Reiniciar el mundo", PauseMenuMath.ResetLabel(false));
            Assert.AreEqual("¿Seguro? Clic de nuevo", PauseMenuMath.ResetLabel(true));
        }

        [Test]
        public void Cycle_WrapsBothWays()
        {
            Assert.AreEqual(0, PauseMenuMath.Cycle(2, 3, 1));
            Assert.AreEqual(2, PauseMenuMath.Cycle(0, 3, -1));
            Assert.AreEqual(1, PauseMenuMath.Cycle(0, 3, 5));
            Assert.AreEqual(0, PauseMenuMath.Cycle(0, 0, 1));
        }
    }
}
