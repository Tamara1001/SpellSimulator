using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Compiler;
using SpellSimulator.Core;

namespace SpellSimulator.Tests
{
    /// <summary>Leyes del plan (animation-spec.md de la referencia, R-05 a R-21).</summary>
    public class PlanTests
    {
        /// <summary>Signo a radio 0.8 en el angulo dado. twist: 0 = hacia adentro, 180 = hacia afuera, +90 = horario, -90 = antihorario.</summary>
        private static SignReading Sign(string manifestation, float bearingDeg, float twistDeg = 0f, float length = 1f, float trust = 0.9f)
        {
            return new SignReading
            {
                Id = manifestation,
                Manifestation = manifestation,
                At = GeometryUtils.VectorFromAngleDeg(bearingDeg) * 0.8f,
                Length = length,
                Facing = GeometryUtils.VectorFromAngleDeg(bearingDeg + 180f + twistDeg),
                FacingTrust = trust
            };
        }

        private static SpellPlan Plan(params SignReading[] signs) =>
            PlanResolver.Resolve(new List<SignReading>(signs), "fire", "fire", 0.9f);

        [Test]
        public void NoSigns_IsBurstOnly()
        {
            var plan = Plan();
            Assert.AreEqual(PlanPrimitive.Burst, plan.Primitives);
        }

        [Test]
        public void OpposedInwardColumns_MakeVerticalJet()
        {
            var plan = Plan(Sign("column", 0f), Sign("column", 180f));
            Assert.That(plan.Aim.x, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(plan.Aim.y, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(plan.Aim.z, Is.EqualTo(1.6f).Within(1e-4f));
            Assert.IsTrue(plan.Has(PlanPrimitive.Jet));
        }

        [Test]
        public void LongerSign_TiltsTheWayItPoints()
        {
            // R-05: el chorro se inclina hacia donde apunta el signo largo (no hacia donde esta).
            var plan = Plan(Sign("column", 180f, length: 2f), Sign("column", 0f, length: 1f));
            Assert.Greater(plan.Aim.x, 0f, "El signo largo esta a la izquierda y apunta a la derecha.");
            Assert.Greater(plan.Aim.z, 0f);
        }

        [Test]
        public void OutwardColumns_MakeFan_NotJet()
        {
            var plan = Plan(Sign("column", 0f, 180f), Sign("column", 180f, 180f));
            Assert.Greater(plan.Dispersion, 0f);
            Assert.AreEqual(0f, plan.Aim.z);
            Assert.IsTrue(plan.Has(PlanPrimitive.Fan));
            Assert.IsFalse(plan.Has(PlanPrimitive.Jet));
        }

        [Test]
        public void Pinwheel_MakesVortex()
        {
            var plan = Plan(Sign("column", 0f, -90f), Sign("column", 90f, -90f), Sign("column", 180f, -90f), Sign("column", 270f, -90f));
            Assert.Greater(plan.Circulation, 0f, "Tangencial antihorario => circulacion positiva.");
            Assert.That(plan.Aim.magnitude, Is.LessThan(1e-3f));
            Assert.IsTrue(plan.Has(PlanPrimitive.Vortex));
        }

        [Test]
        public void UntrustedFacing_AddsPowerNotDirection()
        {
            var plan = Plan(Sign("column", 0f, trust: 0.1f));
            Assert.AreEqual(1f, plan.Budget);
            Assert.AreEqual(Vector3.zero, plan.Aim);
            Assert.Contains("facing-untrusted", plan.Notes);
        }

        [Test]
        public void OpposedLevitation_HoldsAnOrb()
        {
            var plan = Plan(Sign("levitation", 0f), Sign("levitation", 180f));
            Assert.IsNotNull(plan.Hold);
            Assert.Greater(plan.Hold.Grip, 0f);
            Assert.That(plan.Hold.At.z, Is.EqualTo(0.9f).Within(1e-4f));
            Assert.IsTrue(plan.Has(PlanPrimitive.Hold));
        }

        [Test]
        public void InvertedLevitation_DoesNotHold()
        {
            var plan = Plan(Sign("levitation", 0f, 180f), Sign("levitation", 180f, 180f));
            Assert.IsNull(plan.Hold);
            Assert.Contains("levitation-inverted", plan.Notes);
        }

        [Test]
        public void PullOnly_ManipulatesWithoutBurst()
        {
            var plan = Plan(Sign("pull", 0f), Sign("pull", 180f));
            Assert.IsNotNull(plan.Intake);
            Assert.Greater(plan.Intake.Draw, 0f);
            Assert.IsFalse(plan.Has(PlanPrimitive.Burst));
            Assert.Contains("intake-only", plan.Notes);
        }

        [Test]
        public void Convergence_NarrowsTheLens()
        {
            Assert.Greater(Plan(Sign("convergence", 90f)).Focus, 1f);
        }

        [Test]
        public void SwirlingPullOnClashJet_SpinsUpIntoVortex()
        {
            // R-21: una succion helicoidal que alimenta un chorro de choque es un solo vortice.
            var plan = Plan(Sign("column", 0f), Sign("column", 180f),
                            Sign("pull", 90f, 60f), Sign("pull", 270f, 60f));
            Assert.Contains("spun-column", plan.Notes);
            Assert.AreEqual(0f, plan.Aim.z);
            Assert.IsNull(plan.Intake);
            Assert.IsTrue(plan.Has(PlanPrimitive.Vortex));
        }

        [Test]
        public void DispersionSign_IsALeak()
        {
            var plan = Plan(Sign("dispersion", 0f, 180f));
            Assert.IsTrue(plan.Leak);
        }
    }
}
