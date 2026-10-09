using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.Core
{
    /// <summary>
    /// Cartuchos de tintero que lleva el jugador: cada uno tiene una clase de tinta y 5 usos. Con R se gasta un uso
    /// (FIFO: primero el cartucho en uso). Logica pura y serializable.
    /// </summary>
    [Serializable]
    public class InkCartridgeStock
    {
        public const int UsesPerCartridge = 5;
        public const int DefaultMaxCartridges = 4;

        [Serializable]
        private struct Cartridge
        {
            public InkKind kind;
            public int uses;
        }

        [SerializeField] private List<Cartridge> cartridges = new List<Cartridge>();
        [SerializeField] private int maxCartridges = DefaultMaxCartridges;

        /// <summary>Se emite cuando cambia la cantidad de cartuchos, sus usos o el maximo.</summary>
        public event Action Changed;

        /// <summary>Stock inicial del modo Libre: 2 cartuchos de tinta normal.</summary>
        public InkCartridgeStock()
        {
            cartridges.Add(new Cartridge { kind = InkKind.Normal, uses = UsesPerCartridge });
            cartridges.Add(new Cartridge { kind = InkKind.Normal, uses = UsesPerCartridge });
        }

        public int MaxCartridges => maxCartridges;
        public int CartridgeCount => cartridges.Count;

        public int TotalUses
        {
            get
            {
                int total = 0;
                for (int i = 0; i < cartridges.Count; i++) total += cartridges[i].uses;
                return total;
            }
        }

        /// <summary>Clase del cartucho en uso (Normal si no hay ninguno).</summary>
        public InkKind CurrentKind => cartridges.Count > 0 ? cartridges[0].kind : InkKind.Normal;

        /// <summary>Usos que le quedan al cartucho en uso.</summary>
        public int CurrentUses => cartridges.Count > 0 ? cartridges[0].uses : 0;

        /// <summary>Suma un cartucho. False si ya esta lleno el stock.</summary>
        public bool TryAdd(InkKind kind, int uses = UsesPerCartridge)
        {
            if (cartridges.Count >= maxCartridges || uses <= 0) return false;
            cartridges.Add(new Cartridge { kind = kind, uses = uses });
            Changed?.Invoke();
            return true;
        }

        /// <summary>Gasta un uso del cartucho en uso; al llegar a 0 se descarta. False si no hay cartuchos.</summary>
        public bool TryUse(out InkKind kind)
        {
            if (cartridges.Count == 0)
            {
                kind = InkKind.Normal;
                return false;
            }
            Cartridge c = cartridges[0];
            kind = c.kind;
            c.uses--;
            if (c.uses <= 0) cartridges.RemoveAt(0);
            else cartridges[0] = c;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Cambia el maximo de cartuchos (no quita los que ya hay).</summary>
        public void SetMax(int max)
        {
            maxCartridges = Mathf.Max(0, max);
            Changed?.Invoke();
        }
    }
}
