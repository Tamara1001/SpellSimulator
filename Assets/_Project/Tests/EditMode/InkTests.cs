using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Core;
using SpellSimulator.Input.Tools;

namespace SpellSimulator.Tests
{
    public class InkTests
    {
        [Test]
        public void Consume_UntilEmpty()
        {
            var ink = new InkReservoir(10f, 10f);
            Assert.IsTrue(ink.TryConsume(6f));
            Assert.IsFalse(ink.TryConsume(6f), "No alcanza: se vacia y avisa.");
            Assert.IsTrue(ink.IsEmpty);
        }

        [Test]
        public void FillFrom_TransfersOnlyWhatFits()
        {
            var pen = new InkReservoir(100f, 30f);
            var well = new InkReservoir(500f, 50f);
            Assert.AreEqual(50f, pen.FillFrom(well));
            Assert.AreEqual(80f, pen.Level);
            Assert.AreEqual(0f, well.Level);

            well = new InkReservoir(500f, 500f);
            Assert.AreEqual(20f, pen.FillFrom(well));
            Assert.AreEqual(1f, pen.Fraction);
        }

        [Test]
        public void Level_IsClampedToCapacity()
        {
            Assert.AreEqual(10f, new InkReservoir(10f, 50f).Level);
        }
    }

    public class RagToolTests
    {
        [Test]
        public void ShortWipe_DoesNotClear_LongWipeDoes()
        {
            var go = new GameObject("canvas", typeof(MeshRenderer), typeof(MeshFilter));
            try
            {
                // DrawingCanvas necesita Awake (RenderTexture): se agrega en runtime de editor.
                var canvas = go.AddComponent<SpellSimulator.Canvas.DrawingCanvas>();
                canvas.Session.AddStroke(TestStrokes.Line(Vector2.zero, Vector2.one));
                var rag = new RagTool(wipeLengthToClear: 1f);

                rag.OnPointerDown(new Vector2(0.1f, 0.5f), canvas);
                rag.OnPointerDrag(new Vector2(0.5f, 0.5f), canvas);
                Assert.IsFalse(canvas.Session.IsEmpty);

                rag.OnPointerDrag(new Vector2(0.1f, 0.5f), canvas);
                rag.OnPointerDrag(new Vector2(0.5f, 0.5f), canvas);
                Assert.IsTrue(canvas.Session.IsEmpty);
                Assert.IsTrue(rag.ClearedThisUse);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
