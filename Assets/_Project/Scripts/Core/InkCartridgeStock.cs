using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.Core
{
    /// <summary>
    /// Cartuchos de tintero que lleva el jugador, con UN slot por clase de tinta (<see cref="InkKind"/>). Cada cartucho tiene
    /// 5 usos. Sin cinturon se lleva una sola clase a la vez; con cinturon, las tres (hasta 4 cartuchos por clase).
    /// R gasta un uso de la clase ACTIVA (FIFO dentro de la clase); V la cicla. Logica pura y serializable.
    /// Decision: si se quita el cinturon, no se descarta nada; las otras clases quedan guardadas, solo se bloquea agregar
    /// (<see cref="CanCarry"/>) y ciclar (<see cref="CycleActive"/>). Si la activa se vacia, se pasa a la siguiente igual.
    /// </summary>
    [Serializable]
    public class InkCartridgeStock
    {
        public const int UsesPerCartridge = 5;
        /// <summary>Maximo de cartuchos POR CLASE.</summary>
        public const int DefaultMaxCartridges = 4;
        private const int KindCount = 3;
        private const int InitialNormalCartridges = 2;

        [Serializable]
        private class Slot
        {
            /// <summary>Usos restantes de cada cartucho; el indice 0 es el que esta en uso.</summary>
            public List<int> uses = new List<int>();
        }

        [SerializeField] private Slot[] slots = CreateSlots();
        [SerializeField] private int maxCartridges = DefaultMaxCartridges;
        [SerializeField] private int activeKind;
        [SerializeField] private bool hasBelt;

        /// <summary>Se emite cuando cambia algun cartucho, la clase activa, el cinturon o el maximo.</summary>
        public event Action Changed;

        /// <summary>Stock inicial del modo Libre: 2 cartuchos de tinta normal.</summary>
        public InkCartridgeStock()
        {
            for (int i = 0; i < InitialNormalCartridges; i++) slots[(int)InkKind.Normal].uses.Add(UsesPerCartridge);
        }

        private static Slot[] CreateSlots()
        {
            var s = new Slot[KindCount];
            for (int i = 0; i < s.Length; i++) s[i] = new Slot();
            return s;
        }

        /// <summary>Maximo de cartuchos por clase.</summary>
        public int MaxCartridges => maxCartridges;
        public bool HasBelt => hasBelt;
        public InkKind ActiveKind => (InkKind)activeKind;

        public int CartridgeCount(InkKind kind) => slots[(int)kind].uses.Count;

        /// <summary>Usos del cartucho en uso de esa clase (0 si no hay).</summary>
        public int UsesLeft(InkKind kind)
        {
            List<int> u = slots[(int)kind].uses;
            return u.Count > 0 ? u[0] : 0;
        }

        public int TotalUses(InkKind kind)
        {
            List<int> u = slots[(int)kind].uses;
            int total = 0;
            for (int i = 0; i < u.Count; i++) total += u[i];
            return total;
        }

        /// <summary>Cartuchos de todas las clases.</summary>
        public int TotalCartridges
        {
            get
            {
                int total = 0;
                for (int i = 0; i < slots.Length; i++) total += slots[i].uses.Count;
                return total;
            }
        }

        // Alias para la HUD (clase activa).
        public InkKind CurrentKind => ActiveKind;
        public int CurrentUses => UsesLeft(ActiveKind);

        public void SetBelt(bool equipped)
        {
            if (hasBelt == equipped) return;
            hasBelt = equipped;
            Changed?.Invoke();
        }

        /// <summary>Clases de tinta con al menos un cartucho.</summary>
        public int KindsWithCartridges
        {
            get
            {
                int n = 0;
                for (int i = 0; i < slots.Length; i++) if (slots[i].uses.Count > 0) n++;
                return n;
            }
        }

        /// <summary>True si V tiene algo que cambiar: cinturon equipado y cartuchos de mas de una clase.</summary>
        public bool CanCycle => hasBelt && KindsWithCartridges >= 2;

        /// <summary>Con cinturon siempre; sin el, solo si el stock esta vacio o ya lleva esa clase.</summary>
        public bool CanCarry(InkKind kind) => hasBelt || TotalCartridges == 0 || CartridgeCount(kind) > 0;

        /// <summary>Suma un cartucho a su clase. False si no se puede llevar esa clase o el slot esta lleno.</summary>
        public bool TryAdd(InkKind kind, int uses = UsesPerCartridge)
        {
            List<int> u = slots[(int)kind].uses;
            if (uses <= 0 || u.Count >= maxCartridges || !CanCarry(kind)) return false;
            if (TotalCartridges == 0) activeKind = (int)kind;
            u.Add(uses);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Gasta un uso de la clase activa; el cartucho a 0 se descarta y, si la clase se vacia, pasa a la siguiente con cartuchos.</summary>
        public bool TryUse(out InkKind kind)
        {
            if (slots[activeKind].uses.Count == 0 && !MoveToNextWithCartridges())
            {
                kind = InkKind.Normal;
                return false;
            }
            kind = (InkKind)activeKind;
            List<int> u = slots[activeKind].uses;
            int left = u[0] - 1;
            if (left <= 0)
            {
                u.RemoveAt(0);
                if (u.Count == 0) MoveToNextWithCartridges();
            }
            else u[0] = left;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Pasa a la siguiente clase con cartuchos. False si no hay a donde ciclar (sin cinturon tampoco se cicla).</summary>
        public bool CycleActive()
        {
            if (!hasBelt || !MoveToNextWithCartridges()) return false;
            Changed?.Invoke();
            return true;
        }

        private bool MoveToNextWithCartridges()
        {
            for (int step = 1; step < KindCount; step++)
            {
                int k = (activeKind + step) % KindCount;
                if (slots[k].uses.Count == 0) continue;
                activeKind = k;
                return true;
            }
            return false;
        }

        /// <summary>Cambia el maximo de cartuchos por clase (no quita los que ya hay).</summary>
        public void SetMax(int max)
        {
            maxCartridges = Mathf.Max(0, max);
            Changed?.Invoke();
        }
    }
}
