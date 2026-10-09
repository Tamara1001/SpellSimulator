using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Reactions;

namespace SpellSimulator.Tests
{
    /// <summary>Cuentas del agua fisica (WaterMath): charcos, fusion, pendiente, flotacion, zonas inundadas, recipientes y tinta.</summary>
    public class WaterMathTests
    {
        private const float Eps = 1e-4f;

        // ---------------------------------------------------------------- charcos

        [Test]
        public void PuddleRadius_ZeroOrNegativeLiters_IsZero()
        {
            Assert.AreEqual(0f, WaterMath.PuddleRadius(0f));
            Assert.AreEqual(0f, WaterMath.PuddleRadius(-3f));
        }

        [Test]
        public void PuddleRadius_TinyVolume_UsesMinimum()
        {
            Assert.AreEqual(WaterMath.MinPuddleRadius, WaterMath.PuddleRadius(0.001f), Eps);
        }

        [Test]
        public void PuddleRadius_GrowsWithVolume_AsSquareRoot()
        {
            float r4 = WaterMath.PuddleRadius(4f);
            float r16 = WaterMath.PuddleRadius(16f);
            Assert.Greater(r16, r4);
            Assert.AreEqual(2f, r16 / r4, 1e-3f);
            // 12 L en lamina de 1 cm: area 1.2 m2 -> radio ~0.618 m.
            Assert.AreEqual(Mathf.Sqrt(1.2f / Mathf.PI), WaterMath.PuddleRadius(12f), 1e-3f);
        }

        [Test]
        public void PuddleLife_NormalInk_WithinRange_PowerfulLastsLonger()
        {
            float normal = WaterMath.PuddleLifeSeconds(1f);
            float powerful = WaterMath.PuddleLifeSeconds(2.5f);
            Assert.That(normal, Is.InRange(20f, 40f));
            Assert.That(powerful, Is.InRange(20f, 40f));
            Assert.Greater(powerful, normal);
        }

        [Test]
        public void PuddleLife_ExtremeInk_IsClamped()
        {
            Assert.AreEqual(WaterMath.MinPuddleLife, WaterMath.PuddleLifeSeconds(0f), Eps);
            Assert.AreEqual(WaterMath.MinPuddleLife, WaterMath.PuddleLifeSeconds(-5f), Eps);
            Assert.AreEqual(WaterMath.MaxPuddleLife, WaterMath.PuddleLifeSeconds(100f), Eps);
        }

        [Test]
        public void Evaporation_FullPuddleDriesInItsLife()
        {
            foreach (float ink in new[] { 1f, 2.5f })
            {
                float seconds = WaterMath.PuddleMaxLiters / WaterMath.EvaporationRate(ink);
                Assert.AreEqual(WaterMath.PuddleLifeSeconds(ink), seconds, 1e-3f);
            }
            Assert.Less(WaterMath.EvaporationRate(2.5f), WaterMath.EvaporationRate(1f));
        }

        [Test]
        public void DropVolume_ScalesWithInk_NeverNegative()
        {
            Assert.AreEqual(WaterMath.DropLiters, WaterMath.DropVolume(1f), Eps);
            Assert.AreEqual(WaterMath.DropLiters * 2.5f, WaterMath.DropVolume(2.5f), Eps);
            Assert.AreEqual(0f, WaterMath.DropVolume(-1f), Eps);
        }

        [Test]
        public void AddLiters_ClampsToZeroAndMax()
        {
            Assert.AreEqual(WaterMath.PuddleMaxLiters, WaterMath.AddLiters(11f, 5f), Eps);
            Assert.AreEqual(0f, WaterMath.AddLiters(1f, -5f), Eps);
            Assert.AreEqual(3f, WaterMath.AddLiters(1f, 2f), Eps);
        }

