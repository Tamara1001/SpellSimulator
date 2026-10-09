using System;
using UnityEngine;

namespace SpellSimulator.Player
{
    /// <summary>
    /// ESTADO del inventario de un jugador, sin escena (F0 de MULTIPLAYER_PLAN, 2.8): barra de 3 slots, slot activo, lo que hay
    /// en la mano principal, lo agarrado en la mano secundaria (VR), lo que arrastra el movedor (solo consulta) y el cinturon.
    /// Solo guarda referencias y aplica REGLAS (primer slot libre, equipar, guardar, soltar, cambiar de slot); los efectos de
    /// escena (emparentar a la mano, activar/desactivar, fisica) los hace <see cref="PlayerInteractor"/>. En red, esta es la
    /// copia que el servidor valida y replica (`PlayerInventoryNet`). Sin allocations por frame.
    /// </summary>
    public sealed class PlayerInventory
    {
        /// <summary>Slots de la barra (teclas 1-3), con o sin cinturon.</summary>
        public const int Slots = HotbarMath.BaseSlots;

        private readonly GameObject[] hotbar = new GameObject[HotbarMath.MaxSlots];
        private readonly bool[] slotFree = new bool[HotbarMath.MaxSlots];
        private GameObject lastSecondary;

        /// <summary>Cambio en la barra, en lo agarrado o en el cinturon (la HUD vuelve a dibujar).</summary>
        public event Action Changed;

        /// <summary>Slot elegido (0 a 2).</summary>
        public int ActiveSlot { get; private set; }
        /// <summary>Lo que esta en la mano principal (null si esta guardado o vacio).</summary>
        public GameObject HeldMain { get; private set; }
        /// <summary>Lo agarrado en la mano secundaria en VR (en PC lo arrastra el movedor, ver <see cref="Dragged"/>).</summary>
        public GameObject HandSecondary { get; private set; }
        /// <summary>True con el cinturon equipado (permite cartuchos de varias clases; no cambia la barra).</summary>
        public bool HasBelt { get; private set; }

        /// <summary>Consulta de lo que arrastra el movedor (el estado vive en <see cref="ObjectMover"/>). Puede ser null.</summary>
        public Func<GameObject> DraggedQuery { get; set; }

        /// <summary>Lo que arrastra el movedor (null si nada).</summary>
        public GameObject Dragged
        {
            get
            {
                Func<GameObject> query = DraggedQuery;
                return query != null ? query() : null;
            }
        }

        /// <summary>Lo de la mano secundaria: lo arrastrado si hay, si no lo agarrado en la mano (VR). Null si nada.</summary>
        public GameObject Secondary
        {
            get
            {
                GameObject dragged = Dragged;
                return dragged != null ? dragged : HandSecondary;
            }
        }

        /// <summary>Slots usables de la barra: siempre 3.</summary>
        public int SlotCount => HotbarMath.SlotCount(HasBelt);

        /// <summary>Objeto de un slot (null si esta vacio o fuera de rango).</summary>
        public GameObject HotbarItem(int index) => index >= 0 && index < hotbar.Length ? hotbar[index] : null;

        // ---------------------------------------------------------------- mano principal (barra)

        /// <summary>Slot donde entra lo que se levanta: el primero libre o, con la barra llena, el activo (hay que soltar lo que tiene).</summary>
        public int FindPickupSlot()
        {
            for (int i = 0; i < hotbar.Length; i++) slotFree[i] = hotbar[i] == null;
            int slot = HotbarMath.FirstFreeSlot(slotFree, SlotCount);
            return slot >= 0 ? slot : ActiveSlot;
        }

        /// <summary>Guarda lo de la mano principal (sigue en su slot). Sin evento: lo emite la operacion que sigue.</summary>
        public void Stow() => HeldMain = null;

        /// <summary>Pone el objeto en el slot, lo elige y queda en la mano principal.</summary>
        public void Equip(int slot, GameObject item)
        {
            if (slot < 0 || slot >= hotbar.Length) return;
            hotbar[slot] = item;
            ActiveSlot = slot;
            HeldMain = item;
            RaiseChanged();
        }

        /// <summary>False si elegir ese slot no cambia nada (ya elegido y en la mano, o ya elegido y vacio).</summary>
        public bool CanSelect(int index)
        {
            if (index < 0 || index >= hotbar.Length) return false;
            return !(index == ActiveSlot && (HeldMain != null || hotbar[index] == null));
        }

        /// <summary>Elige el slot: lo que tenga pasa a la mano principal (null si esta vacio). Devuelve ese objeto.</summary>
        public GameObject Select(int index)
        {
            if (index < 0 || index >= hotbar.Length) return null;
            ActiveSlot = index;
            HeldMain = hotbar[index];
            RaiseChanged();
            return HeldMain;
        }

        /// <summary>Suelta lo de la mano principal y libera su slot. Devuelve lo soltado (null si no habia nada).</summary>
        public GameObject RemoveMain()
        {
            GameObject item = HeldMain;
            if (item == null) return null;
            if (hotbar[ActiveSlot] == item) hotbar[ActiveSlot] = null;
            HeldMain = null;
            RaiseChanged();
            return item;
        }

        // ---------------------------------------------------------------- mano secundaria (VR)

        /// <summary>Lo agarrado pasa a la mano secundaria.</summary>
        public void SetHandSecondary(GameObject item)
        {
            HandSecondary = item;
            RaiseChanged();
        }

        /// <summary>Vacia la mano secundaria. Devuelve lo que tenia (null si nada).</summary>
        public GameObject ClearHandSecondary()
        {
            GameObject item = HandSecondary;
            HandSecondary = null;
            RaiseChanged();
            return item;
        }

        // ---------------------------------------------------------------- cinturon y avisos

        /// <summary>Equipa o quita el cinturon. Devuelve true si cambio (y emite el evento).</summary>
        public bool SetBelt(bool equipped)
        {
            if (HasBelt == equipped) return false;
            HasBelt = equipped;
            RaiseChanged();
            return true;
        }

        /// <summary>Una vez por cuadro: avisa si cambio lo de la mano secundaria por fuera (el movedor empezo o dejo de arrastrar).</summary>
        public void PollSecondary()
        {
            GameObject secondary = Secondary;
            if (ReferenceEquals(secondary, lastSecondary)) return;
            lastSecondary = secondary;
            RaiseChanged();
        }

        public void RaiseChanged() => Changed?.Invoke();
    }
}
