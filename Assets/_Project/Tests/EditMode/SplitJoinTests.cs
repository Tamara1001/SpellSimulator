using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Casting;
using SpellSimulator.Core;

namespace SpellSimulator.Tests
{
    public class SplitJoinTests
    {
        private static DrawingSession Session(params Vector2[] points)
        {
            var s = new DrawingSession();
            s.AddStroke(new Stroke(new System.Collections.Generic.List<Vector2>(points)) { Width = 0.01f });
            return s;
        }

        [Test]
        public void Merge_PlacesEachHalfInItsShareOfTheJoinedCanvas()
        {
            var left = Session(new Vector2(0f, 0.5f), new Vector2(1f, 0.5f));
            var right = Session(new Vector2(0f, 0.2f), new Vector2(1f, 0.8f));
            var merged = SplitJoinMath.Merge(left, 0.3f, right, 0.3f);

            Assert.AreEqual(2, merged.Strokes.Count);
            Assert.AreEqual(0f, merged.Strokes[0].Points[0].x, 1e-5f);
            Assert.AreEqual(0.5f, merged.Strokes[0].Points[1].x, 1e-5f, "El borde derecho de la mitad izquierda cae en el centro.");
            Assert.AreEqual(0.5f, merged.Strokes[1].Points[0].x, 1e-5f);
            Assert.AreEqual(1f, merged.Strokes[1].Points[1].x, 1e-5f);
            Assert.AreEqual(0.8f, merged.Strokes[1].Points[1].y, 1e-5f, "La altura no cambia.");
            Assert.AreEqual(0.01f, merged.Strokes[0].Width, 1e-6f);
        }

        [Test]
        public void Merge_UnevenWidths_SplitProportionally()
        {
            var left = Session(new Vector2(1f, 0.5f));
            var right = Session(new Vector2(0f, 0.5f));
            var merged = SplitJoinMath.Merge(left, 0.2f, right, 0.6f);
            Assert.AreEqual(0.25f, merged.Strokes[0].Points[0].x, 1e-5f);
            Assert.AreEqual(0.25f, merged.Strokes[1].Points[0].x, 1e-5f);
        }

        [Test]
        public void Merge_TolerantOfEmptyHalf()
        {
            var merged = SplitJoinMath.Merge(Session(new Vector2(0.5f, 0.5f)), 0.3f, new DrawingSession(), 0.3f);
            Assert.AreEqual(1, merged.Strokes.Count);
        }

        [Test]
        public void RightNeighbour_TouchingHalves_Join()
        {
            Assert.IsTrue(SplitJoinMath.IsRightNeighbour(Vector3.zero, Vector3.right, Vector3.up, 0.3f,
                new Vector3(0.31f, 0.01f, 0.02f), Vector3.up, 0.3f));
        }

        [Test]
        public void RightNeighbour_TooFarOrOnTheWrongSideOrFlipped_DoNotJoin()
        {
            Assert.IsFalse(SplitJoinMath.IsRightNeighbour(Vector3.zero, Vector3.right, Vector3.up, 0.3f, new Vector3(0.6f, 0f, 0f), Vector3.up, 0.3f), "Separadas.");
            Assert.IsFalse(SplitJoinMath.IsRightNeighbour(Vector3.zero, Vector3.right, Vector3.up, 0.3f, new Vector3(-0.3f, 0f, 0f), Vector3.up, 0.3f), "Del lado izquierdo.");
            Assert.IsFalse(SplitJoinMath.IsRightNeighbour(Vector3.zero, Vector3.right, Vector3.up, 0.3f, new Vector3(0.3f, 0f, 0f), Vector3.down, 0.3f), "Una dada vuelta.");
            Assert.IsFalse(SplitJoinMath.IsRightNeighbour(Vector3.zero, Vector3.right, Vector3.up, 0.3f, new Vector3(0.3f, 0f, 0.5f), Vector3.up, 0.3f), "Desfasada.");
        }
    }
}
