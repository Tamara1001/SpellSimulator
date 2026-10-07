using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Core;
using SpellSimulator.Parsing;

namespace SpellSimulator.Tests
{
    public class RingDetectorTests
    {
        private static readonly Vector2 C = new Vector2(0.5f, 0.5f);

        [Test]
        public void ClosedCircle_IsCompleteRing()
        {
            var ring = TestStrokes.Circle(C, 0.3f);
            var result = RingDetector.Detect(TestStrokes.Session(ring));

            Assert.IsTrue(result.Found);
            Assert.IsTrue(result.Complete);
            Assert.AreEqual(1f, result.Completeness);
            Assert.IsTrue(result.Contains(ring));
            Assert.That(result.Center.x, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(result.Radius, Is.EqualTo(0.3f).Within(0.01f));
            Assert.Greater(result.Neatness, 0.8f);
        }

        [Test]
        public void CircleWithGap_IsFoundButOpen()
        {
            var arc = TestStrokes.Circle(C, 0.3f, 48, closureGapDegrees: 40f);
            var result = RingDetector.Detect(TestStrokes.Session(arc));

            Assert.IsTrue(result.Found, "Un anillo con hueco sigue siendo un anillo (preparado).");
            Assert.IsFalse(result.Complete);
            Assert.That(result.Completeness, Is.EqualTo(320f / 360f).Within(0.03f));
        }

        [Test]
        public void TwoArcs_FormOneClosedRing()
        {
            var first = TestStrokes.Arc(C, 0.3f, 0f, 190f);
            var second = TestStrokes.Arc(C, 0.3f, 185f, 365f);
            var result = RingDetector.Detect(TestStrokes.Session(first, second));

            Assert.IsTrue(result.Complete);
            Assert.AreEqual(2, result.Strokes.Count);
        }

        [Test]
        public void ArcCenter_IsFittedNotBoundingBox()
        {
            var arc = TestStrokes.Circle(C, 0.3f, 48, closureGapDegrees: 120f);
            var result = RingDetector.Detect(TestStrokes.Session(arc));

            Assert.That(Vector2.Distance(result.Center, C), Is.LessThan(0.01f));
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
            var arc = TestStrokes.Circle(C, 0.3f, 48, closureGapDegrees: 180f);
            Assert.IsFalse(RingDetector.Detect(TestStrokes.Session(arc)).Found);
        }

        [Test]
        public void PicksLargestRing()
        {
            var small = TestStrokes.Circle(C, 0.08f);
            var big = TestStrokes.Circle(C, 0.35f);
            var result = RingDetector.Detect(TestStrokes.Session(small, big));

            Assert.IsTrue(result.Contains(big));
            Assert.IsFalse(result.Contains(small));
        }
    }

    public class StrokeGrouperTests
    {
        private static readonly Vector2 C = new Vector2(0.5f, 0.5f);

        [Test]
        public void SplitsCenterSigil_FromOuterSigns()
        {
            var ring = TestStrokes.Circle(C, 0.4f);
            var sigil = TestStrokes.Line(C + new Vector2(-0.05f, 0f), C + new Vector2(0.05f, 0f));
            var north = TestStrokes.Line(C + new Vector2(-0.03f, 0.32f), C + new Vector2(0.03f, 0.32f));
            var south = TestStrokes.Line(C + new Vector2(-0.03f, -0.32f), C + new Vector2(0.03f, -0.32f));

            var session = TestStrokes.Session(ring, sigil, north, south);
            var groups = StrokeGrouper.Group(session, RingDetector.Detect(session));

            Assert.AreEqual(1, groups.Sigil.Strokes.Count);
            Assert.AreSame(sigil, groups.Sigil.Strokes[0]);
            Assert.AreEqual(2, groups.Signs.Count);
        }

        [Test]
        public void MergesNearbyOuterStrokes_IntoOneSign()
        {
            var ring = TestStrokes.Circle(C, 0.4f);
            var a = TestStrokes.Line(C + new Vector2(-0.03f, 0.30f), C + new Vector2(0.03f, 0.30f));
            var b = TestStrokes.Line(C + new Vector2(-0.03f, 0.33f), C + new Vector2(0.03f, 0.33f));

            var session = TestStrokes.Session(ring, a, b);
            var groups = StrokeGrouper.Group(session, RingDetector.Detect(session));

            Assert.AreEqual(1, groups.Signs.Count);
            Assert.AreEqual(2, groups.Signs[0].Strokes.Count);
        }

        [Test]
        public void Clustering_IsTransitive()
        {
            // A toca B y B toca C, pero A no toca C: los tres deben quedar en un mismo signo.
            var ring = TestStrokes.Circle(C, 0.4f);
            var a = TestStrokes.Line(C + new Vector2(-0.10f, 0.30f), C + new Vector2(-0.06f, 0.30f));
            var c = TestStrokes.Line(C + new Vector2(0.06f, 0.30f), C + new Vector2(0.10f, 0.30f));
            var b = TestStrokes.Line(C + new Vector2(-0.03f, 0.30f), C + new Vector2(0.03f, 0.30f));

            var session = TestStrokes.Session(ring, a, c, b);
            var groups = StrokeGrouper.Group(session, RingDetector.Detect(session));

            Assert.AreEqual(1, groups.Signs.Count);
        }

