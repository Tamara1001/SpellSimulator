using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Core;

namespace SpellSimulator.Tests
{
    public class StrokeCodecTests
    {
        private const float MaxError = 1f / 2048f;

        private static Stroke Circle(int points, float radius = 0.3f)
        {
            var s = new Stroke { Width = 0.012f, Ink = InkKind.Resistant, OwnerId = 3, StrokeId = 7 };
            for (int i = 0; i < points; i++)
            {
                float a = i * 2f * Mathf.PI / points;
                s.AddPoint(new Vector2(0.5f + radius * Mathf.Cos(a), 0.5f + radius * Mathf.Sin(a)));
            }
            return s;
        }

        private static void AssertSamePoints(Stroke a, Stroke b)
        {
            Assert.IsNotNull(b);
            Assert.AreEqual(a.Points.Count, b.Points.Count);
            for (int i = 0; i < a.Points.Count; i++)
            {
                Assert.Less(Mathf.Abs(a.Points[i].x - b.Points[i].x), MaxError, "x " + i);
                Assert.Less(Mathf.Abs(a.Points[i].y - b.Points[i].y), MaxError, "y " + i);
            }
        }

        [Test]
        public void RoundTrip_Circle200_ErrorBelowHalfThousandth()
        {
            Stroke s = Circle(200);
            AssertSamePoints(s, StrokeCodec.Decode(StrokeCodec.Encode(s)));
        }

        [Test]
        public void Size_Circle200_UnderBudget()
        {
            Stroke s = Circle(200);
            int size = StrokeCodec.EncodedSize(s);
            Assert.Less(size, 450);
            Assert.AreEqual(size, StrokeCodec.Encode(s).Length);
        }

        [Test]
        public void RoundTrip_PointsOutsideUnitSquare_StayWithinMargin()
        {
            var s = new Stroke();
            s.AddPoint(new Vector2(-0.05f, 1.05f));
            s.AddPoint(new Vector2(0.5f, 0.5f));
            s.AddPoint(new Vector2(1.06f, -0.06f));
            AssertSamePoints(s, StrokeCodec.Decode(StrokeCodec.Encode(s)));
        }

        [Test]
        public void RoundTrip_PointsFarOutside_ClampToRange()
        {
            var s = new Stroke();
            s.AddPoint(new Vector2(-5f, 9f));
            Stroke d = StrokeCodec.Decode(StrokeCodec.Encode(s));
            Assert.AreEqual(StrokeCodec.UvMin, d.Points[0].x, 1e-4f);
            Assert.AreEqual(StrokeCodec.UvMin + StrokeCodec.UvRange, d.Points[0].y, 1e-4f);
        }

        [Test]
        public void RoundTrip_SinglePoint()
        {
            var s = new Stroke();
            s.AddPoint(new Vector2(0.25f, 0.75f));
            AssertSamePoints(s, StrokeCodec.Decode(StrokeCodec.Encode(s)));
        }

        [Test]
        public void RoundTrip_EmptyStroke()
        {
            Stroke d = StrokeCodec.Decode(StrokeCodec.Encode(new Stroke { OwnerId = 2, StrokeId = 5 }));
            Assert.IsNotNull(d);
            Assert.AreEqual(0, d.Points.Count);
            Assert.AreEqual(5u, d.StrokeId);
        }

        [Test]
        public void RoundTrip_2000Points_WithJumps()
        {
            var rng = new System.Random(42);
            var s = new Stroke();
            for (int i = 0; i < 2000; i++)
                s.AddPoint(i % 97 == 0 ? new Vector2((float)rng.NextDouble(), (float)rng.NextDouble())
                                       : new Vector2(0.5f + 0.002f * (i % 50), 0.5f - 0.0007f * (i % 31)));
            AssertSamePoints(s, StrokeCodec.Decode(StrokeCodec.Encode(s)));
        }

        [Test]
        public void RoundTrip_LargeIds_Varint()
        {
            var s = Circle(10);
            s.OwnerId = 1234567;
            s.StrokeId = 4000000000u;
            Stroke d = StrokeCodec.Decode(StrokeCodec.Encode(s));
            Assert.AreEqual(1234567, d.OwnerId);
            Assert.AreEqual(4000000000u, d.StrokeId);
        }

