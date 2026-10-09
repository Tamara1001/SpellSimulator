using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Casting;
using SpellSimulator.Compiler;

namespace SpellSimulator.Tests
{
    public class SpellFusionTests
    {
        private static SpellIR Make(ElementType element, string sigil, float strength, float radius, PlanPrimitive primitives)
        {
            SpellIR ir = SpellIR.Invalid("ok", null);
            ir.valid = true;
            ir.active = true;
            ir.element = element;
            ir.sigilId = sigil;
            ir.strength = strength;
            ir.sealRadius = radius;
            ir.inkPower = 1f;
            ir.plan = new SpellPlan { Primitives = primitives, Budget = 1f };
            return ir;
        }

        [Test]
        public void Merge_KeepsElementSigilAndSizeOfTheBigOne()
        {
            var big = Make(ElementType.Fire, "fire_a", 0.6f, 0.4f, PlanPrimitive.Burst);
            var small = Make(ElementType.Wind, "wind_a", 0.3f, 0.1f, PlanPrimitive.Burst);
            var merged = SpellFusion.Merge(big, small);
            Assert.AreEqual(ElementType.Fire, merged.element);
            Assert.AreEqual("fire_a", merged.sigilId);
            Assert.AreEqual(0.4f, merged.sealRadius, 1e-6f);
        }

        [Test]
        public void Merge_StrengthIsQuadratureCappedAtOne()
        {
            var a = Make(ElementType.Fire, "f", 0.3f, 0.3f, PlanPrimitive.Burst);
            var b = Make(ElementType.Wind, "w", 0.4f, 0.2f, PlanPrimitive.Burst);
            Assert.AreEqual(0.5f, SpellFusion.Merge(a, b).strength, 1e-5f);
            a.strength = 0.9f;
            b.strength = 0.9f;
            Assert.AreEqual(1f, SpellFusion.Merge(a, b).strength, 1e-6f);
        }

        [Test]
        public void Merge_TakesMaxOfInkPowerForceRangeDuration()
        {
            var a = Make(ElementType.Fire, "f", 0.5f, 0.3f, PlanPrimitive.Burst);
            var b = Make(ElementType.Water, "w", 0.5f, 0.2f, PlanPrimitive.Burst);
            a.inkPower = 1f; b.inkPower = 2.5f;
            a.force = 0.2f; b.force = 0.7f;
            a.range = 0.9f; b.range = 0.1f;
            a.duration = 3f; b.duration = 8f;
            var m = SpellFusion.Merge(a, b);
            Assert.AreEqual(2.5f, m.inkPower, 1e-6f);
            Assert.AreEqual(0.7f, m.force, 1e-6f);
            Assert.AreEqual(0.9f, m.range, 1e-6f);
            Assert.AreEqual(8f, m.duration, 1e-6f);
        }

        [Test]
        public void Merge_UnionsPlanPrimitivesAndCopiesMissingData()
        {
            var big = Make(ElementType.Fire, "f", 0.5f, 0.3f, PlanPrimitive.Jet);
            big.plan.Aim = new Vector3(0f, 0f, 1f);
            var small = Make(ElementType.Wind, "w", 0.5f, 0.2f, PlanPrimitive.Jet | PlanPrimitive.Vortex | PlanPrimitive.Fan);
            small.plan.Aim = new Vector3(1f, 0f, 0f);
            small.plan.Circulation = 2f;
            small.plan.Dispersion = 1.5f;
            var m = SpellFusion.Merge(big, small);
            Assert.IsTrue(m.plan.Has(PlanPrimitive.Jet));
            Assert.IsTrue(m.plan.Has(PlanPrimitive.Vortex));
            Assert.IsTrue(m.plan.Has(PlanPrimitive.Fan));
            Assert.AreEqual(new Vector3(0f, 0f, 1f), m.plan.Aim, "El chorro de la grande manda.");
            Assert.AreEqual(2f, m.plan.Circulation, 1e-6f);
            Assert.AreEqual(1.5f, m.plan.Dispersion, 1e-6f);
        }

        [Test]
        public void Merge_DoesNotMutateInputs()
        {
            var big = Make(ElementType.Fire, "f", 0.5f, 0.3f, PlanPrimitive.Burst);
            var small = Make(ElementType.Wind, "w", 0.5f, 0.2f, PlanPrimitive.Vortex);
            SpellFusion.Merge(big, small);
            Assert.AreEqual(PlanPrimitive.Burst, big.plan.Primitives);
            Assert.AreEqual(PlanPrimitive.Vortex, small.plan.Primitives);
        }
    }
}
