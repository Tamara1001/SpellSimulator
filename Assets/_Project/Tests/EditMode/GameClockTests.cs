using NUnit.Framework;
using SpellSimulator.Core;

namespace SpellSimulator.Tests
{
    public class GameClockTests
    {
        [Test]
        public void Scope_UsaElRelojFalsoYRestauraAlSalir()
        {
            var fake = new FakeGameClock { Time = 42f };
            using (new ClockScope(fake)) Assert.AreEqual(42f, GameClock.Time);
            Assert.AreNotSame(fake, GameClock.Current);
        }

        [Test]
        public void SetNull_VuelveAlRelojDeUnity()
        {
            GameClock.Set(new FakeGameClock());
            GameClock.Set(null);
            Assert.AreEqual(UnityEngine.Time.time, GameClock.Time);
        }

        [Test]
        public void Cooldown_ListoSoloDespuesDelRearme()
        {
            var fake = new FakeGameClock { Time = 10f };
            using (new ClockScope(fake))
            {
                float rearmAt = GameClock.Time + 3f;
                Assert.IsFalse(CooldownMath.IsReady(GameClock.Time, rearmAt));
                fake.Advance(2.9f);
                Assert.IsFalse(CooldownMath.IsReady(GameClock.Time, rearmAt));
                fake.Advance(0.2f);
                Assert.IsTrue(CooldownMath.IsReady(GameClock.Time, rearmAt));
            }
        }

        [Test]
        public void Recent_DejaDeSerRecientePasadoElLimite()
        {
            var fake = new FakeGameClock { Time = 5f };
            using (new ClockScope(fake))
            {
                float last = GameClock.Time;
                fake.Advance(0.4f);
                Assert.IsTrue(CooldownMath.IsRecent(GameClock.Time, last, 0.5f));
                fake.Advance(0.2f);
                Assert.IsFalse(CooldownMath.IsRecent(GameClock.Time, last, 0.5f));
            }
        }

        [Test]
        public void Pin_SeSueltaALosSegundosDeFlotado()
        {
            var fake = new FakeGameClock { Time = 100f };
            using (new ClockScope(fake))
            {
                float pinnedAt = GameClock.Time;
                fake.Advance(59f);
                Assert.IsFalse(PinMath.ShouldRelease(GameClock.Time, pinnedAt, 60f));
                fake.Advance(1f);
                Assert.IsTrue(PinMath.ShouldRelease(GameClock.Time, pinnedAt, 60f));
            }
        }

        [Test]
        public void GameRandom_SetNullVuelveAUnityYPermiteFijarValor()
        {
            GameRandom.Set(new FixedRandom());
            Assert.AreEqual(0.25f, GameRandom.Current.Value);
            GameRandom.Set(null);
            Assert.AreNotEqual(typeof(FixedRandom), GameRandom.Current.GetType());
        }

        private sealed class FixedRandom : IGameRandom
        {
            public float Value => 0.25f;
            public float Range(float min, float max) => min;
            public UnityEngine.Vector3 InsideUnitSphere => UnityEngine.Vector3.zero;
        }
    }
}
