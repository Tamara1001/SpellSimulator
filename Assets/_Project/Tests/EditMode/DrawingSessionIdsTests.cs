using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Core;

namespace SpellSimulator.Tests
{
    public class DrawingSessionIdsTests
    {
        private static Stroke Line() => new Stroke { Width = 0.01f, Ink = InkKind.Powerful, Points = { new Vector2(0.1f, 0.2f), new Vector2(0.3f, 0.4f) } };

        [Test]
        public void AddStroke_AssignsIncreasingIds()
        {
            var s = new DrawingSession();
            var a = Line(); var b = Line(); var c = Line();
            s.AddStroke(a); s.AddStroke(b); s.AddStroke(c);
            Assert.AreEqual(1u, a.StrokeId);
            Assert.AreEqual(2u, b.StrokeId);
            Assert.AreEqual(3u, c.StrokeId);
            Assert.AreEqual(4u, s.NextStrokeId);
        }

        [Test]
        public void AddStroke_KeepsExistingId_AndNeverReuses()
        {
            var s = new DrawingSession();
            s.AddStroke(new Stroke { StrokeId = 10 });
            var next = Line();
            s.AddStroke(next);
            Assert.AreEqual(11u, next.StrokeId);
        }

        [Test]
        public void UndoThenAdd_DoesNotReuseId()
        {
            var s = new DrawingSession();
            s.AddStroke(Line());
            s.AddStroke(Line());
            s.UndoLastStroke();
            var c = Line();
            s.AddStroke(c);
            Assert.AreEqual(3u, c.StrokeId);
        }

        [Test]
        public void Revision_RisesOnAddUndoClear()
        {
            var s = new DrawingSession();
            int r0 = s.Revision;
            s.AddStroke(Line());
            int r1 = s.Revision;
            Assert.Greater(r1, r0);
            s.AddStroke(Line());
            Assert.Greater(s.Revision, r1);
            int r2 = s.Revision;
            Assert.IsTrue(s.UndoLastStroke());
            Assert.Greater(s.Revision, r2);
            int r3 = s.Revision;
            s.Clear();
            Assert.Greater(s.Revision, r3);
        }

        [Test]
        public void Revision_DoesNotRiseWhenNothingChanges()
        {
            var s = new DrawingSession();
            int r = s.Revision;
            Assert.IsFalse(s.UndoLastStroke());
            s.Clear();
            Assert.AreEqual(r, s.Revision);
        }

        [Test]
        public void MarkChanged_RaisesRevision()
        {
            var s = new DrawingSession();
            int r = s.Revision;
            s.MarkChanged();
            Assert.Greater(s.Revision, r);
        }

        [Test]
        public void SetOwnerForNewStrokes_AppliesOnlyToStrokesWithoutOwner()
        {
            var s = new DrawingSession();
            s.SetOwnerForNewStrokes(5);
            var a = Line();
            var b = Line(); b.OwnerId = 9;
            s.AddStroke(a); s.AddStroke(b);
            Assert.AreEqual(5, a.OwnerId);
            Assert.AreEqual(9, b.OwnerId);
        }

        [Test]
        public void Scaled_PreservesIdsOwnersInkAndWidth()
        {
            var s = new DrawingSession();
            s.SetOwnerForNewStrokes(4);
            s.AddStroke(Line());
            s.AddStroke(Line());
            DrawingSession copy = s.Scaled(new Vector2(2f, 0.5f));
            Assert.AreEqual(2, copy.Strokes.Count);
            for (int i = 0; i < 2; i++)
            {
                Assert.AreEqual(s.Strokes[i].StrokeId, copy.Strokes[i].StrokeId);
                Assert.AreEqual(4, copy.Strokes[i].OwnerId);
                Assert.AreEqual(InkKind.Powerful, copy.Strokes[i].Ink);
                Assert.AreEqual(0.01f, copy.Strokes[i].Width);
            }
            Assert.AreEqual(0.2f, copy.Strokes[0].Points[0].x, 1e-6f);
            Assert.AreEqual(s.NextStrokeId, copy.NextStrokeId);
        }

        [Test]
        public void AddingSharedStrokeToOtherSession_DoesNotChangeItsId()
        {
            var a = new DrawingSession();
            var stroke = Line();
            a.AddStroke(stroke);
            a.AddStroke(Line());
            var b = new DrawingSession();
            b.AddStroke(stroke);
            Assert.AreEqual(1u, stroke.StrokeId);
        }
    }
}
