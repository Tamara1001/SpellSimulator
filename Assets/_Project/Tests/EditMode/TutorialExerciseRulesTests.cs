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
        [Test]
        public void PaperAtSpot_Acostado_EnElLugar_Cuenta()
        {
            Vector3 spot = new Vector3(1f, 2f, 3f);
            Assert.IsTrue(TutorialExerciseRules.PaperAtSpot(spot + new Vector3(0.2f, 0.1f, 0f), Vector3.up, spot, false));
            Assert.IsFalse(TutorialExerciseRules.PaperAtSpot(spot, Vector3.forward, spot, false), "de canto no es acostado");
        }

        [Test]
        public void PaperAtSpot_Parado_PideLaPosturaYElLugar()
        {
            Vector3 spot = Vector3.zero;
            Assert.IsTrue(TutorialExerciseRules.PaperAtSpot(spot, Vector3.right, spot, true));
            Assert.IsFalse(TutorialExerciseRules.PaperAtSpot(spot, Vector3.up, spot, true), "acostado no es parado");
            Assert.IsFalse(TutorialExerciseRules.PaperAtSpot(new Vector3(2f, 0f, 0f), Vector3.right, spot, true), "lejos del fantasma");
        }
    }
}