        [Test]
        public void ShouldMerge_OverlappingYes_FarNo_EmptyNo()
        {
            Assert.IsTrue(WaterMath.ShouldMerge(Vector2.zero, 0.5f, new Vector2(0.6f, 0f), 0.5f));
            Assert.IsFalse(WaterMath.ShouldMerge(Vector2.zero, 0.5f, new Vector2(0.9f, 0f), 0.5f));
            Assert.IsFalse(WaterMath.ShouldMerge(Vector2.zero, 0f, Vector2.zero, 0.5f));
        }

        [Test]
        public void MergedCenter_IsVolumeWeighted()
        {
            Vector3 c = WaterMath.MergedCenter(Vector3.zero, 3f, new Vector3(4f, 0f, 0f), 1f);
            Assert.AreEqual(1f, c.x, Eps);
            Vector3 empty = WaterMath.MergedCenter(Vector3.zero, 0f, new Vector3(2f, 0f, 0f), 0f);
            Assert.AreEqual(1f, empty.x, Eps);
        }

        // ---------------------------------------------------------------- pendiente

        [Test]
        public void Gradient_FlatGround_IsZero_AndNoSlide()
        {
            Vector2 g = WaterMath.Gradient(1f, 1f, 1f, 1f, 0.2f);
            Assert.AreEqual(Vector2.zero, g);
            Assert.AreEqual(Vector2.zero, WaterMath.SlideVelocity(g));
        }

        [Test]
        public void Slide_GoesDownhill()
        {
            // Mas alto al este: el charco va al oeste (x negativo).
            Vector2 g = WaterMath.Gradient(0.1f, 0f, 0f, 0f, 0.5f);
            Vector2 v = WaterMath.SlideVelocity(g);
            Assert.Less(v.x, 0f);
            Assert.AreEqual(0f, v.y, Eps);
        }

        [Test]
        public void Slide_TinySlope_DoesNotMove_SteepIsCapped()
        {
            Assert.AreEqual(Vector2.zero, WaterMath.SlideVelocity(new Vector2(0.01f, 0.01f)));
            Vector2 steep = WaterMath.SlideVelocity(WaterMath.Gradient(5f, 0f, 0f, 0f, 0.1f));
            Assert.AreEqual(WaterMath.MaxSlideSpeed, steep.magnitude, Eps);
        }

        [Test]
        public void Gradient_WallStep_IsClampedToMaxSlope_ZeroSpacingSafe()
        {
            Vector2 g = WaterMath.Gradient(10f, 0f, 0f, -10f, 0.1f);
            Assert.AreEqual(WaterMath.MaxSlope, g.x, Eps);
            Assert.AreEqual(WaterMath.MaxSlope, g.y, Eps);
            Assert.AreEqual(Vector2.zero, WaterMath.Gradient(1f, 0f, 0f, 0f, 0f));
        }

        // ---------------------------------------------------------------- flotacion y zonas

        [Test]
        public void Submerged_Fraction_Limits()
        {
            Assert.AreEqual(0f, WaterMath.SubmergedFraction(1f, 0.5f, 0.5f), Eps);
            Assert.AreEqual(1f, WaterMath.SubmergedFraction(0f, 0.5f, 2f), Eps);
            Assert.AreEqual(0.5f, WaterMath.SubmergedFraction(0f, 0.4f, 0.2f), Eps);
            // Altura cero no divide por cero.
            Assert.AreEqual(1f, WaterMath.SubmergedFraction(0f, 0f, 0.1f), Eps);
        }

        [Test]
        public void Buoyancy_OnlyLightBodiesFloat()
        {
            Assert.Greater(WaterMath.BuoyantAcceleration(1f, 1f, 1.2f, -9.81f), 9.81f);
            Assert.AreEqual(0f, WaterMath.BuoyantAcceleration(WaterMath.MaxFloatMass, 1f, 1.2f, -9.81f), Eps);
            Assert.AreEqual(0f, WaterMath.BuoyantAcceleration(1f, 0f, 1.2f, -9.81f), Eps);
            Assert.AreEqual(9.81f * 0.5f, WaterMath.BuoyantAcceleration(1f, 0.5f, 1f, -9.81f), 1e-3f);
        }

