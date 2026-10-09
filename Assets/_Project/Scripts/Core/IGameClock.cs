using UnityEngine;

namespace SpellSimulator.Core
{
    /// <summary>
    /// Reloj de juego inyectable (F0 de MULTIPLAYER_PLAN): los temporizadores de reglas (carga de sellos, enfriamientos, duracion de
    /// portales, vencimientos) leen este reloj y no Time.time, para que en red todos usen la hora del servidor y las pruebas puedan
    /// adelantar el tiempo. <see cref="GameClock.Current"/> es el reloj de Unity salvo que se reemplace.
    /// </summary>
    public interface IGameClock
    {
        /// <summary>Segundos de juego desde el inicio (equivale a Time.time).</summary>
        float Time { get; }
        float DeltaTime { get; }
        float FixedDeltaTime { get; }
        /// <summary>Segundos reales (no afectados por la escala de tiempo; equivale a Time.unscaledTime).</summary>
        float UnscaledTime { get; }
    }

    /// <summary>Reloj actual del juego; por defecto el de Unity.</summary>
    public static class GameClock
    {
        private sealed class UnityClock : IGameClock
        {
            public float Time => UnityEngine.Time.time;
            public float DeltaTime => UnityEngine.Time.deltaTime;
            public float FixedDeltaTime => UnityEngine.Time.fixedDeltaTime;
            public float UnscaledTime => UnityEngine.Time.unscaledTime;
        }

        private static readonly IGameClock unityClock = new UnityClock();
        private static IGameClock current = unityClock;

        public static IGameClock Current => current;
        public static float Time => current.Time;
        public static float DeltaTime => current.DeltaTime;
        public static float FixedDeltaTime => current.FixedDeltaTime;
        public static float UnscaledTime => current.UnscaledTime;

        /// <summary>Reemplaza el reloj (red o pruebas). Null vuelve al de Unity.</summary>
        public static void Set(IGameClock clock) => current = clock ?? unityClock;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload() => current = unityClock;
    }
}
