using UnityEngine;

namespace SpellSimulator.Core
{
    /// <summary>
    /// Fuente de azar de las REGLAS (F0 de MULTIPLAYER_PLAN): en red el servidor la siembra para que todos vean lo mismo, y las pruebas
    /// la fijan. El azar puramente visual (fases de vaiven, particulas) sigue con UnityEngine.Random.
    /// Hoy ningun azar de reglas lo usa todavia (el de SealTrigger/RuneJoin/SupplyPoint/SpellCaster es visual o inexistente).
    /// </summary>
    public interface IGameRandom
    {
        /// <summary>Valor en [0, 1].</summary>
        float Value { get; }
        float Range(float min, float max);
        Vector3 InsideUnitSphere { get; }
    }

    /// <summary>Azar actual del juego; por defecto el de Unity.</summary>
    public static class GameRandom
    {
        private sealed class UnityRandom : IGameRandom
        {
            public float Value => UnityEngine.Random.value;
            public float Range(float min, float max) => UnityEngine.Random.Range(min, max);
            public Vector3 InsideUnitSphere => UnityEngine.Random.insideUnitSphere;
        }

        private static readonly IGameRandom unityRandom = new UnityRandom();
        private static IGameRandom current = unityRandom;

        public static IGameRandom Current => current;

        /// <summary>Reemplaza la fuente de azar (semilla de servidor o pruebas). Null vuelve a la de Unity.</summary>
        public static void Set(IGameRandom random) => current = random ?? unityRandom;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload() => current = unityRandom;
    }
}
