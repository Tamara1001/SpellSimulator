namespace SpellSimulator.Player
{
    /// <summary>Cuentas puras de la barra de objetos (3 slots fijos; el cinturon ya no suma slots).</summary>
    public static class HotbarMath
    {
        public const int BaseSlots = 3;
        public const int MaxSlots = 3;

        /// <summary>Siempre 3; el parametro se conserva por compatibilidad y se ignora.</summary>
        public static int SlotCount(bool belt) => BaseSlots;

        /// <summary>Primer slot libre dentro de los primeros <paramref name="slotCount"/>; -1 si no hay.</summary>
        public static int FirstFreeSlot(bool[] free, int slotCount)
        {
            if (free == null) return -1;
            int n = slotCount < free.Length ? slotCount : free.Length;
            for (int i = 0; i < n; i++) if (free[i]) return i;
            return -1;
        }

        /// <summary>Tecla valida (0..slotCount-1) o -1 si se ignora.</summary>
        public static int ClampKey(int key, int slotCount) => key >= 0 && key < slotCount ? key : -1;
    }
}
