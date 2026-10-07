using NUnit.Framework;
using SpellSimulator.Combat;
using SpellSimulator.Compiler;

namespace SpellSimulator.Tests
{
    /// <summary>Reglas de daño (DamageModel) con los valores de referencia del Duelo (GAME_DESIGN 5).</summary>
    public class DamageTests
    {
        private static readonly DamageTable.Values Table = DamageTable.Values.Default;

        [Test]
        public void DirectFireHit_IsInTheReferenceRange()
        {
            // Un chorro de fuego de ~1.5 s a intensidad media-alta: impacto directo 8-30.
            float damage = DamageModel.SpellDamage(Table, ElementType.Fire, 0.8f, 1.5f);
            Assert.That(damage, Is.InRange(8f, 30f));
        }

        [Test]
        public void WaterAndWind_BarelyHurt()
        {
            Assert.Less(DamageModel.SpellDamage(Table, ElementType.Water, 1f, 2f), 4f);
            Assert.Less(DamageModel.SpellDamage(Table, ElementType.Wind, 1f, 2f), 4f);
            Assert.Greater(DamageModel.SpellDamage(Table, ElementType.Earth, 1f, 2f), DamageModel.SpellDamage(Table, ElementType.Fire, 1f, 2f));
        }

        [Test]
        public void NoExposure_NoDamage()
        {
            Assert.AreEqual(0f, DamageModel.SpellDamage(Table, ElementType.Fire, 0f, 1f));
            Assert.AreEqual(0f, DamageModel.SpellDamage(Table, ElementType.None, 1f, 1f));
        }

        [Test]
        public void Burning_HurtsFourPerSecond()
        {
            Assert.AreEqual(4f, DamageModel.BurnDamage(Table, true, 1f), 1e-5f);
            Assert.AreEqual(0f, DamageModel.BurnDamage(Table, false, 1f));
        }

        [Test]
        public void Impact_HasThresholdAndCap()
        {
            Assert.AreEqual(0f, DamageModel.ImpactDamage(Table, 2f, 5f), "Un golpe suave no daña.");
            Assert.Greater(DamageModel.ImpactDamage(Table, 6f, 2f), 0f);
            Assert.AreEqual(25f, DamageModel.ImpactDamage(Table, 30f, 50f), 1e-5f, "Aplastamiento: hasta 25.");
        }
    }
}
