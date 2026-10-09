using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Casting;
using SpellSimulator.Core;
using SpellSimulator.Parsing;

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

        // ---------------------------------------------------------------- tolerancias generosas (hojas apoyadas casi tocandose)

        [Test]
        public void SmallGapAndSmallOverlap_AreAdjacent()
        {
            Assert.IsTrue(CanvasJoinMath.AreAdjacent(Rect(Vector3.zero), Rect(new Vector3(0.35f, 0f, 0f))), "Hueco de 0,05 m.");
            Assert.IsTrue(CanvasJoinMath.AreAdjacent(Rect(Vector3.zero), Rect(new Vector3(0.26f, 0f, 0f))), "Solape de 0,04 m.");
            Assert.IsTrue(CanvasJoinMath.AreAdjacent(Rect(Vector3.zero), Rect(new Vector3(0f, 0.24f, 0f))), "Apiladas con solape de 0,04 m.");
            Assert.IsTrue(CanvasJoinMath.AreAdjacent(Rect(Vector3.zero), Rect(new Vector3(0.3f, 0f, 0.08f))), "8 cm fuera del plano.");
        }

        [Test]
        public void GapOverLimit_OrOverlapOverLimit_AreNotAdjacent()
        {
            Assert.IsFalse(CanvasJoinMath.AreAdjacent(Rect(Vector3.zero), Rect(new Vector3(0.45f, 0f, 0f))), "Hueco de 0,15 m > 0,12.");
            Assert.IsFalse(CanvasJoinMath.AreAdjacent(Rect(Vector3.zero), Rect(new Vector3(0.18f, 0f, 0f))), "Solape de 0,12 m > 35 % del lado menor (0,105).");
            Assert.IsFalse(CanvasJoinMath.AreAdjacent(Rect(Vector3.zero), Rect(new Vector3(0.3f, 0f, 0.12f))), "12 cm fuera del plano > 0,10.");
        }

        [Test]
        public void SlightlyTiltedFace_StillAdjacent()
        {
            var tilted = new CanvasRect(new Vector3(0.3f, 0f, 0f), Vector3.right, Vector3.up, new Vector3(0f, 0.4f, 1f).normalized, 0.3f, 0.2f);
            Assert.IsTrue(CanvasJoinMath.AreAdjacent(Rect(Vector3.zero), tilted), "dot ~0,93 >= 0,8.");
        }

        // ---------------------------------------------------------------- costura y aporte de cada mitad

        private static Stroke Arc(float fromDeg, float toDeg, float stepDeg = 4f)
        {
            var stroke = new Stroke { Width = 0.012f, Ink = InkKind.Normal };
            int n = Mathf.Max(3, Mathf.CeilToInt((toDeg - fromDeg) / stepDeg) + 1);
            for (int i = 0; i < n; i++)
                stroke.AddPoint(new Vector2(0.5f, 0.5f) + GeometryUtils.VectorFromAngleDeg(Mathf.Lerp(fromDeg, toDeg, i / (float)(n - 1))) * 0.3f);
            return stroke;
        }

        [Test]
        public void HealSeam_ClosesARingWithSmallGaps()
        {
            var session = new DrawingSession();
            session.AddStroke(Arc(10f, 170f));   // mitad izquierda
            session.AddStroke(Arc(185f, 350f));  // mitad derecha, huecos de 15 y 20 grados (~90 % de cobertura)
            RingInfo ring = RingDetector.Detect(session.Scaled(Vector2.one));
            Assert.IsTrue(ring.Found && !ring.Complete);
            Assert.GreaterOrEqual(ring.Completeness, CanvasJoinMath.MinHealCompleteness);

            int added = CanvasJoinMath.HealSeam(session, ring, Vector2.one);
            Assert.AreEqual(2, added, "Un puente por hueco.");
            Assert.AreEqual(0.012f, session.Strokes[2].Width, 1e-6f, "El puente conserva el ancho.");
            Assert.AreEqual(InkKind.Normal, session.Strokes[2].Ink);
            Assert.IsTrue(RingDetector.Detect(session.Scaled(Vector2.one)).Complete, "Tras el puente el anillo queda cerrado.");
        }

        [Test]
        public void HealSeam_DoesNothing_WhenTooOpenOrAlreadyClosed()
        {
            var half = new DrawingSession();
            half.AddStroke(Arc(0f, 200f)); // 55 %: hay anillo pero mucho hueco
            RingInfo open = RingDetector.Detect(half.Scaled(Vector2.one));
            Assert.AreEqual(0, CanvasJoinMath.HealSeam(half, open, Vector2.one));
            Assert.AreEqual(1, half.Strokes.Count);

            var closed = new DrawingSession();
            closed.AddStroke(Arc(0f, 360f));
            RingInfo full = RingDetector.Detect(closed.Scaled(Vector2.one));
            Assert.AreEqual(0, CanvasJoinMath.HealSeam(closed, full, Vector2.one));
            Assert.AreEqual(0, CanvasJoinMath.HealSeam(closed, null, Vector2.one));
        }

        // Dos mitades como (grados de A, grados de B) de un anillo de 360: A de 180 hacia atras, B de 0 hacia adelante, sin tocarse.
        private static DrawingSession Halves(float degA, float degB, out RingInfo ring)
        {
            var s = new DrawingSession();
            s.AddStroke(Arc(180f - degA * 0.5f, 180f + degA * 0.5f)); // A (primer trazo)
            s.AddStroke(Arc(-degB * 0.5f, degB * 0.5f));              // B
            ring = RingDetector.Detect(s.Scaled(Vector2.one));
            return s;
        }

        [Test]
        public void EachHalfContributes_RequiresAQuarterOfTheCircleFromBoth()
        {
            var ok = Halves(162f, 198f, out RingInfo ringOk); // 45 % + 55 %
            Assert.IsTrue(CanvasJoinMath.EachHalfContributes(ok.Scaled(Vector2.one), 1, ringOk));

            var okSmall = Halves(144f, 180f, out RingInfo ringSmall); // 40 % + 50 %
            Assert.IsTrue(CanvasJoinMath.EachHalfContributes(okSmall.Scaled(Vector2.one), 1, ringSmall));

            var scribble = new DrawingSession();
            scribble.AddStroke(Arc(100f, 140f));   // ~11 % del circulo
            scribble.AddStroke(Arc(150f, 420f));   // 75 %
            RingInfo ring = RingDetector.Detect(scribble.Scaled(Vector2.one));
            Assert.IsTrue(ring.Found);
            Assert.IsFalse(CanvasJoinMath.EachHalfContributes(scribble.Scaled(Vector2.one), 1, ring), "Una mitad con casi nada no cuenta.");
        }

        [Test]
        public void FortyPlusForty_IsBelowHealThreshold_FortyPlusFifty_Heals()
        {
            var low = Halves(144f, 144f, out RingInfo ringLow); // 80 % total
            Assert.IsTrue(ringLow.Found);
            Assert.Less(ringLow.Completeness, CanvasJoinMath.MinHealCompleteness);
            Assert.AreEqual(0, CanvasJoinMath.HealSeam(low, ringLow, Vector2.one));

            var high = Halves(144f, 180f, out RingInfo ringHigh); // 90 % total
            Assert.GreaterOrEqual(ringHigh.Completeness, CanvasJoinMath.MinHealCompleteness);
            Assert.Greater(CanvasJoinMath.HealSeam(high, ringHigh, Vector2.one), 0);
            Assert.IsTrue(RingDetector.Detect(high.Scaled(Vector2.one)).Complete);
        }

        [Test]
        public void Merge_TolerantOfEmptySession()
        {
            var merged = CanvasJoinMath.Merge(Rect(Vector3.zero), Session(new Vector2(0.5f, 0.5f)), Rect(new Vector3(0.3f, 0f, 0f)), null, out _);
            Assert.AreEqual(1, merged.Strokes.Count);
        }
    }
}
