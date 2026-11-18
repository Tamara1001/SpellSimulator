using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Core;
using SpellSimulator.Recognition;

namespace SpellSimulator.Tests
{
    public class BrushRulesTests
    {
        [Test]
        public void Catalog_Is_Sorted_By_Width_And_Within_Range()
        {
            float last = 0f;
            foreach (BrushSpec b in BrushRules.Catalog)
            {
                Assert.Greater(b.widthMeters, last);
                Assert.GreaterOrEqual(b.widthMeters, BrushRules.MinWidth);
                Assert.LessOrEqual(b.widthMeters, BrushRules.MaxWidth);
                last = b.widthMeters;
            }
        }

        [Test]
        public void Legacy_Definitions_Keep_Their_Look()
        {
            // Lapiz (0,04) y Pincel (0,1) de siempre arrancaban en «fino» (×0,2).
            Assert.AreEqual(0.008f, BrushRules.FromLegacyDefinition(0.04f), 1e-5f);
            Assert.AreEqual(0.02f, BrushRules.FromLegacyDefinition(0.1f), 1e-5f);
            Assert.AreEqual(BrushRules.LegacyPenInkPerMeter, BrushRules.InkPerMeter(BrushRules.LegacyPenWidth), 1e-4f);
        }

        [Test]
        public void Ink_Cost_Grows_With_Width_But_Less_Than_Proportionally()
        {
            float thin = BrushRules.InkPerMeter(0.008f), thick = BrushRules.InkPerMeter(0.04f);
            Assert.Greater(thick, thin);
            Assert.Less(thick / thin, 5f);
        }

        [Test]
        public void Sanitize_Clamps_And_Names()
        {
            BrushSpec s = BrushRules.Sanitize(new BrushSpec(null, 5f));
            Assert.AreEqual(BrushRules.MaxWidth, s.widthMeters);
            Assert.AreEqual("Pincel", s.name);
            Assert.AreEqual(1, BrushRules.NearestInCatalog(0.009f));
        }

        [Test]
        public void Recognition_Tensor_Does_Not_Depend_On_Stroke_Width()
        {
            // El grosor del trazo es solo visual: la rasterizacion del modelo siempre usa 2 px (TensorRasterizer.StrokeWidth).
            var entry = TestAssets.Dictionary.Get("fire");
            var thin = new DrawingSession();
            var thick = new DrawingSession();
            foreach (var pts in entry.TemplateStrokes)
            {
                thin.AddStroke(new Stroke(new System.Collections.Generic.List<Vector2>(pts)) { Width = BrushRules.MinWidth });
                thick.AddStroke(new Stroke(new System.Collections.Generic.List<Vector2>(pts)) { Width = BrushRules.MaxWidth });
            }
            float[] a = TensorRasterizer.Rasterize(thin), b = TensorRasterizer.Rasterize(thick);
            CollectionAssert.AreEqual(a, b);
        }
    }
}
