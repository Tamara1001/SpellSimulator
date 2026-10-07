using NUnit.Framework;
using SpellSimulator.Casting;
using SpellSimulator.Parsing;

namespace SpellSimulator.Tests
{
    public class SealActivationTests
    {
        private static RingInfo Ring(bool found, bool complete) => new RingInfo { Found = found, Complete = complete };

        [Test]
        public void NoRing_IsDrawing_AndDoesNotCast()
        {
            var phase = SealActivation.Evaluate(Ring(false, false), SealPhase.Drawing, true, out bool cast);
            Assert.AreEqual(SealPhase.Drawing, phase);
            Assert.IsFalse(cast);
        }

        [Test]
        public void OpenRing_IsPrepared()
        {
            var phase = SealActivation.Evaluate(Ring(true, false), SealPhase.Drawing, true, out bool cast);
            Assert.AreEqual(SealPhase.Prepared, phase);
            Assert.IsFalse(cast);
        }

        [Test]
        public void ClosingTheRing_Casts()
        {
            SealActivation.Evaluate(Ring(true, true), SealPhase.Prepared, true, out bool cast);
            Assert.IsTrue(cast);
            Assert.AreEqual(SealPhase.Spent, SealActivation.AfterCast(launched: true));
        }

        [Test]
        public void SpentSeal_DoesNotRecast_OnNewStrokes()
        {
            var phase = SealActivation.Evaluate(Ring(true, true), SealPhase.Spent, true, out bool cast);
            Assert.AreEqual(SealPhase.Spent, phase);
            Assert.IsFalse(cast);
        }

        [Test]
        public void InvalidClosedSeal_RetriesOnNextStroke()
        {
            // Anillo cerrado primero, sin sigilo: el cast falla y queda Prepared; el siguiente trazo reintenta.
            var afterFail = SealActivation.AfterCast(launched: false);
            SealActivation.Evaluate(Ring(true, true), afterFail, true, out bool cast);
            Assert.IsTrue(cast);
        }

        [Test]
        public void ErasingCannotCast_ButReopensSpentSeal()
        {
            SealActivation.Evaluate(Ring(true, true), SealPhase.Prepared, castAllowed: false, out bool cast);
            Assert.IsFalse(cast);

            var reopened = SealActivation.Evaluate(Ring(true, false), SealPhase.Spent, false, out _);
            Assert.AreEqual(SealPhase.Prepared, reopened, "Borrar parte del anillo de un sello gastado lo rearma.");
        }
    }
}
