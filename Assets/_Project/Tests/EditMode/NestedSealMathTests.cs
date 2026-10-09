using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Casting;
using SpellSimulator.Core;

namespace SpellSimulator.Tests
{
    /// <summary>Deteccion de anillos anidados y particion de trazos de un sello compuesto (NestedSealReader, sin modelo).</summary>
    public class NestedSealMathTests
    {
        private static readonly Vector2 C = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 Left = new Vector2(0.32f, 0.5f);
        private static readonly Vector2 Right = new Vector2(0.68f, 0.5f);
        private const float Outer = 0.45f, Inner = 0.15f;

        // Signo de prueba: linea horizontal corta (no se ajusta a un circulo).
        private static Stroke Mark(Vector2 at) => TestStrokes.Line(at + new Vector2(-0.05f, 0f), at + new Vector2(0.05f, 0f), 6);

        [Test]
        public void TwoClosedInners_AreDetectedAndPartitioned()
        {
            Stroke markL = Mark(Left), ringL = TestStrokes.Circle(Left, Inner, 64);
            Stroke markR = Mark(Right), ringR = TestStrokes.Circle(Right, Inner, 64);
            Stroke outer = TestStrokes.Circle(C, Outer, 96);
            Stroke loose = Mark(new Vector2(0.5f, 0.82f)); // fuera de los interiores: se ignora
            var session = TestStrokes.Session(markL, ringL, markR, ringR, loose, outer);

            Assert.IsTrue(NestedSealReader.TryLayout(session, out NestedLayout layout));
            Assert.AreEqual(2, layout.Inners.Count);
            Assert.IsTrue(layout.Outer.Contains(outer));

            NestedInner left = layout.Inners[0].Ring.Center.x < 0.5f ? layout.Inners[0] : layout.Inners[1];
            NestedInner right = left == layout.Inners[0] ? layout.Inners[1] : layout.Inners[0];
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, left.StrokeIndices);
            CollectionAssert.AreEquivalent(new[] { 2, 3 }, right.StrokeIndices);
            Assert.AreEqual(Inner, left.Ring.Radius, 0.01f);
        }

        [Test]
        public void SingleInner_IsNotNested()
        {
            var session = TestStrokes.Session(Mark(C), TestStrokes.Circle(C, 0.2f, 64), TestStrokes.Circle(C, Outer, 96));
            Assert.IsFalse(NestedSealReader.TryLayout(session, out _));
        }

        [Test]
        public void OpenInner_IsNotNested()
        {
            var session = TestStrokes.Session(
                Mark(Left), TestStrokes.Circle(Left, Inner, 64),
                Mark(Right), TestStrokes.Arc(Right, Inner, 0f, 250f, 48),
                TestStrokes.Circle(C, Outer, 96));
            Assert.IsFalse(NestedSealReader.TryLayout(session, out _));
        }

        [Test]
        public void OpenOuter_IsNotNested()
        {
            var session = TestStrokes.Session(
                TestStrokes.Circle(Left, Inner, 64), TestStrokes.Circle(Right, Inner, 64),
                TestStrokes.Arc(C, Outer, 0f, 250f, 72));
            Assert.IsFalse(NestedSealReader.TryLayout(session, out _));
        }

        [Test]
        public void OverlappingInners_AreNotNested()
        {
            var session = TestStrokes.Session(
                TestStrokes.Circle(new Vector2(0.4f, 0.5f), Inner, 64), TestStrokes.Circle(new Vector2(0.6f, 0.5f), Inner, 64),
                TestStrokes.Circle(C, Outer, 96));
            Assert.IsFalse(NestedSealReader.TryLayout(session, out _));
        }

        [Test]
        public void SubSession_NormalizesInnerToTemplateSize()
        {
            var session = TestStrokes.Session(Mark(Left), TestStrokes.Circle(Left, Inner, 64), TestStrokes.Circle(Right, Inner, 64),
                TestStrokes.Circle(C, Outer, 96));
            Assert.IsTrue(NestedSealReader.TryLayout(session, out NestedLayout layout));
            NestedInner left = layout.Inners[0].Ring.Center.x < 0.5f ? layout.Inners[0] : layout.Inners[1];

            DrawingSession sub = NestedSealReader.SubSession(session, left, out float boost, out Vector2 pivot);
            Assert.AreEqual(left.StrokeIndices.Count, sub.Strokes.Count);
            foreach (var stroke in sub.Strokes)
            {
                if (stroke.Points.Count < 20) continue; // el signo
                foreach (var p in stroke.Points)
                    Assert.AreEqual(NestedSealReader.TargetRadius, Vector2.Distance(p, C), 0.01f);
            }
            // La transformacion equivale a agrandar alrededor del pivote (como SpellIR.analysisBoost).
            Vector2 original = session.Strokes[0].Points[0];
            Vector2 mapped = sub.Strokes[0].Points[0];
            Assert.AreEqual(mapped.x, (pivot + (original - pivot) * boost).x, 1e-3f);
            Assert.AreEqual(mapped.y, (pivot + (original - pivot) * boost).y, 1e-3f);
        }
    }
}
