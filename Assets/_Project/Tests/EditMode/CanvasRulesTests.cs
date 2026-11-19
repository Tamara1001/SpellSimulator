using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Canvas;

namespace SpellSimulator.Tests
{
    public class CanvasRulesTests
    {
        [Test]
        public void Notebook_M_Is_The_Classic_Size()
        {
            Vector2 d = CanvasRules.Dimensions(CanvasSpec.Notebook(CanvasSize.M));
            Assert.AreEqual(0.24f, d.x, 1e-4f);
            Assert.AreEqual(0.32f, d.y, 1e-4f);
        }

        [Test]
        public void Notebook_S_Is_Round_And_L_Is_Rect()
        {
            Assert.AreEqual(CanvasShape.Round, CanvasSpec.Notebook(CanvasSize.S).shape);
            Assert.AreEqual(CanvasShape.Rect, CanvasSpec.Notebook(CanvasSize.L).shape);
            Vector2 s = CanvasRules.Dimensions(CanvasSpec.Notebook(CanvasSize.S));
            Assert.AreEqual(s.x, s.y, 1e-5f);
        }

        [Test]
        public void Pages_Are_Clamped_And_Sheets_Have_One()
        {
            Assert.AreEqual(CanvasRules.MaxPages, CanvasSpec.Notebook(CanvasSize.M, 20).pages);
            Assert.AreEqual(1, CanvasSpec.Notebook(CanvasSize.M, 0).pages);
            Assert.AreEqual(1, CanvasRules.Sanitize(new CanvasSpec(CanvasKind.Sheet, CanvasSize.M, CanvasShape.Square, 5)).pages);
        }

        [Test]
        public void Sheet_Cannot_Be_Round()
        {
            Assert.AreEqual(CanvasShape.Square, CanvasRules.Sanitize(new CanvasSpec(CanvasKind.Sheet, CanvasSize.S, CanvasShape.Round)).shape);
        }

        [Test]
        public void Sheet_Sizes_Grow_And_Are_Positive()
        {
            foreach (CanvasShape shape in new[] { CanvasShape.Square, CanvasShape.Rect })
            {
                float last = 0f;
                for (int size = 0; size < 3; size++)
                {
                    Vector2 d = CanvasRules.Dimensions(new CanvasSpec(CanvasKind.Sheet, (CanvasSize)size, shape));
                    Assert.Greater(d.x, 0f);
                    Assert.Greater(d.y, last);
                    last = d.y;
                }
            }
        }

        [Test]
        public void Nearest_Maps_Old_Sizes()
        {
            Assert.AreEqual(CanvasSize.M, CanvasRules.Nearest(CanvasKind.Notebook, 0.24f, 0.32f).size);
            Assert.AreEqual(CanvasSize.L, CanvasRules.Nearest(CanvasKind.Sheet, 3f, 2.2f).size);
            Assert.AreEqual(CanvasShape.Square, CanvasRules.Nearest(CanvasKind.Sheet, 0.45f, 0.45f).shape);
        }

        [Test]
        public void Easel_Reverse_Flips_The_Ink_Sign()
        {
            Assert.AreEqual(1f, CanvasRules.EffectiveInkSign(1f, false));
            Assert.AreEqual(-1f, CanvasRules.EffectiveInkSign(1f, true));
            Assert.AreEqual(1f, CanvasRules.EffectiveInkSign(-1f, true));
        }

        [Test]
        public void Slot_Snap_Picks_Nearest_Free_Slot_Or_Swaps()
        {
            var slots = new[] { new Vector3(0, 0, 0), new Vector3(0.5f, 0, 0) };
            var occ = new[] { false, true };
            Assert.AreEqual(0, CanvasRules.NearestSlot(new Vector3(0.45f, 0, 0), slots, occ, 2, false));
            Assert.AreEqual(1, CanvasRules.NearestSlot(new Vector3(0.45f, 0, 0), slots, occ, 2, true));
            Assert.AreEqual(-1, CanvasRules.NearestSlot(new Vector3(5f, 0, 0), slots, occ, 2, true));
            Assert.IsTrue(CanvasRules.InSnapRange(Vector3.zero, new Vector3(0.5f, 0, 0)));
        }

        [Test]
        public void Pages_Are_Clamped()
        {
            Assert.AreEqual(0, CanvasRules.ClampPage(-3, 8));
            Assert.AreEqual(7, CanvasRules.ClampPage(30, 8));
        }
    }
}