        [Test]
        public void WithoutRing_EverythingIsSigil()
        {
            var session = TestStrokes.Session(TestStrokes.Line(Vector2.zero, Vector2.one), TestStrokes.Line(Vector2.up, Vector2.right));
            var groups = StrokeGrouper.Group(session, RingInfo.NotFound());

            Assert.AreEqual(2, groups.Sigil.Strokes.Count);
            Assert.IsEmpty(groups.Signs);
        }
    }

    public class SymbolCandidateTests
    {
        [Test]
        public void Metrics_AreRelativeToRing()
        {
            Vector2 c = new Vector2(0.5f, 0.5f);
            // Signo horizontal de 0.08 de ancho, abajo del centro a 0.3 (radio del anillo 0.4).
            var sign = TestStrokes.Session(TestStrokes.Line(c + new Vector2(-0.04f, -0.3f), c + new Vector2(0.04f, -0.3f)));
            var candidate = SymbolCandidate.Build(sign, c, 0.4f);

            Assert.That(candidate.RadiusNorm, Is.EqualTo(0.75f).Within(1e-3f));
            Assert.That(candidate.AngleDeg, Is.EqualTo(270f).Within(1e-2f));
            Assert.AreEqual(RingLayer.Outer, candidate.Layer);
            Assert.That(candidate.SizeNorm, Is.EqualTo(0.1f).Within(1e-3f));
            Assert.That(candidate.DirectedOrientationDeg, Is.EqualTo(0f).Within(1e-2f));
            Assert.AreEqual(1f, candidate.ElongationNorm);
        }

        [Test]
        public void Layers_FollowReferenceThresholds()
        {
            Assert.AreEqual(RingLayer.Center, RingLayers.FromRadius(0.2f, out _));
            Assert.AreEqual(RingLayer.Middle, RingLayers.FromRadius(0.5f, out _));
            Assert.AreEqual(RingLayer.Outer, RingLayers.FromRadius(0.8f, out _));
            Assert.AreEqual(RingLayer.RingBoundary, RingLayers.FromRadius(1.0f, out _));
            Assert.AreEqual(RingLayer.Outside, RingLayers.FromRadius(1.5f, out _));
            RingLayers.FromRadius(0.33f, out bool near);
            Assert.IsTrue(near);
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
        public void ShortStrokes_AreCopiedNotShared()
        {
            var dot = new Stroke();
            dot.AddPoint(new Vector2(0.2f, 0.2f));
            var cleaned = StrokeCleaner.CleanSession(TestStrokes.Session(dot), 0.005f, 3);

            Assert.AreEqual(1, cleaned.Strokes[0].Points.Count);
            Assert.AreNotSame(dot, cleaned.Strokes[0]);
        }

        [Test]
        public void CollinearPoints_AreSimplified()
        {
            var cleaned = StrokeCleaner.CleanSession(TestStrokes.Session(TestStrokes.Line(Vector2.zero, Vector2.one, 100)), 0.005f, 1);
            Assert.Less(cleaned.Strokes[0].Points.Count, 10);
        }

        [Test]
        public void SelfCrossingStroke_ProducesNoNaN()
        {
            // Ida y vuelta sobre el mismo segmento: antes generaba divisiones por cero.
            var stroke = new Stroke();
            foreach (var p in new[] { Vector2.zero, Vector2.one, new Vector2(0.5f, 0.2f), Vector2.zero, Vector2.one })
                stroke.AddPoint(p);
            var cleaned = StrokeCleaner.CleanSession(TestStrokes.Session(stroke), 0.005f, 3);

            foreach (var p in cleaned.Strokes[0].Points)
                Assert.IsFalse(float.IsNaN(p.x) || float.IsNaN(p.y));
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
        public void AngleConvention_IsMathYUp()
        {
            Assert.That(GeometryUtils.AngleDeg(Vector2.up), Is.EqualTo(90f).Within(1e-4f));
            Assert.That(GeometryUtils.AngleDeg(Vector2.down), Is.EqualTo(270f).Within(1e-4f));
            Assert.That(GeometryUtils.VectorFromAngleDeg(180f).x, Is.EqualTo(-1f).Within(1e-5f));
        }

        [Test]
        public void DominantAxis_OfHorizontalLine_IsZeroOr180()
        {
            var s = TestStrokes.Session(TestStrokes.Line(new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.5f)));
            float axis = GeometryUtils.DominantAxisOrientationDeg(s.Strokes) % 180f;
            Assert.That(axis, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void ScaledSession_IsDeepCopy()
        {
            var s = TestStrokes.Session(TestStrokes.Line(Vector2.zero, Vector2.one));
            var scaled = s.Scaled(new Vector2(2f, 1f));
            Assert.AreEqual(2f, scaled.Strokes[0].Points[scaled.Strokes[0].Count - 1].x);
            Assert.AreEqual(1f, s.Strokes[0].Points[s.Strokes[0].Count - 1].x);
        }
    }
}
