using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Combat;
using SpellSimulator.Player;

namespace SpellSimulator.Tests
{
    /// <summary>Vida del jugador en Libre (paso V): regeneración, protección e invulnerabilidad de Health.</summary>
    public class VitalityRulesTests
    {
        [Test]
        public void Regen_WaitsAfterDamage_ThenHealsAtAFixedRate()
        {
            Assert.AreEqual(0f, VitalityRules.RegenAmount(VitalityRules.RegenDelay - 0.1f, 1f));
            Assert.AreEqual(VitalityRules.RegenPerSecond * 0.5f, VitalityRules.RegenAmount(VitalityRules.RegenDelay, 0.5f), 1e-5f);
            Assert.AreEqual(0f, VitalityRules.RegenAmount(100f, 0f));
        }

        [Test]
        public void Protected_OnlyWithTheSwitchAndOutsideADuel()
        {
            Assert.IsTrue(VitalityRules.Protected(true, false));
            Assert.IsFalse(VitalityRules.Protected(true, true), "en Duelo el interruptor no vale");
            Assert.IsFalse(VitalityRules.Protected(false, false));
        }

        [Test]
        public void WarnLow_BelowTheThreshold_WithCooldown()
        {
            Assert.IsTrue(VitalityRules.ShouldWarnLow(0.2f, 100f));
            Assert.IsFalse(VitalityRules.ShouldWarnLow(0.2f, 1f), "enfriamiento");
            Assert.IsFalse(VitalityRules.ShouldWarnLow(0.9f, 100f));
            Assert.IsFalse(VitalityRules.ShouldWarnLow(0f, 100f), "muerto: avisa Died, no esto");
        }

        [Test]
        public void Health_Invulnerable_IgnoresDamage_AndHealCapsAtMax()
        {
            var go = new GameObject("HealthTest");
            try
            {
                var h = go.AddComponent<Health>();
                h.SetMax(100f);
                h.Invulnerable = true;
                h.Damage(50f);
                Assert.AreEqual(100f, h.Current);
                h.Invulnerable = false;
                h.Damage(50f);
                Assert.AreEqual(50f, h.Current);
                h.Heal(80f);
                Assert.AreEqual(100f, h.Current);
                // Morir y reaparecer lo cubre el Play Smoke (la muerte muestra numeros flotantes, que piden Play).
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