        [Test]
        public void FloodSpell_NeedsBigStrongAndDown()
        {
            Assert.IsTrue(WaterMath.IsFloodSpell(0.4f, 0.8f, 0.9f));
            Assert.IsFalse(WaterMath.IsFloodSpell(0.2f, 0.8f, 0.9f));   // anillo chico
            Assert.IsFalse(WaterMath.IsFloodSpell(0.4f, 0.2f, 0.9f));   // poca fuerza
            Assert.IsFalse(WaterMath.IsFloodSpell(0.4f, 0.8f, 0f));     // horizontal
            Assert.IsTrue(WaterMath.IsFloodSpell(WaterMath.FloodMinRingRadius, WaterMath.FloodMinForce, WaterMath.FloodMinDownDot));
        }

        [Test]
        public void FloodSeconds_Within12And20_InkLonger()
        {
            Assert.That(WaterMath.FloodSeconds(0f, 1f), Is.InRange(12f, 20f));
            Assert.That(WaterMath.FloodSeconds(1f, 1f), Is.InRange(12f, 20f));
            Assert.AreEqual(20f, WaterMath.FloodSeconds(1f, 100f), Eps);
            Assert.AreEqual(12f, WaterMath.FloodSeconds(0f, 0f), Eps);
            Assert.Greater(WaterMath.FloodSeconds(0.5f, 2.5f), WaterMath.FloodSeconds(0.5f, 1f));
        }

        [Test]
        public void FloodLevel_FillsHoldsDrains()
        {
            const float life = 15f;
            Assert.AreEqual(0f, WaterMath.FloodLevel(-1f, life), Eps);
            Assert.AreEqual(0f, WaterMath.FloodLevel(0f, life), Eps);
            Assert.Less(WaterMath.FloodLevel(1f, life), 1f);
            Assert.AreEqual(1f, WaterMath.FloodLevel(life * 0.5f, life), Eps);
            Assert.Less(WaterMath.FloodLevel(life - 1f, life), 1f);
            Assert.AreEqual(0f, WaterMath.FloodLevel(life, life), Eps);
            Assert.AreEqual(0f, WaterMath.FloodLevel(5f, 0f), Eps);
        }

        // ---------------------------------------------------------------- recipientes y tinta

        [Test]
        public void ContainerMass_OneKgPerLiter_Capped()
        {
            Assert.AreEqual(3f, WaterMath.ContainerExtraMass(3f, 8f), Eps);
            Assert.AreEqual(8f, WaterMath.ContainerExtraMass(20f, 8f), Eps);
            Assert.AreEqual(0f, WaterMath.ContainerExtraMass(-1f, 8f), Eps);
            Assert.AreEqual(0f, WaterMath.ContainerExtraMass(5f, -2f), Eps);
        }

        [Test]
        public void Spill_OnlyPastAngle()
        {
            Assert.IsFalse(WaterMath.ShouldSpill(0f));
            Assert.IsFalse(WaterMath.ShouldSpill(WaterMath.SpillAngle));
            Assert.IsTrue(WaterMath.ShouldSpill(120f));
            Assert.IsTrue(WaterMath.ShouldSpill(180f));
        }

        [Test]
        public void FlowMultiplier_NormalIsOne_PowerfulMore_Clamped()
        {
            Assert.AreEqual(1f, WaterMath.FlowMultiplier(1f), Eps);
            Assert.AreEqual(2.5f, WaterMath.FlowMultiplier(2.5f), Eps);
            Assert.AreEqual(0.5f, WaterMath.FlowMultiplier(0f), Eps);
            Assert.AreEqual(3f, WaterMath.FlowMultiplier(10f), Eps);
        }
    }
}
