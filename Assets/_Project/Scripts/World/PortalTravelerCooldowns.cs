using System.Collections.Generic;

namespace SpellSimulator.World
{
    /// <summary>
    /// Enfriamiento del viajero de los portales, POR JUGADOR (ownerId -> hora en que puede volver a cruzar). Logica pura
    /// (la hora la pone quien llama, normalmente <c>GameClock.Time</c>) para poder probarla y para multijugador.
    /// </summary>
    public static class PortalTravelerCooldowns
    {
        private static readonly Dictionary<int, float> readyAt = new Dictionary<int, float>(4);

        /// <summary>True si el jugador ya puede viajar a la hora <paramref name="now"/>.</summary>
        public static bool Ready(int ownerId, float now)
            => !readyAt.TryGetValue(ownerId, out float t) || now >= t;

        /// <summary>Marca que el jugador acaba de viajar: no podra volver a cruzar hasta <c>now + cooldown</c>.</summary>
        public static void Mark(int ownerId, float now, float cooldown) => readyAt[ownerId] = now + cooldown;

        public static void Clear() => readyAt.Clear();
    }
}
