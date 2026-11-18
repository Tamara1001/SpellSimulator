using NUnit.Framework;
using SpellSimulator.Canvas;
using SpellSimulator.Core;

namespace SpellSimulator.Tests
{
    public class RestockRulesTests
    {
        [Test]
        public void Default_Is_A_Valid_Medium_Sheet()
        {
            CanvasSpec spec = RestockSelection.Default.ToSpec();
            Assert.AreEqual(CanvasKind.Sheet, spec.kind);
            Assert.AreEqual(CanvasSize.M, spec.size);
            Assert.AreEqual(1, spec.pages);
        }

        [Test]
        public void Notebook_Shape_Follows_Size_And_Pages_Are_Limited_To_Eight()
        {
            RestockSelection s = RestockRules.ToggleKind(RestockSelection.Default); // libreta M
            Assert.AreEqual(CanvasKind.Notebook, s.kind);
            Assert.AreEqual(CanvasShape.Rect, s.ToSpec().shape);
            s = RestockRules.CycleSize(RestockRules.CycleSize(s)); // M -> L -> S
            Assert.AreEqual(CanvasSize.S, s.size);
            Assert.AreEqual(CanvasShape.Round, s.ToSpec().shape);
            for (int i = 0; i < 20; i++) s = RestockRules.AddPages(s, 1);
            Assert.AreEqual(CanvasRules.MaxPages, s.ToSpec().pages);
            for (int i = 0; i < 20; i++) s = RestockRules.AddPages(s, -1);
            Assert.AreEqual(1, s.ToSpec().pages);
        }

        [Test]
        public void Shape_Only_Cycles_For_Sheets()
        {
            RestockSelection s = RestockRules.CycleShape(RestockSelection.Default);
            Assert.AreEqual(CanvasShape.Rect, s.ToSpec().shape);
            Assert.AreEqual(CanvasShape.Square, RestockRules.CycleShape(s).ToSpec().shape);
            RestockSelection book = RestockRules.ToggleKind(s);
            Assert.AreEqual(book.ToSpec().shape, RestockRules.CycleShape(book).ToSpec().shape);
            // Volver a hoja suelta nunca deja una forma redonda.
            RestockSelection back = RestockRules.ToggleKind(RestockRules.CycleSize(RestockRules.ToggleKind(RestockRules.CycleSize(RestockRules.CycleSize(RestockSelection.Default)))));
            Assert.AreNotEqual(CanvasShape.Round, back.ToSpec().shape);
        }

        [Test]
        public void Describe_Mentions_Kind_Size_And_Pages()
        {
            string book = RestockRules.Describe(RestockRules.ToggleKind(RestockSelection.Default));
            StringAssert.Contains("Libreta M", book);
            StringAssert.Contains("8 hojas", book);
            StringAssert.Contains("Lienzo M", RestockRules.Describe(RestockSelection.Default));
            StringAssert.Contains("mm", RestockRules.Describe(BrushRules.Catalog[2]));
        }
    }
}
