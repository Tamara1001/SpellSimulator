using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Combat;
using SpellSimulator.Core;

namespace SpellSimulator.Tests
{
    public class DuelInkTests
    {
        [Test]
        public void ElSaldoNoAlcanzaYNoSeDescuentaNada()
        {
            var l = new InkLedger(100f, 3f);
            Assert.IsTrue(l.TryCharge(60f));
            Assert.IsFalse(l.TryCharge(50f));
            Assert.AreEqual(40f, l.Current, 1e-4f);
        }

        [Test]
        public void SeRecargaConElTiempoYTieneTope()
        {
            var l = new InkLedger(100f, 3f);
            l.TryCharge(90f);
            l.Tick(10f);
            Assert.AreEqual(40f, l.Current, 1e-4f);
            l.Tick(1000f);
            Assert.AreEqual(100f, l.Current, 1e-4f);
        }

        [Test]
        public void ElCostoCreceConLosMetrosYLaClaseDeTinta()
        {
            var normal = new Stroke();
            normal.AddPoint(new Vector2(0f, 0.5f)); normal.AddPoint(new Vector2(1f, 0.5f));
            var powerful = new Stroke { Ink = InkKind.Powerful };
            powerful.AddPoint(new Vector2(0f, 0.5f)); powerful.AddPoint(new Vector2(1f, 0.5f));
            Vector2 size = new Vector2(0.5f, 0.5f);
            float a = DuelInk.CanvasCost(new List<Stroke> { normal }, size, 8f);
            float b = DuelInk.CanvasCost(new List<Stroke> { normal, normal }, size, 8f);
            float c = DuelInk.CanvasCost(new List<Stroke> { powerful }, size, 8f);
            Assert.AreEqual(4f, a, 1e-3f); // 0,5 m x 8 / escala 1
            Assert.AreEqual(a * 2f, b, 1e-4f);
            Assert.AreNotEqual(a, c);
        }

        [Test]
        public void SinTrazosNoCuestaNada() => Assert.AreEqual(0f, DuelInk.CanvasCost(null, Vector2.one, 8f));
    }
}
