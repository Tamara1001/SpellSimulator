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
        private const InkKind N = InkKind.Normal;
        private const InkKind P = InkKind.Powerful;
        private const InkKind R = InkKind.Resistant;

        [Test]
        public void DefaultStock_HasTwoNormalCartridges()
        {
            var stock = new InkCartridgeStock();
            Assert.AreEqual(2, stock.CartridgeCount(N));
            Assert.AreEqual(2, stock.TotalCartridges);
            Assert.AreEqual(10, stock.TotalUses(N));
            Assert.AreEqual(N, stock.ActiveKind);
            Assert.IsFalse(stock.HasBelt);
        }

        [Test]
        public void WithoutBelt_OnlyOneKindAtATime()
        {
            var stock = new InkCartridgeStock();
            Assert.IsTrue(stock.CanCarry(N));
            Assert.IsFalse(stock.CanCarry(P));
            Assert.IsFalse(stock.TryAdd(P));
            Assert.IsTrue(stock.TryAdd(N));
        }

        [Test]
        public void WithoutBelt_EmptyStockAcceptsAnyKind()
        {
            var stock = new InkCartridgeStock();
            while (stock.TryUse(out _)) { }
            Assert.IsTrue(stock.CanCarry(P));
            Assert.IsTrue(stock.TryAdd(P));
            Assert.AreEqual(P, stock.ActiveKind);
            Assert.IsFalse(stock.TryAdd(R));
        }

        [Test]
        public void WithBelt_ThreeKinds()
        {
            var stock = new InkCartridgeStock();
            stock.SetBelt(true);
            Assert.IsTrue(stock.TryAdd(P));
            Assert.IsTrue(stock.TryAdd(R));
            Assert.AreEqual(4, stock.TotalCartridges);
            Assert.AreEqual(1, stock.CartridgeCount(R));
        }

        [Test]
        public void MaxPerKind()
        {
            var stock = new InkCartridgeStock();
            stock.SetBelt(true);
            Assert.AreEqual(InkCartridgeStock.DefaultMaxCartridges, stock.MaxCartridges);
            Assert.IsTrue(stock.TryAdd(N));
            Assert.IsTrue(stock.TryAdd(N));
            Assert.IsFalse(stock.TryAdd(N), "4 cartuchos por clase.");
            Assert.IsTrue(stock.TryAdd(P), "Otra clase tiene su propio slot.");
        }

        [Test]
        public void FiveUses_RemoveTheCartridge()
        {
            var stock = new InkCartridgeStock();
            for (int i = 0; i < InkCartridgeStock.UsesPerCartridge; i++) Assert.IsTrue(stock.TryUse(out _));
            Assert.AreEqual(1, stock.CartridgeCount(N));
        }

        [Test]
        public void Use_IsFifoWithinKind()
        {
            var stock = new InkCartridgeStock();
            stock.TryAdd(N, 3);
            stock.TryUse(out _);
            Assert.AreEqual(4, stock.UsesLeft(N));
            for (int i = 0; i < 4; i++) stock.TryUse(out _);
            Assert.AreEqual(2, stock.CartridgeCount(N), "El primero se agoto.");
            Assert.AreEqual(5, stock.UsesLeft(N));
            Assert.AreEqual(8, stock.TotalUses(N));
        }

        [Test]
        public void TryUse_SwitchesKindWhenActiveRunsOut()
        {
            var stock = new InkCartridgeStock();
            stock.SetBelt(true);
            stock.TryAdd(P);
            for (int i = 0; i < 10; i++)
            {
                Assert.IsTrue(stock.TryUse(out InkKind k));
                Assert.AreEqual(N, k);
            }
            Assert.AreEqual(P, stock.ActiveKind);
            Assert.IsTrue(stock.TryUse(out InkKind last));
            Assert.AreEqual(P, last);
            Assert.AreEqual(4, stock.UsesLeft(P));
        }

        [Test]
        public void CycleActive_SkipsEmptyKinds()
        {
            var stock = new InkCartridgeStock();
            stock.SetBelt(true);
            Assert.IsFalse(stock.CycleActive(), "Solo hay una clase.");
            stock.TryAdd(R);
            Assert.IsTrue(stock.CycleActive());
            Assert.AreEqual(R, stock.ActiveKind, "Salta la poderosa, que esta vacia.");
            Assert.IsTrue(stock.CycleActive());
            Assert.AreEqual(N, stock.ActiveKind);
        }

        [Test]
        public void CycleActive_WithoutBelt_DoesNothing()
        {
            var stock = new InkCartridgeStock();
            stock.SetBelt(true);
            stock.TryAdd(P);
            stock.SetBelt(false);
            Assert.IsFalse(stock.CycleActive());
            Assert.AreEqual(N, stock.ActiveKind);
        }

        [Test]
        public void Changed_FiresOnAddUseCycleAndBelt()
        {
            var stock = new InkCartridgeStock();
            int calls = 0;
            stock.Changed += () => calls++;
            stock.TryUse(out _);
            stock.TryAdd(N);
            stock.SetBelt(true);
            stock.TryAdd(P);
            stock.CycleActive();
            Assert.AreEqual(5, calls);
        }

        [Test]
        public void TryUse_WhenEmpty_ReturnsFalse()
        {
            var stock = new InkCartridgeStock();
            while (stock.TryUse(out _)) { }
            Assert.AreEqual(0, stock.TotalCartridges);
            Assert.IsFalse(stock.TryUse(out InkKind kind));
            Assert.AreEqual(N, kind);
        }
    }
}
