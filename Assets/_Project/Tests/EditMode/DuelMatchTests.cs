using NUnit.Framework;
using SpellSimulator.Combat;

namespace SpellSimulator.Tests
{
    public class DuelMatchTests
    {
        private static DuelMatch ToRound(DuelRules r)
        {
            var m = new DuelMatch(r);
            m.Tick(r.countdownSeconds + 0.01f, 1f, 1f);
            Assert.AreEqual(DuelPhase.Round, m.Phase);
            return m;
        }

        [Test]
        public void ArrancaConCuentaAtrasYPasaALaRonda()
        {
            var m = new DuelMatch(DuelRules.Default);
            Assert.AreEqual(DuelPhase.Countdown, m.Phase);
            m.Tick(2f, 1f, 1f);
            Assert.AreEqual(DuelPhase.Countdown, m.Phase);
            m.Tick(1.5f, 1f, 1f);
            Assert.AreEqual(DuelPhase.Round, m.Phase);
        }

        [Test]
        public void QuienLlegaACeroPierdeLaRonda()
        {
            var m = ToRound(DuelRules.Default);
            m.Tick(0.1f, 0f, 0.5f);
            Assert.AreEqual(DuelPhase.RoundEnd, m.Phase);
            Assert.AreEqual(1, m.RoundWinner);
            Assert.AreEqual(1, m.Wins[1]);
        }

        [Test]
        public void MuertesSimultaneasEmpatanLaRonda()
        {
            var m = ToRound(DuelRules.Default);
            m.Tick(0.1f, 0f, 0f);
            Assert.AreEqual(-1, m.RoundWinner);
            Assert.AreEqual(0, m.Wins[0] + m.Wins[1]);
        }

        [Test]
        public void AlAcabarseElTiempoGanaElQueTieneMasVida()
        {
            var m = ToRound(DuelRules.Default);
            m.Tick(m.Rules.roundSeconds + 1f, 0.7f, 0.2f);
            Assert.AreEqual(0, m.RoundWinner);
        }

        [Test]
        public void AlMejorDeTresTerminaAlSegundoPunto()
        {
            var r = DuelRules.Default;
            var m = ToRound(r);
            m.Tick(0.1f, 1f, 0f); // gana 0
            m.Tick(r.roundEndSeconds + 0.1f, 1f, 1f);
            Assert.AreEqual(DuelPhase.Countdown, m.Phase);
            Assert.AreEqual(2, m.RoundNumber);
            m.Tick(r.countdownSeconds + 0.1f, 1f, 1f);
            m.Tick(0.1f, 1f, 0f); // gana 0 otra vez
            m.Tick(r.roundEndSeconds + 0.1f, 1f, 1f);
            Assert.IsTrue(m.Over);
            Assert.AreEqual(0, m.Winner);
        }

        [Test]
        public void ConUnoAUnoSeJuegaLaTercera()
        {
            var r = DuelRules.Default;
            var m = ToRound(r);
            m.Tick(0.1f, 0f, 1f); m.Tick(r.roundEndSeconds + 0.1f, 1f, 1f);
            m.Tick(r.countdownSeconds + 0.1f, 1f, 1f);
            m.Tick(0.1f, 1f, 0f); m.Tick(r.roundEndSeconds + 0.1f, 1f, 1f);
            Assert.IsFalse(m.Over);
            Assert.AreEqual(3, m.RoundNumber);
        }

        [Test]
        public void AbandonarDaLaVictoriaAlOtro()
        {
            var m = ToRound(DuelRules.Default);
            m.Forfeit(1);
            Assert.IsTrue(m.Over);
            Assert.AreEqual(0, m.Winner);
        }

        [Test]
        public void UnClienteCopiaElEstadoDelAnfitrion()
        {
            var m = new DuelMatch(DuelRules.Default);
            DuelPhase seen = DuelPhase.Countdown;
            m.PhaseChanged += p => seen = p;
            m.Apply(DuelPhase.RoundEnd, 2, 1.5f, 1, 0, 0, -1);
            Assert.AreEqual(DuelPhase.RoundEnd, seen);
            Assert.AreEqual(1, m.Wins[0]);
            Assert.AreEqual(2, m.RoundNumber);
        }
    }
}
