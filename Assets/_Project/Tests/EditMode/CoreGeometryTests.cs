using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Core;
using SpellSimulator.Inference;

namespace SpellSimulator.Tests
{
    public class RingDetectorTests
    {
        [Test]
        public void ClosedCircle_IsDetectedAsRing()
        {
            var ring = TestStrokes.Circle(new Vector2(0.5f, 0.5f), 0.3f);
            var result = RingDetector.Detect(TestStrokes.Session(ring));

            Assert.IsTrue(result.Found);
            Assert.AreSame(ring, result.Stroke);
            Assert.That(result.Center.x, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(result.Radius, Is.EqualTo(0.3f).Within(0.01f));
        }

        [Test]
        public void StraightLine_IsNotARing()
        {
            var line = TestStrokes.Line(new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.5f), 30);
            Assert.IsFalse(RingDetector.Detect(TestStrokes.Session(line)).Found);
        }

        [Test]
        public void HalfCircle_IsNotARing()
        {
            var arc = TestStrokes.Circle(new Vector2(0.5f, 0.5f), 0.3f, 48, closureGapDegrees: 180f);
            Assert.IsFalse(RingDetector.Detect(TestStrokes.Session(arc)).Found);
        }

        [Test]
        public void PicksLargestRing_WhenSeveralClosedStrokes()
        {
            var small = TestStrokes.Circle(new Vector2(0.5f, 0.5f), 0.08f);
            var big = TestStrokes.Circle(new Vector2(0.5f, 0.5f), 0.35f);
            var result = RingDetector.Detect(TestStrokes.Session(small, big));

            Assert.AreSame(big, result.Stroke);
        }
    }

    public class StrokeGrouperTests
    {
        [Test]
        public void SplitsCenterSigil_FromOuterSigns()
        {
            Vector2 c = new Vector2(0.5f, 0.5f);
            var ring = TestStrokes.Circle(c, 0.4f);
            var sigil = TestStrokes.Line(c + new Vector2(-0.05f, 0f), c + new Vector2(0.05f, 0f));
            var signNorth = TestStrokes.Line(c + new Vector2(-0.03f, 0.32f), c + new Vector2(0.03f, 0.32f));
            var signSouth = TestStrokes.Line(c + new Vector2(-0.03f, -0.32f), c + new Vector2(0.03f, -0.32f));

            var session = TestStrokes.Session(ring, sigil, signNorth, signSouth);
            var grouped = StrokeGrouper.Group(session, RingDetector.Detect(session));

            Assert.AreEqual(1, grouped.Sigil.Strokes.Count);
            Assert.AreSame(sigil, grouped.Sigil.Strokes[0]);
            Assert.AreEqual(2, grouped.Signs.Count);
        }

        [Test]
        public void MergesNearbyOuterStrokes_IntoOneSign()
        {
            Vector2 c = new Vector2(0.5f, 0.5f);
            var ring = TestStrokes.Circle(c, 0.4f);
            var a = TestStrokes.Line(c + new Vector2(-0.03f, 0.30f), c + new Vector2(0.03f, 0.30f));
            var b = TestStrokes.Line(c + new Vector2(-0.03f, 0.33f), c + new Vector2(0.03f, 0.33f));

            var session = TestStrokes.Session(ring, a, b);
            var grouped = StrokeGrouper.Group(session, RingDetector.Detect(session));

            Assert.AreEqual(1, grouped.Signs.Count);
            Assert.AreEqual(2, grouped.Signs[0].Strokes.Count);
        }

        [Test]
        public void WithoutRing_EverythingIsSigil()
        {
            var a = TestStrokes.Line(Vector2.zero, Vector2.one);
            var b = TestStrokes.Line(Vector2.up, Vector2.right);
            var session = TestStrokes.Session(a, b);

            var grouped = StrokeGrouper.Group(session, new RingInfo { Found = false });

            Assert.AreEqual(2, grouped.Sigil.Strokes.Count);
            Assert.IsEmpty(grouped.Signs);
        }
    }

    public class StrokeCleanerTests
    {
        [Test]
        public void DoesNotMutateOriginalSession()
        {
            var raw = TestStrokes.Session(TestStrokes.Circle(new Vector2(0.5f, 0.5f), 0.3f, 200));
            int before = raw.Strokes[0].Points.Count;

            StrokeCleaner.CleanSession(raw, 0.005f, 3);

            Assert.AreEqual(before, raw.Strokes[0].Points.Count);
        }

        [Test]
        public void CollinearPoints_AreSimplified()
        {
            var raw = TestStrokes.Session(TestStrokes.Line(Vector2.zero, Vector2.one, 100));
            var cleaned = StrokeCleaner.CleanSession(raw, 0.005f, 1);

            Assert.Less(cleaned.Strokes[0].Points.Count, 10);
        }

        [Test]
        public void ShortStrokes_ArePassedThrough()
        {
            var dot = new Stroke();
            dot.AddPoint(new Vector2(0.2f, 0.2f));
            var cleaned = StrokeCleaner.CleanSession(TestStrokes.Session(dot), 0.005f, 3);

            Assert.AreEqual(1, cleaned.Strokes[0].Points.Count);
        }
    }

    public class GeometryUtilsTests
    {
        [Test]
        public void RotatePoint_90Degrees()
        {
            Vector2 r = GeometryUtils.RotatePointAroundPivot(Vector2.right, Vector2.zero, 90f);
            Assert.That(r.x, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(r.y, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void SessionCenter_IsBoundingBoxCenter()
        {
            var s = TestStrokes.Session(TestStrokes.Line(new Vector2(0.2f, 0.4f), new Vector2(0.6f, 0.8f)));
            Vector2 c = GeometryUtils.GetSessionCenter(s);
            Assert.That(c.x, Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(c.y, Is.EqualTo(0.6f).Within(1e-5f));
        }
    }

    public class TensorRasterizerTests
    {
        [Test]
        public void EmptySession_IsAllBackground()
        {
            float[] t = TensorRasterizer.Rasterize(new DrawingSession(), 96, 1f, 0.1f);
            Assert.AreEqual(96 * 96, t.Length);
            Assert.AreEqual(0, TestStrokes.CountInk(t));
        }

        [Test]
        public void Output_IsScaleInvariant()
        {
            var small = TestStrokes.Session(TestStrokes.Circle(new Vector2(0.5f, 0.5f), 0.05f));
            var big = TestStrokes.Session(TestStrokes.Circle(new Vector2(0.3f, 0.6f), 0.4f));

            int inkSmall = TestStrokes.CountInk(TensorRasterizer.Rasterize(small, 96, 1f, 0.1f));
            int inkBig = TestStrokes.CountInk(TensorRasterizer.Rasterize(big, 96, 1f, 0.1f));

            Assert.Greater(inkSmall, 0);
            Assert.That(inkSmall, Is.EqualTo(inkBig).Within(inkBig / 10));
        }

        [Test]
        public void Values_AreMinusOneOrOne()
        {
            var s = TestStrokes.Session(TestStrokes.Line(Vector2.zero, Vector2.one));
            foreach (float v in TensorRasterizer.Rasterize(s, 96, 1f, 0.1f))
                Assert.IsTrue(v == -1f || v == 1f);
        }
    }
}
