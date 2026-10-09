using NUnit.Framework;
using UnityEngine;
using SpellSimulator.World;

namespace SpellSimulator.Tests
{
    public class PortalVisualCurveTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void Dormant_DarkDiscFaintGlyphsNoLight()
        {
            PortalLevels l = PortalVisualCurve.Evaluate(PortalState.Dormant, 0.7f, 1f, 3f);
            Assert.AreEqual(0f, l.Disc, Eps);
            Assert.AreEqual(0f, l.Light, Eps);
            Assert.AreEqual(PortalVisualCurve.DormantGlyphs, l.Glyphs, Eps);
            Assert.AreEqual(PortalVisualCurve.DormantRim, l.Rim, Eps);
        }

        [Test]
        public void Charging_Extremes_StartAtDormantAndEndFull()
        {
            PortalLevels start = PortalVisualCurve.Evaluate(PortalState.Charging, 0f, 0f, 0f);
            Assert.AreEqual(PortalVisualCurve.DormantGlyphs, start.Glyphs, Eps);
            Assert.AreEqual(0f, start.Light, Eps);
            PortalLevels end = PortalVisualCurve.Evaluate(PortalState.Charging, 1f, 0f, 0f);
            Assert.AreEqual(1f, end.Glyphs, Eps);
            Assert.AreEqual(1f, end.Rim, Eps);
            Assert.AreEqual(1f, end.Charge, Eps);
        }

        [Test]
        public void Charging_IsMonotonic()
        {
            PortalLevels prev = PortalVisualCurve.Evaluate(PortalState.Charging, 0f, 0f, 0f);
            for (int i = 1; i <= 100; i++)
            {
                PortalLevels l = PortalVisualCurve.Evaluate(PortalState.Charging, i / 100f, 0f, i * 0.37f);
                Assert.GreaterOrEqual(l.Glyphs, prev.Glyphs - Eps, $"grabados en {i}");
                Assert.GreaterOrEqual(l.Rim, prev.Rim - Eps, $"borde en {i}");
                Assert.GreaterOrEqual(l.Disc, prev.Disc - Eps, $"disco en {i}");
                Assert.GreaterOrEqual(l.Light, prev.Light - Eps, $"luz en {i}");
                prev = l;
            }
        }

        [Test]
        public void Staggered_EachGlyphMonotonicFromDormantToFull()
        {
            const int count = 3;
            for (int g = 0; g < count; g++)
            {
                Assert.AreEqual(PortalVisualCurve.DormantGlyphs, PortalVisualCurve.Staggered(0f, g, count), Eps);
                Assert.AreEqual(1f, PortalVisualCurve.Staggered(1f, g, count), Eps);
                float prev = 0f;
                for (int i = 0; i <= 50; i++)
                {
                    float v = PortalVisualCurve.Staggered(i / 50f, g, count);
                    Assert.GreaterOrEqual(v, prev - Eps);
                    prev = v;
                }
            }
            // El primero se enciende antes que el ultimo.
            Assert.Greater(PortalVisualCurve.Staggered(0.3f, 0, count), PortalVisualCurve.Staggered(0.3f, 2, count));
        }

        [Test]
        public void Open_FullUntilWarning()
        {
            for (int i = 0; i < 40; i++)
            {
                PortalLevels l = PortalVisualCurve.Evaluate(PortalState.Open, 1f, PortalVisualCurve.WarnSeconds + 2f, i * 0.113f);
                Assert.AreEqual(1f, l.Disc, Eps);
                Assert.AreEqual(1f, l.Light, Eps);
                Assert.AreEqual(1f, l.Glyphs, Eps);
            }
        }

        [Test]
        public void Open_BlinksInLastSeconds()
        {
            float min = 1f, max = 0f;
            for (int i = 0; i <= 200; i++)
            {
                float v = PortalVisualCurve.Evaluate(PortalState.Open, 1f, 1.5f, i / 200f).Light;
                Assert.That(v, Is.InRange(0f, 1f));
                min = Mathf.Min(min, v);
                max = Mathf.Max(max, v);
            }
            Assert.Less(min, 0.7f, "en los ultimos segundos la luz baja");
            Assert.Greater(max, 0.95f, "y vuelve a subir (parpadeo)");
            // Mas cerca del cierre, el parpadeo es mas hondo.
            Assert.Less(PortalVisualCurve.Blink(0.2f, 0.2f), PortalVisualCurve.Blink(2.8f, 0.2f));
        }

        [Test]
        public void Cooldown_FadesOutMonotonically()
        {
            PortalLevels open = PortalVisualCurve.Evaluate(PortalState.Open, 1f, 10f, 0f);
            PortalLevels prev = PortalVisualCurve.Evaluate(PortalState.Cooldown, 0f, 0f, 0f);
            Assert.LessOrEqual(prev.Disc, open.Disc);
            for (int i = 1; i <= 20; i++)
            {
                PortalLevels l = PortalVisualCurve.Evaluate(PortalState.Cooldown, i / 20f, 0f, 0f);
                Assert.LessOrEqual(l.Disc, prev.Disc + Eps);
                Assert.LessOrEqual(l.Light, prev.Light + Eps);
                prev = l;
            }
            Assert.AreEqual(0f, prev.Disc, Eps);
            Assert.AreEqual(0f, prev.Light, Eps);
        }

        [Test]
        public void Pulse_DormantIsStill()
        {
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(1f, PortalVisualCurve.Pulse(PortalState.Dormant, 0f, i * 0.31f), Eps);
        }

        [Test]
        public void TintFor_StableAndDistinctPerPortal()
        {
            string[] ids = { "tower-study", "watchtower", "field-circle", "cave" };
            for (int i = 0; i < ids.Length; i++)
            {
                Assert.AreEqual(PortalVisualCurve.TintFor(ids[i]), PortalVisualCurve.TintFor(ids[i]));
                for (int j = i + 1; j < ids.Length; j++)
                    Assert.AreNotEqual(PortalVisualCurve.TintFor(ids[i]), PortalVisualCurve.TintFor(ids[j]));
            }
            Assert.AreEqual(PortalVisualCurve.TintFor("otro-portal"), PortalVisualCurve.TintFor("otro-portal"));
        }
    }
}