        [Test]
        public void RoundTrip_NegativeOwner()
        {
            var s = Circle(5);
            s.OwnerId = -4;
            Assert.AreEqual(-4, StrokeCodec.Decode(StrokeCodec.Encode(s)).OwnerId);
        }

        [Test]
        public void RoundTrip_InkAndWidthPreserved()
        {
            foreach (InkKind ink in new[] { InkKind.Normal, InkKind.Powerful, InkKind.Resistant })
            {
                foreach (float width in new[] { 0f, 0.002f, 0.012f, 0.04f, 0.2f })
                {
                    var s = Circle(8);
                    s.Ink = ink;
                    s.Width = width;
                    Stroke d = StrokeCodec.Decode(StrokeCodec.Encode(s));
                    Assert.AreEqual(ink, d.Ink);
                    if (width == 0f) Assert.AreEqual(0f, d.Width);
                    else Assert.AreEqual(width, d.Width, width * 0.03f);
                }
            }
        }

        [Test]
        public void Decode_TruncatedData_ReturnsNullWithoutThrowing()
        {
            byte[] data = StrokeCodec.Encode(Circle(60));
            for (int len = 0; len < data.Length; len++)
            {
                var cut = new byte[len];
                System.Array.Copy(data, cut, len);
                Assert.DoesNotThrow(() => StrokeCodec.Decode(cut));
                Assert.IsNull(StrokeCodec.Decode(cut), "len " + len);
            }
        }

        [Test]
        public void Decode_NullAndGarbage_ReturnNull()
        {
            Assert.IsNull(StrokeCodec.Decode(null));
            Assert.IsNull(StrokeCodec.Decode(new byte[0]));
            Assert.IsNull(StrokeCodec.Decode(new byte[] { 9, 9, 9, 9 }));
            Assert.IsNull(StrokeCodec.Decode(StrokeCodec.Encode(Circle(5)), 100));
            Assert.IsNull(StrokeCodec.DecodeSession(new byte[] { 1, 200, 3 }));
        }

        [Test]
        public void Decode_WithOffset_ReadsAtOffset()
        {
            byte[] one = StrokeCodec.Encode(Circle(20));
            var padded = new byte[one.Length + 5];
            System.Array.Copy(one, 0, padded, 5, one.Length);
            AssertSamePoints(Circle(20), StrokeCodec.Decode(padded, 5));
        }

        [Test]
        public void Session_RoundTrip_SeveralStrokes()
        {
            var session = new DrawingSession();
            session.AddStroke(Circle(200, 0.3f));
            session.AddStroke(new Stroke { Ink = InkKind.Powerful, OwnerId = 2, Width = 0.03f, Points = new List<Vector2> { new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f) } });
            session.AddStroke(new Stroke());

            DrawingSession d = StrokeCodec.DecodeSession(StrokeCodec.EncodeSession(session));
            Assert.IsNotNull(d);
            Assert.AreEqual(3, d.Strokes.Count);
            for (int i = 0; i < 3; i++)
            {
                AssertSamePoints(session.Strokes[i], d.Strokes[i]);
                Assert.AreEqual(session.Strokes[i].StrokeId, d.Strokes[i].StrokeId);
                Assert.AreEqual(session.Strokes[i].OwnerId, d.Strokes[i].OwnerId);
                Assert.AreEqual(session.Strokes[i].Ink, d.Strokes[i].Ink);
            }
        }

        [Test]
        public void Session_EmptyAndTruncated()
        {
            DrawingSession empty = StrokeCodec.DecodeSession(StrokeCodec.EncodeSession(new DrawingSession()));
            Assert.IsNotNull(empty);
            Assert.IsTrue(empty.IsEmpty);

            var session = new DrawingSession();
            session.AddStroke(Circle(50));
            byte[] data = StrokeCodec.EncodeSession(session);
            for (int len = 0; len < data.Length; len++)
            {
                var cut = new byte[len];
                System.Array.Copy(data, cut, len);
                Assert.DoesNotThrow(() => StrokeCodec.DecodeSession(cut));
                Assert.IsNull(StrokeCodec.DecodeSession(cut), "len " + len);
            }
        }
    }
}
