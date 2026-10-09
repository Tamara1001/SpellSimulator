namespace SpellSimulator.Core
{
    /// <summary>Enfriamientos: logica pura que recibe la hora (de <see cref="GameClock"/>) para poder probarla con un reloj falso.</summary>
    public static class CooldownMath
    {
        /// <summary>True si ya paso la hora de rearme (<paramref name="rearmAt"/>).</summary>
        public static bool IsReady(float now, float rearmAt) => now >= rearmAt;

        /// <summary>True si el ultimo evento fue hace menos de <paramref name="seconds"/>.</summary>
        public static bool IsRecent(float now, float lastTime, float seconds) => now - lastTime < seconds;
    }

    /// <summary>Vencimiento del flotado (PinnedBody / PhysicalCanvas).</summary>
    public static class PinMath
    {
        public static bool ShouldRelease(float now, float pinnedAt, float floatSeconds) => now - pinnedAt >= floatSeconds;
    }
}
