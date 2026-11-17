using NUnit.Framework;
using SpellSimulator.World;
using UnityEngine;

namespace SpellSimulator.Tests
{
    public class TutorialExerciseRulesTests
    {
        [Test]
        public void Upright_LienzoDeCanto_Cuenta()
        {
            Assert.IsTrue(TutorialExerciseRules.Upright(Vector3.forward));
            Assert.IsTrue(TutorialExerciseRules.Upright(new Vector3(0.9f, 0.2f, 0f).normalized));
        }

        [Test]
        public void Upright_LienzoAcostado_NoCuenta()
        {
            Assert.IsFalse(TutorialExerciseRules.Upright(Vector3.up));
            Assert.IsFalse(TutorialExerciseRules.Upright(Vector3.down));
        }

        [Test]
        public void HardEnough_RoceNo_TiroSi()
        {
            Assert.IsFalse(TutorialExerciseRules.HardEnough(0.5f));
            Assert.IsTrue(TutorialExerciseRules.HardEnough(5f));
        }
    }
}
