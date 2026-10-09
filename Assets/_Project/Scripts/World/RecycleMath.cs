namespace SpellSimulator.World
{
    /// <summary>
    /// Economia cerrada de la estacion de reciclaje (<see cref="RecyclingStation"/>): 1 runa agotada = 1 runa nueva de su
    /// clase; una runa que todavia sirve cuesta 2 (de la misma clase) por 1 nueva. Un papel con tinta = 1 papel limpio.
    /// </summary>
    public static class RecycleMath
    {
        /// <summary>Segundos entre que se consume lo dejado y la estacion entrega el reemplazo.</summary>
        public const float RecycleSeconds = 8f;
        /// <summary>Segundos de la animacion de consumo (achicarse y subir).</summary>
        public const float AnimSeconds = 1f;

        /// <summary>Cuantas runas de esa clase hay que dejar para obtener una nueva.</summary>
        public static int Cost(RuneKind kind, bool exhausted) => exhausted ? 1 : 2;

        /// <summary>Runas nuevas que se obtienen con esa cantidad de agotadas y de todavia utiles (de una misma clase).</summary>
        public static int Outputs(RuneKind kind, int exhaustedCount, int freshCount)
        {
            int fromExhausted = exhaustedCount > 0 ? exhaustedCount / Cost(kind, true) : 0;
            int fromFresh = freshCount > 0 ? freshCount / Cost(kind, false) : 0;
            return fromExhausted + fromFresh;
        }

        public static bool CanRecycle(int exhaustedCount, int freshCount, RuneKind kind) => Outputs(kind, exhaustedCount, freshCount) > 0;

        /// <summary>Cuantas runas utiles se consumen: siempre de a pares, la impar sobrante se rechaza.</summary>
        public static int FreshToConsume(RuneKind kind, int freshCount)
        {
            int cost = Cost(kind, false);
            return freshCount > 0 ? freshCount / cost * cost : 0;
        }

        /// <summary>Instante (mismo reloj que <c>now</c>) en que se entrega lo consumido ahora.</summary>
        public static float ReadyAt(float now) => now + RecycleSeconds;

        public static bool IsReady(float now, float readyAt) => now >= readyAt;
    }
}
