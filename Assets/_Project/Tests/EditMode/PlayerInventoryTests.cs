using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Player;

namespace SpellSimulator.Tests
{
    public class PlayerInventoryTests
    {
        private readonly List<GameObject> created = new List<GameObject>();

        private GameObject Item(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
            created.Clear();
        }

        [Test]
        public void Equipar_VaAlPrimerSlotLibre()
        {
            var inv = new PlayerInventory();
            GameObject pen = Item("pen"), eraser = Item("eraser");
            inv.Equip(inv.FindPickupSlot(), pen);
            inv.Stow();
            inv.Equip(inv.FindPickupSlot(), eraser);
            Assert.AreSame(pen, inv.HotbarItem(0));
            Assert.AreSame(eraser, inv.HotbarItem(1));
            Assert.AreEqual(1, inv.ActiveSlot);
            Assert.AreSame(eraser, inv.HeldMain);
        }

        [Test]
        public void BarraLlena_ElSlotDeEntradaEsElActivo()
        {
            var inv = new PlayerInventory();
            for (int i = 0; i < 3; i++) { inv.Stow(); inv.Equip(inv.FindPickupSlot(), Item("t" + i)); }
            inv.Stow();
            inv.Select(1);
            Assert.AreEqual(1, inv.FindPickupSlot());

            // Reemplazo: se suelta lo del activo y entra lo nuevo en ese slot.
            GameObject old = inv.RemoveMain();
            Assert.IsNotNull(old);
            Assert.IsNull(inv.HotbarItem(1));
            GameObject stamp = Item("stamp");
            inv.Equip(inv.FindPickupSlot(), stamp);
            Assert.AreSame(stamp, inv.HotbarItem(1));
            Assert.AreEqual(1, inv.ActiveSlot);
        }

        [Test]
        public void Soltar_LiberaElSlotYLaMano()
        {
            var inv = new PlayerInventory();
            GameObject pen = Item("pen");
            inv.Equip(0, pen);
            Assert.AreSame(pen, inv.RemoveMain());
            Assert.IsNull(inv.HeldMain);
            Assert.IsNull(inv.HotbarItem(0));
            Assert.IsNull(inv.RemoveMain(), "sin nada en la mano no suelta nada");
        }

        [Test]
        public void CambiarSlot_PasaLoGuardadoALaMano()
        {
            var inv = new PlayerInventory();
            GameObject pen = Item("pen"), eraser = Item("eraser");
            inv.Equip(0, pen);
            inv.Stow();
            inv.Equip(1, eraser);

            Assert.IsTrue(inv.CanSelect(0));
            inv.Stow();
            Assert.AreSame(pen, inv.Select(0));
            Assert.AreEqual(0, inv.ActiveSlot);
            Assert.AreSame(pen, inv.HeldMain);
            Assert.AreSame(eraser, inv.HotbarItem(1), "lo guardado sigue en su slot");

            Assert.IsFalse(inv.CanSelect(0), "el mismo slot con algo en la mano no cambia nada");
            Assert.IsFalse(inv.CanSelect(5), "fuera de rango");
            inv.Stow();
            Assert.IsNull(inv.Select(2), "slot vacio: mano vacia");
            Assert.IsFalse(inv.CanSelect(2), "elegido y vacio: no cambia nada");
        }

        [Test]
        public void Cinturon_NoCambiaLaBarra()
        {
            var inv = new PlayerInventory();
            Assert.AreEqual(3, inv.SlotCount);
            Assert.IsTrue(inv.SetBelt(true));
            Assert.IsTrue(inv.HasBelt);
            Assert.AreEqual(3, inv.SlotCount);
            Assert.AreEqual(3, PlayerInventory.Slots);
            Assert.IsFalse(inv.SetBelt(true), "equiparlo dos veces no cambia nada");
        }

        [Test]
        public void ManoSecundaria_LoArrastradoTienePrioridad()
        {
            var inv = new PlayerInventory();
            GameObject canvas = Item("canvas"), box = Item("box");
            inv.SetHandSecondary(canvas);
            Assert.AreSame(canvas, inv.Secondary);
            GameObject dragged = box;
            inv.DraggedQuery = () => dragged;
            Assert.AreSame(box, inv.Secondary);
            dragged = null;
            Assert.AreSame(canvas, inv.Secondary);
            Assert.AreSame(canvas, inv.ClearHandSecondary());
            Assert.IsNull(inv.Secondary);
        }

        [Test]
        public void EventoChanged_SeEmiteEnCadaOperacion()
        {
            var inv = new PlayerInventory();
            int count = 0;
            inv.Changed += () => count++;
            GameObject pen = Item("pen");

            inv.Equip(0, pen); Assert.AreEqual(1, count, "equipar");
            inv.Stow(); Assert.AreEqual(1, count, "guardar solo no avisa (avisa la operacion que sigue)");
            inv.Select(0); Assert.AreEqual(2, count, "cambiar de slot");
            inv.RemoveMain(); Assert.AreEqual(3, count, "soltar");
            inv.RemoveMain(); Assert.AreEqual(3, count, "soltar sin nada no avisa");
            inv.SetBelt(true); Assert.AreEqual(4, count, "cinturon");
            inv.SetBelt(true); Assert.AreEqual(4, count, "cinturon repetido no avisa");
            inv.SetHandSecondary(pen); Assert.AreEqual(5, count, "mano secundaria");

            inv.PollSecondary(); Assert.AreEqual(6, count, "primer sondeo ve la mano secundaria nueva");
            inv.PollSecondary(); Assert.AreEqual(6, count, "sin cambios no avisa");
            inv.ClearHandSecondary(); Assert.AreEqual(7, count);
            inv.PollSecondary(); Assert.AreEqual(8, count, "el sondeo detecta que se vacio");
        }
    }
}
