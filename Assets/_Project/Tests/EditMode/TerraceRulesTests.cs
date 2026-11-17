using NUnit.Framework;
using UnityEngine;
using SpellSimulator.World.Map;

namespace SpellSimulator.Tests
{
    public class TerraceRulesTests
    {
        private static Vector3 At(float r, float angle, float y)
        {
            float a = angle * Mathf.Deg2Rad;
            return new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a));
        }

        [Test]
        public void TerraceHeightMatchesTutorialRules() =>
            Assert.AreEqual(TutorialRules.FloorHeight * 3f, TerraceRules.TerraceY, 0.001f);

        [Test]
        public void CrouchOnlyCountsUnderTheBeamAndLow()
        {
            Vector3 under = At(5f, TerraceRules.BeamAngle, TerraceRules.TerraceY + 1f);
            Assert.IsTrue(TerraceRules.Crouched(under, true));
            Assert.IsFalse(TerraceRules.Crouched(under, false));
            Assert.IsFalse(TerraceRules.Crouched(At(5f, TerraceRules.BeamAngle + 40f, TerraceRules.TerraceY + 1f), true));
        }

        [Test]
        public void BeamAlsoCountsOnTheFarSideOfTheCentre()
        {
            Assert.IsTrue(TerraceRules.Crouched(At(2f, TerraceRules.BeamAngle + 180f, TerraceRules.TerraceY + 1f), true));
        }

        [Test]
        public void JumpNeedsToBeAirborneOverTheWall()
        {
            Vector3 over = At(5f, TerraceRules.JumpWallAngle, TerraceRules.TerraceY + 1.4f);
            Assert.IsTrue(TerraceRules.Jumped(over, false));
            Assert.IsFalse(TerraceRules.Jumped(over, true));
            Assert.IsFalse(TerraceRules.Jumped(At(5f, TerraceRules.JumpWallAngle - 40f, TerraceRules.TerraceY + 1.4f), false));
        }

        [Test]
        public void OtherFloorsDoNotCount()
        {
            Assert.IsFalse(TerraceRules.Crouched(At(5f, TerraceRules.BeamAngle, 4.5f), true));
        }
    }
}
