using NUnit.Framework;
using UnityEngine;
using SpellSimulator.World;

namespace SpellSimulator.Tests
{
    public class HoldableTests
    {
        private GameObject go;

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        [Test]
        public void PorDefecto_NadieLoSostiene()
        {
            go = new GameObject("caja");
            var h = Holdable.GetOrAdd(go);
            Assert.AreEqual(Holdable.NoOwner, h.HeldByOwnerId);
            Assert.IsFalse(h.IsHeld);
            Assert.AreSame(h, Holdable.GetOrAdd(go), "GetOrAdd no duplica el componente");
        }

        [Test]
        public void SetHeldBy_EmiteSoloAlCambiar()
        {
            go = new GameObject("caja");
            var h = Holdable.GetOrAdd(go);
            int calls = 0, last = -99;
            h.HeldChanged += id => { calls++; last = id; };

            h.SetHeldBy(0);
            Assert.IsTrue(h.IsHeld);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(0, last);

            h.SetHeldBy(0);
            Assert.AreEqual(1, calls, "mismo dueno: sin evento");

            h.SetHeldBy(2);
            Assert.AreEqual(2, calls);
            Assert.AreEqual(2, h.HeldByOwnerId);

            h.Release();
            Assert.IsFalse(h.IsHeld);
            Assert.AreEqual(3, calls);
            Assert.AreEqual(Holdable.NoOwner, last);
        }

        [Test]
        public void Mark_AlSoltarNoAgregaComponente()
        {
            go = new GameObject("caja");
            Holdable.Mark(go, Holdable.NoOwner);
            Assert.IsFalse(go.TryGetComponent(out Holdable _));
            Holdable.Mark(go, 0);
            Assert.IsTrue(Holdable.IsObjectHeld(go.transform));
            Holdable.Mark(go, Holdable.NoOwner);
            Assert.IsFalse(Holdable.IsObjectHeld(go.transform));
        }

        [Test]
        public void IsObjectHeld_MiraLosPadres()
        {
            go = new GameObject("lienzo");
            var child = new GameObject("estampa");
            child.transform.SetParent(go.transform);
            Holdable.Mark(go, 1);
            Assert.IsTrue(Holdable.IsObjectHeld(child.transform));
            Assert.IsFalse(Holdable.IsObjectHeld(null));
        }
    }
}
