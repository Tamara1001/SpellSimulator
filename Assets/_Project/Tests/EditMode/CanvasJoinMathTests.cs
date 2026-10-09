using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Casting;
using SpellSimulator.Core;

namespace SpellSimulator.Tests
{
    public class CanvasJoinMathTests
    {
        // Lienzo de 0,3 x 0,2 m en el plano XY, con la cara hacia +Z.
        private static CanvasRect Rect(Vector3 center, float w = 0.3f, float h = 0.2f) =>
            new CanvasRect(center, Vector3.right, Vector3.up, Vector3.forward, w, h);

        private static DrawingSession Session(params Vector2[] points)
        {
            var s = new DrawingSession();
            s.AddStroke(new Stroke(new System.Collections.Generic.List<Vector2>(points)) { Width = 0.01f, Ink = InkKind.Normal });
            return s;
        }

        [Test]
        public void SideBySide_AreAdjacent_AndMergeSplitsTheEnvelope()
        {
            var a = Rect(Vector3.zero);
            var b = Rect(new Vector3(0.3f, 0.01f, 0.02f));
            Assert.IsTrue(CanvasJoinMath.AreAdjacent(a, b));

            var merged = CanvasJoinMath.Merge(a, Session(new Vector2(1f, 0.5f)), b, Session(new Vector2(0f, 0.5f)), out var layout);
            Assert.AreEqual(0.6f, layout.width, 1e-4f);
            Assert.AreEqual(0.2f, layout.height, 0.02f);
            Assert.AreEqual(2, merged.Strokes.Count);
            Assert.AreEqual(0.5f, merged.Strokes[0].Points[0].x, 1e-3f, "El borde derecho de A cae en el medio.");
            Assert.AreEqual(0.5f, merged.Strokes[1].Points[0].x, 1e-3f, "El borde izquierdo de B cae en el medio.");
            Assert.AreEqual(0.01f, merged.Strokes[0].Width, 1e-6f);
            Assert.AreEqual(InkKind.Normal, merged.Strokes[1].Ink);
        }

        [Test]
        public void SideBySide_LeftNeighbour_AlsoJoins()
        {
            Assert.IsTrue(CanvasJoinMath.AreAdjacent(Rect(Vector3.zero), Rect(new Vector3(-0.3f, 0f, 0f))));
        }

        [Test]
        public void AboveAndBelow_AreAdjacent_AndMergeStacksVertically()
        {
            var a = Rect(Vector3.zero);
            var b = Rect(new Vector3(0f, 0.2f, 0f));
            Assert.IsTrue(CanvasJoinMath.AreAdjacent(a, b));
            Assert.IsTrue(CanvasJoinMath.AreAdjacent(b, a));

            var merged = CanvasJoinMath.Merge(a, Session(new Vector2(0.5f, 0.5f)), b, Session(new Vector2(0.5f, 0.5f)), out var layout);
            Assert.AreEqual(0.4f, layout.height, 1e-4f);
            Assert.AreEqual(0.25f, merged.Strokes[0].Points[0].y, 1e-3f, "A queda abajo.");
            Assert.AreEqual(0.75f, merged.Strokes[1].Points[0].y, 1e-3f, "B queda arriba.");
            Assert.AreEqual(0.5f, merged.Strokes[1].Points[0].x, 1e-3f);
        }

        [Test]
        public void RotatedNeighbour_90Degrees_JoinsAndMapsPoints()
        {
            var a = Rect(Vector3.zero);
            // B girado 90 grados en el plano: su ancho (0,2) corre hacia arriba y su alto (0,3) hacia -X.
            var b = new CanvasRect(new Vector3(0.3f, 0f, 0f), Vector3.up, Vector3.left, Vector3.forward, 0.2f, 0.3f);
            Assert.IsTrue(CanvasJoinMath.AreAdjacent(a, b));

            var merged = CanvasJoinMath.Merge(a, new DrawingSession(), b, Session(new Vector2(0.5f, 1f), new Vector2(1f, 0.5f)), out var layout);
            Assert.AreEqual(0.6f, layout.width, 1e-4f);
            Assert.AreEqual(0.2f, layout.height, 1e-4f);
            Assert.AreEqual(0.5f, merged.Strokes[0].Points[0].x, 1e-3f, "El v=1 de B apunta hacia -X: toca el borde comun.");
            Assert.AreEqual(0.5f, merged.Strokes[0].Points[0].y, 1e-3f);
            Assert.AreEqual(1f, merged.Strokes[0].Points[1].y, 1e-3f, "El u=1 de B apunta hacia +Y.");
        }

        [Test]
        public void FarApart_AreNotAdjacent()
        {
            Assert.IsFalse(CanvasJoinMath.AreAdjacent(Rect(Vector3.zero), Rect(new Vector3(0.8f, 0f, 0f))));
        }

        [Test]
        public void NotCoplanar_OrFlipped_AreNotAdjacent()
        {
            Assert.IsFalse(CanvasJoinMath.AreAdjacent(Rect(Vector3.zero), Rect(new Vector3(0.3f, 0f, 0.3f))), "Plano distinto.");
            var flipped = new CanvasRect(new Vector3(0.3f, 0f, 0f), Vector3.right, Vector3.up, Vector3.back, 0.3f, 0.2f);
            Assert.IsFalse(CanvasJoinMath.AreAdjacent(Rect(Vector3.zero), flipped), "Cara con tinta al reves.");
            var tilted = new CanvasRect(new Vector3(0.3f, 0f, 0f), Vector3.right, Vector3.up, new Vector3(0f, 0.7f, 0.7f).normalized, 0.3f, 0.2f);
            Assert.IsFalse(CanvasJoinMath.AreAdjacent(Rect(Vector3.zero), tilted), "Inclinada.");
        }

        [Test]
        public void InsufficientOverlapAlongTheEdge_IsNotAdjacent()
        {
            // Apoyados 0,01 m de 0,2: casi solo se tocan en la esquina.
            Assert.IsFalse(CanvasJoinMath.AreAdjacent(Rect(Vector3.zero), Rect(new Vector3(0.3f, 0.19f, 0f))));
            // Con la mitad apoyada si.
            Assert.IsTrue(CanvasJoinMath.AreAdjacent(Rect(Vector3.zero), Rect(new Vector3(0.3f, 0.1f, 0f))));
        }

        [Test]
        public void HeavilyOverlapped_IsNotAdjacent()
        {
            Assert.IsFalse(CanvasJoinMath.AreAdjacent(Rect(Vector3.zero), Rect(new Vector3(0.05f, 0f, 0f))), "Una casi encima de otra.");
            Assert.IsFalse(CanvasJoinMath.AreAdjacent(Rect(Vector3.zero), Rect(Vector3.zero)), "Superpuestas.");
        }

        [Test]
        public void Merge_TolerantOfEmptySession()
        {
            var merged = CanvasJoinMath.Merge(Rect(Vector3.zero), Session(new Vector2(0.5f, 0.5f)), Rect(new Vector3(0.3f, 0f, 0f)), null, out _);
            Assert.AreEqual(1, merged.Strokes.Count);
        }
    }
}
