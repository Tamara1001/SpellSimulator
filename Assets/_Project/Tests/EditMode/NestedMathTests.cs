using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Casting;

namespace SpellSimulator.Tests
{
    /// <summary>Geometria, amplificacion y retardo de los sellos compuestos (NestedMath, SealCharge.NestedDuration).</summary>
    public class NestedMathTests
    {
        [Test]
        public void IsInside_RespectsMargin()
        {
            var c = new Vector2(0.5f, 0.5f);
            Assert.IsTrue(NestedMath.IsInside(c + new Vector2(0.2f, 0f), 0.15f, c, 0.45f)); // 0,35 <= 0,414
            // Justo en el limite del margen: dentro.
            float limit = 0.45f * (1f - NestedMath.ContainmentMargin);
            Assert.IsTrue(NestedMath.IsInside(c + new Vector2(limit - 0.1f - 1e-4f, 0f), 0.1f, c, 0.45f));
            // Toca la linea del exterior (sin margen): fuera.
            Assert.IsFalse(NestedMath.IsInside(c + new Vector2(0.3f, 0f), 0.15f, c, 0.45f));
            Assert.IsFalse(NestedMath.IsInside(c, 0f, c, 0.45f));
        }

        [Test]
        public void FitsAsInner_RejectsTooBig()
        {
            var c = new Vector2(0.5f, 0.5f);
            Assert.IsTrue(NestedMath.FitsAsInner(c, 0.45f * NestedMath.MaxInnerRadiusRatio - 0.001f, c, 0.45f)); // margen por la precision de float
            Assert.IsFalse(NestedMath.FitsAsInner(c, 0.45f * NestedMath.MaxInnerRadiusRatio + 0.01f, c, 0.45f));
        }

        [Test]
        public void Overlaps_ByRadiiSum()
        {
            Assert.IsTrue(NestedMath.Overlaps(Vector2.zero, 0.15f, new Vector2(0.29f, 0f), 0.15f));
            Assert.IsFalse(NestedMath.Overlaps(Vector2.zero, 0.15f, new Vector2(0.31f, 0f), 0.15f));
        }

        [Test]
        public void BelongsTo_UsesTolerance()
        {
            float r = 0.2f;
            Assert.IsTrue(NestedMath.BelongsTo(new Vector2(r * (1f + NestedMath.StrokeTolerance) - 1e-4f, 0f), Vector2.zero, r));
            Assert.IsFalse(NestedMath.BelongsTo(new Vector2(r * (1f + NestedMath.StrokeTolerance) + 1e-3f, 0f), Vector2.zero, r));
        }

        [Test]
        public void Centroid_IsMean()
        {
            var points = new[] { new Vector2(0f, 0f), new Vector2(2f, 0f), new Vector2(1f, 3f) };
            Assert.AreEqual(new Vector2(1f, 1f), NestedMath.Centroid(points));
            Assert.AreEqual(Vector2.zero, NestedMath.Centroid(new Vector2[0]));
        }

        [Test]
        public void Amplification_IsBoundedAndNeutralWithoutInners()
        {
            Assert.AreEqual(1f, NestedMath.Amplification(0.45f, null), 1e-5f);
            Assert.AreEqual(1f, NestedMath.Amplification(0.45f, new float[0]), 1e-5f);
            Assert.AreEqual(1f, NestedMath.Amplification(0.45f, new[] { 0.45f }), 1e-5f);
            Assert.AreEqual(NestedMath.MaxAmplification, NestedMath.Amplification(1f, new[] { 0.01f, 0.01f, 0.01f, 0.01f }), 1e-5f);
            for (float inner = 0.02f; inner <= 0.27f; inner += 0.01f)
            {
                float a = NestedMath.Amplification(0.45f, new[] { inner, inner });
                Assert.GreaterOrEqual(a, 1f);
                Assert.LessOrEqual(a, NestedMath.MaxAmplification);
            }
        }

        [Test]
        public void Amplification_GrowsWithOuterAndCount_ShrinksWithInner()
        {
            float prev = 0f;
            for (float outer = 0.3f; outer <= 0.6f; outer += 0.02f)
            {
                float a = NestedMath.Amplification(outer, new[] { 0.15f, 0.15f });
                Assert.GreaterOrEqual(a, prev - 1e-6f);
                prev = a;
            }

            float two = NestedMath.Amplification(0.45f, new[] { 0.12f, 0.12f });
            float three = NestedMath.Amplification(0.45f, new[] { 0.12f, 0.12f, 0.12f });
            float four = NestedMath.Amplification(0.45f, new[] { 0.12f, 0.12f, 0.12f, 0.12f });
            Assert.Greater(three, two);
            Assert.Greater(four, three);
            // Rendimientos decrecientes: el cuarto suma menos que el tercero.
            Assert.Less(four - three, three - two);

            Assert.Greater(NestedMath.Amplification(0.45f, new[] { 0.1f, 0.1f }), NestedMath.Amplification(0.45f, new[] { 0.2f, 0.1f }));
        }

        [Test]
        public void EffectScaleFactor_IsSqrtAndNeverShrinks()
        {
            Assert.AreEqual(1f, NestedMath.EffectScaleFactor(0.5f), 1e-5f);
            Assert.AreEqual(2f, NestedMath.EffectScaleFactor(4f), 1e-5f);
        }

        [Test]
        public void NestedDuration_SumsAndClamps()
        {
            Assert.AreEqual(1.5f, SealCharge.NestedDuration(new[] { 0.5f, 1f }), 1e-5f);
            Assert.AreEqual(SealCharge.MaxNestedSeconds, SealCharge.NestedDuration(new[] { 2f, 2f, 2f }), 1e-5f);
            Assert.AreEqual(SealCharge.MinSeconds, SealCharge.NestedDuration(null), 1e-5f);
            Assert.AreEqual(SealCharge.MinSeconds, SealCharge.NestedDuration(new float[0]), 1e-5f);
            Assert.AreEqual(1f, SealCharge.NestedDuration(new[] { 1f, -3f }), 1e-5f);
        }
    }
}
