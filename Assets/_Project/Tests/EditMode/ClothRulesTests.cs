using NUnit.Framework;
using SpellSimulator.World.Map;

namespace SpellSimulator.Tests
{
    /// <summary>Tela pesada del piso 1 de la torre-tutorial: se suelta con viento o un golpe, nunca por el jugador.</summary>
    public class ClothRulesTests
    {
        [Test]
        public void Quiet_StaysHanging() => Assert.IsFalse(ClothRules.ShouldOpen(0.1f, 0.3f, 0f, false));

        [Test]
        public void PushedAway_Opens() => Assert.IsTrue(ClothRules.ShouldOpen(ClothRules.OpenDistance, 0f, 0f, false));

        [Test]
        public void FastGust_Opens() => Assert.IsTrue(ClothRules.ShouldOpen(0.05f, ClothRules.OpenSpeed, 0f, false));

        [Test]
        public void ThrownCrate_Opens_ButAPaperDoesNot()
        {
            Assert.IsTrue(ClothRules.ShouldOpen(0f, 0f, 10f, false));
            Assert.IsFalse(ClothRules.ShouldOpen(0f, 0f, 0.9f, false));
        }

        [Test]
        public void Counterweight_ReleasesOnlyWhenLifted()
        {
            Assert.IsFalse(ClothRules.ShouldRelease(ClothRules.LiftDistance - 0.1f));
            Assert.IsTrue(ClothRules.ShouldRelease(ClothRules.LiftDistance));
        }

        [Test]
        public void Player_NeverOpensIt() => Assert.IsFalse(ClothRules.ShouldOpen(2f, 5f, 50f, true));
    }
}
