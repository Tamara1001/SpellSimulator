using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Compiler;
using SpellSimulator.Net;

namespace SpellSimulator.Tests
{
    public class SpellIRCodecTests
    {
        private static SpellIR Sample()
        {
            var plan = new SpellPlan
            {
                Mode = PlanMode.Manipulate,
                Aim = new Vector3(0.2f, -0.1f, 0.9f),
                Dispersion = 0.3f, Circulation = -0.4f, Budget = 2.5f, Focus = 1.7f, Leak = true, Quality = 0.8f,
                Primitives = PlanPrimitive.Jet | PlanPrimitive.Hold | PlanPrimitive.Intake,
                Hold = new HoldSpec { At = new Vector3(0, 0.5f, 1.2f), Grip = 0.6f, Spin = 0.1f, Budget = 1f },
                Intake = new IntakeSpec { Budget = 0.7f, Draw = 0.5f, Swirl = 0.2f, Lateral = new Vector2(0.1f, -0.2f) },
            };
            plan.Signs.Add(new SignReading { Id = "column", Manifestation = "column", At = new Vector2(0.3f, -0.6f), Length = 0.5f, Facing = new Vector2(0, 1), FacingTrust = 0.9f, FacingFromPose = true });
            plan.Signs.Add(new SignReading { Id = "pull", Manifestation = "pull", At = new Vector2(-0.4f, 0.1f), Length = 0.2f, Facing = new Vector2(1, 0), FacingTrust = 0.3f, FacingFromPose = false });
            return new SpellIR
            {
                valid = true, active = true, prepared = false, status = "Hechizo activo", warnings = new string[0],
                element = ElementType.Wind, sigilId = "wind-directs-air", elementConfidence = 0.93f,
                primarySizeNorm = 0.4f, sizeRatio = 1.2f, strength = 3.5f, effectScale = 1.1f,
                primaryManifestation = Manifestations.Column,
                manifestations = new[] { new ManifestationStrength { Id = Manifestations.Column, Strength = 2f, Influence = 0.5f, Point = new Vector2(0.1f, 0.2f), Radius = 0.3f, Rigidity = 0.4f } },
                force = 0.7f, spread = 0.2f, focus = 0.5f, range = 0.6f, gravity = 0.3f, duration = 4f, stability = 0.8f, quality = 0.9f, neatness = 0.7f,
                direction = new Vector3(0.1f, 0.2f, 0.97f), xTiltDeg = 3f, yTiltDeg = -2f, directionCoherence = 0.85f,
                activatedAt = 12.5f, plan = plan, sealRadius = 0.31f, inkPower = 2.5f, waterproof = true, analysisBoost = 1.5f, analysisPivot = new Vector2(0.5f, 0.5f),
            };
        }

        [Test]
        public void RoundTrip_KeepsEverythingTheOutputUses()
        {
            SpellIR a = Sample();
            Assert.IsTrue(SpellIRCodec.TryDecode(SpellIRCodec.Encode(a), 99f, out SpellIR b));
            Assert.IsTrue(b.valid && b.active && !b.prepared && b.waterproof);
            Assert.AreEqual(a.element, b.element);
            Assert.AreEqual(a.sigilId, b.sigilId);
            Assert.AreEqual(a.strength, b.strength);
            Assert.AreEqual(a.inkPower, b.inkPower);
            Assert.AreEqual(a.sealRadius, b.sealRadius);
            Assert.AreEqual(a.direction, b.direction);
            Assert.AreEqual(a.primaryManifestation, b.primaryManifestation);
            Assert.AreEqual(1, b.manifestations.Length);
            Assert.AreEqual(a.manifestations[0].Radius, b.manifestations[0].Radius);
            Assert.AreEqual(99f, b.activatedAt, "la hora de activacion es la local");
            Assert.AreEqual(a.plan.Primitives, b.plan.Primitives);
            Assert.AreEqual(a.plan.Aim, b.plan.Aim);
            Assert.IsTrue(b.plan.Leak);
            Assert.AreEqual(a.plan.Hold.At, b.plan.Hold.At);
            Assert.AreEqual(a.plan.Intake.Lateral, b.plan.Intake.Lateral);
            Assert.AreEqual(2, b.plan.Signs.Count);
            Assert.AreEqual("pull", b.plan.Signs[1].Id);
            Assert.IsTrue(b.plan.Signs[0].FacingFromPose);
        }

        [Test]
        public void RoundTrip_WithoutPlan_StaysNull()
        {
            SpellIR a = Sample();
            a.plan = null;
            Assert.IsTrue(SpellIRCodec.TryDecode(SpellIRCodec.Encode(a), 0f, out SpellIR b));
            Assert.IsNull(b.plan);
        }

        [Test]
        public void InactiveSpell_DecodesWithoutActivationTime()
        {
            SpellIR a = Sample();
            a.active = false;
            Assert.IsTrue(SpellIRCodec.TryDecode(SpellIRCodec.Encode(a), 50f, out SpellIR b));
            Assert.AreEqual(-1f, b.activatedAt);
        }

        [Test]
        public void TryDecode_RejectsTruncatedAndGarbage()
        {
            byte[] data = SpellIRCodec.Encode(Sample());
            for (int cut = 0; cut < data.Length; cut += 7)
            {
                var truncated = new byte[cut];
                System.Array.Copy(data, truncated, cut);
                Assert.IsFalse(SpellIRCodec.TryDecode(truncated, 0f, out _), "corte en " + cut);
            }
            Assert.IsFalse(SpellIRCodec.TryDecode(null, 0f, out _));
            Assert.IsFalse(SpellIRCodec.TryDecode(new byte[] { 9, 9, 9, 9 }, 0f, out _), "version desconocida");
        }

        [Test]
        public void TryDecode_RejectsNonFiniteNumbers()
        {
            SpellIR a = Sample();
            a.force = float.NaN;
            Assert.IsFalse(SpellIRCodec.TryDecode(SpellIRCodec.Encode(a), 0f, out _));
            a = Sample();
            a.sealRadius = float.PositiveInfinity;
            Assert.IsFalse(SpellIRCodec.TryDecode(SpellIRCodec.Encode(a), 0f, out _));
        }
    }
}
