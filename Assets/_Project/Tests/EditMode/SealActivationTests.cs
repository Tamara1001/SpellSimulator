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

        [Test]
        public void ClosedRing_WithTrigger_ArmsInsteadOfCasting()
        {
            var phase = SealActivation.Evaluate(Ring(true, true), SealPhase.Prepared, true, deferred: true, out bool cast);
            Assert.AreEqual(SealPhase.Armed, phase);
            Assert.IsFalse(cast);
        }

        [Test]
        public void ArmedSeal_StaysArmed_AndOnlyArmedFires()
        {
            var phase = SealActivation.Evaluate(Ring(true, true), SealPhase.Armed, true, deferred: true, out bool cast);
            Assert.AreEqual(SealPhase.Armed, phase);
            Assert.IsFalse(cast);
            Assert.IsTrue(SealActivation.ShouldFire(SealPhase.Armed));
            Assert.IsFalse(SealActivation.ShouldFire(SealPhase.Prepared));
            Assert.IsFalse(SealActivation.ShouldFire(SealPhase.Spent));
        }

        [Test]
        public void SpentSeal_WithTrigger_DoesNotRearm_ButOpeningTheRingPrepares()
        {
            Assert.AreEqual(SealPhase.Spent, SealActivation.Evaluate(Ring(true, true), SealPhase.Spent, true, true, out _));
            Assert.AreEqual(SealPhase.Prepared, SealActivation.Evaluate(Ring(true, false), SealPhase.Armed, false, true, out _));
        }

        [Test]
        public void Charging_StaysCharging_AndNeverCastsAgain()
        {
            var phase = SealActivation.Evaluate(Ring(true, true), SealPhase.Charging, true, out bool cast);
            Assert.AreEqual(SealPhase.Charging, phase);
            Assert.IsFalse(cast, "Un lanzamiento en carga no se duplica con otro trazo.");
        }

        [Test]
        public void Charging_OpenOrMissingRing_Cancels()
        {
            Assert.AreEqual(SealPhase.Prepared, SealActivation.Evaluate(Ring(true, false), SealPhase.Charging, true, out bool c1));
            Assert.IsFalse(c1);
            Assert.AreEqual(SealPhase.Drawing, SealActivation.Evaluate(Ring(false, false), SealPhase.Charging, false, out bool c2));
            Assert.IsFalse(c2);
        }

        [Test]
        public void Charging_Phase_CannotFireTrigger_AndRearmKeepsIt()
        {
            Assert.IsFalse(SealActivation.ShouldFire(SealPhase.Charging));
            Assert.AreEqual(SealPhase.Charging, SealActivation.Rearm(SealPhase.Charging));
        }

        [Test]
        public void CancelledCharge_NewStroke_RestartsCast_ErasingDoesNot()
        {
            // El caster cancela la carga (fase Prepared) y reevalua con la edicion.
            SealActivation.Evaluate(Ring(true, true), SealPhase.Prepared, true, out bool restart);
            Assert.IsTrue(restart);
            SealActivation.Evaluate(Ring(true, true), SealPhase.Prepared, false, out bool erased);
            Assert.IsFalse(erased);
        }

        [Test]
        public void Charging_WithTrigger_Arms()
        {
            Assert.AreEqual(SealPhase.Armed, SealActivation.Evaluate(Ring(true, true), SealPhase.Charging, true, true, out _));
        }

        [Test]
        public void Rearm_OnlyTurnsASpentSealBackToArmed()
        {
            Assert.AreEqual(SealPhase.Armed, SealActivation.Rearm(SealPhase.Spent));
            Assert.AreEqual(SealPhase.Prepared, SealActivation.Rearm(SealPhase.Prepared));
            Assert.AreEqual(SealPhase.Drawing, SealActivation.Rearm(SealPhase.Drawing));
        }
    }
}
