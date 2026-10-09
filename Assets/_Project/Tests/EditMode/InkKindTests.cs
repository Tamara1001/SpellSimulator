using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Core;

namespace SpellSimulator.Tests
{
    public class InkMathTests
    {
        private static Stroke Line(float length, InkKind kind)
            => new Stroke(new List<Vector2> { Vector2.zero, new Vector2(length, 0f) }) { Ink = kind };

        [Test]
        public void ReferenceCanvas_HasScaleOne()
        {
            Assert.AreEqual(1f, InkMath.CanvasScale(new Vector2(0.5f, 0.5f)), 1e-4f);
            Assert.AreEqual(1f, InkMath.CanvasScale(new Vector2(0.2f, 0.2f)), 1e-4f, "Mas chico que la referencia no encarece.");
        }

        [Test]
        public void BigCanvas_ScalesAndClamps()
        {
            Assert.AreEqual(4f, InkMath.CanvasScale(new Vector2(2f, 2f)), 1e-4f);
            Assert.AreEqual(InkMath.MaxCanvasScale, InkMath.CanvasScale(new Vector2(100f, 100f)), 1e-4f);
        }

        [Test]
        public void BiggerCanvas_CostsLess()
        {
            float small = InkMath.StrokeCost(1f, new Vector2(0.5f, 0.5f), 8f, InkKind.Normal);
            float big = InkMath.StrokeCost(1f, new Vector2(2f, 2f), 8f, InkKind.Normal);
            Assert.Less(big, small);
            Assert.AreEqual(small / 4f, big, 1e-4f);
        }

        [Test]
        public void PowerfulAndResistantInk_CostDouble()
        {
            var size = new Vector2(0.5f, 0.5f);
            float normal = InkMath.StrokeCost(1f, size, 8f, InkKind.Normal);
            Assert.AreEqual(2f * normal, InkMath.StrokeCost(1f, size, 8f, InkKind.Powerful), 1e-4f);
            Assert.AreEqual(2f * normal, InkMath.StrokeCost(1f, size, 8f, InkKind.Resistant), 1e-4f);
        }

        [Test]
        public void Cost_IsNeverNegative()
        {
            Assert.AreEqual(0f, InkMath.StrokeCost(-3f, new Vector2(0.5f, 0.5f), 8f, InkKind.Normal));
            Assert.AreEqual(0f, InkMath.StrokeCost(1f, Vector2.zero, -8f, InkKind.Normal));
        }

        [Test]
        public void KindInfo_PowerAndWaterproof()
        {
            Assert.AreEqual(2.5f, InkKindInfo.PowerMultiplier(InkKind.Powerful));
            Assert.AreEqual(1f, InkKindInfo.PowerMultiplier(InkKind.Resistant));
            Assert.IsTrue(InkKindInfo.IsWaterproof(InkKind.Resistant));
            Assert.IsFalse(InkKindInfo.IsWaterproof(InkKind.Powerful));
        }

        [Test]
        public void Dominant_EmptyAndTie_AreNormal()
        {
            Assert.AreEqual(InkKind.Normal, InkMath.Dominant(new List<Stroke>()));
            Assert.AreEqual(InkKind.Normal, InkMath.Dominant(new List<Stroke> { Line(1f, InkKind.Powerful), Line(1f, InkKind.Resistant) }));
        }

        [Test]
        public void Dominant_LongestTotalWins()
        {
            var strokes = new List<Stroke> { Line(0.3f, InkKind.Normal), Line(0.2f, InkKind.Powerful), Line(0.2f, InkKind.Powerful) };
            Assert.AreEqual(InkKind.Powerful, InkMath.Dominant(strokes));
        }
    }

    public class InkCartridgeStockTests
    {
        [Test]
        public void DefaultStock_HasTwoNormalCartridges()
        {
            var stock = new InkCartridgeStock();
            Assert.AreEqual(2, stock.CartridgeCount);
            Assert.AreEqual(10, stock.TotalUses);
            Assert.AreEqual(InkKind.Normal, stock.CurrentKind);
        }

        [Test]
        public void TryAdd_StopsAtMax()
        {
            var stock = new InkCartridgeStock();
            Assert.AreEqual(InkCartridgeStock.DefaultMaxCartridges, stock.MaxCartridges);
            Assert.IsTrue(stock.TryAdd(InkKind.Powerful));
            Assert.IsTrue(stock.TryAdd(InkKind.Resistant));
            Assert.IsFalse(stock.TryAdd(InkKind.Normal), "Con 4 cartuchos ya esta lleno.");
            stock.SetMax(5);
            Assert.IsTrue(stock.TryAdd(InkKind.Normal));
        }

        [Test]
        public void FiveUses_RemoveTheCartridge()
        {
            var stock = new InkCartridgeStock();
            for (int i = 0; i < InkCartridgeStock.UsesPerCartridge; i++) Assert.IsTrue(stock.TryUse(out _));
            Assert.AreEqual(1, stock.CartridgeCount);
        }

        [Test]
        public void Use_IsFifoAndCurrentKindFollows()
        {
            var stock = new InkCartridgeStock();
            stock.TryAdd(InkKind.Powerful);
            for (int i = 0; i < 10; i++)
            {
                Assert.IsTrue(stock.TryUse(out InkKind k));
                Assert.AreEqual(InkKind.Normal, k);
            }
            Assert.AreEqual(InkKind.Powerful, stock.CurrentKind);
            Assert.AreEqual(5, stock.CurrentUses);
            Assert.IsTrue(stock.TryUse(out InkKind last));
            Assert.AreEqual(InkKind.Powerful, last);
            Assert.AreEqual(4, stock.CurrentUses);
        }

        [Test]
        public void Changed_FiresOnAddAndUse()
        {
            var stock = new InkCartridgeStock();
            int calls = 0;
            stock.Changed += () => calls++;
            stock.TryUse(out _);
            stock.TryAdd(InkKind.Normal);
            Assert.AreEqual(2, calls);
        }

        [Test]
        public void TryUse_WhenEmpty_ReturnsFalse()
        {
            var stock = new InkCartridgeStock();
            while (stock.TryUse(out _)) { }
            Assert.AreEqual(0, stock.CartridgeCount);
            Assert.IsFalse(stock.TryUse(out InkKind kind));
            Assert.AreEqual(InkKind.Normal, kind);
            Assert.AreEqual(InkKind.Normal, stock.CurrentKind);
        }
    }
}
