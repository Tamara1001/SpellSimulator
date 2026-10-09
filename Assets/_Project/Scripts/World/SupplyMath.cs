namespace SpellSimulator.World
{
    /// <summary>
    /// Reglas puras de un punto de reaparicion (<see cref="SupplyPoint"/>): cuando toca reponer y cuanto falta. Sin escena,
    /// para poder validarlas en el servidor (multijugador) y en EditMode.
    /// </summary>
    public static class SupplyMath
    {
        /// <summary>Se repone si hay menos objetos vivos que el limite y paso el enfriamiento desde que quedo libre un lugar.</summary>
        public static bool ShouldSpawn(int live, int maxLive, float secondsSinceFree, float cooldown)
            => maxLive > 0 && live < maxLive && secondsSinceFree >= cooldown;

        /// <summary>Segundos que faltan para la proxima reposicion; -1 si el punto esta lleno (la cuenta no corre).</summary>
        public static float SecondsRemaining(int live, int maxLive, float secondsSinceFree, float cooldown)
        {
            if (live >= maxLive) return -1f;
            float left = cooldown - secondsSinceFree;
            return left > 0f ? left : 0f;
        }
    }
}
