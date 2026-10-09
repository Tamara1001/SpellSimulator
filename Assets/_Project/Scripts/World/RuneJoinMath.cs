namespace SpellSimulator.World
{
    /// <summary>Reglas puras para decidir si dos runas se unen (bloque 4): armadas, vivas y en contacto suave.</summary>
    public static class RuneJoinMath
    {
        /// <summary>Segundos de contacto suave sostenido para que dos runas se fusionen.</summary>
        public const float DefaultJoinSeconds = 0.4f;
        /// <summary>Una runa que se disparo hace menos de esto no se une (evita duplicar el disparo por impacto).</summary>
        public const float RecentFireSeconds = 0.25f;

        public static bool CanJoin(bool aArmed, bool bArmed, bool aExhausted, bool bExhausted,
            bool aFiredRecently, bool bFiredRecently, float relativeSpeed, float impactThreshold)
        {
            if (!aArmed || !bArmed || aExhausted || bExhausted || aFiredRecently || bFiredRecently) return false;
            return relativeSpeed < impactThreshold;
        }

        /// <summary>Temporizador de contacto: suma mientras haya contacto y se reinicia al perderlo.</summary>
        public static float Advance(float held, float dt, bool contact) => contact ? held + dt : 0f;

        public static bool Completed(float held, float joinSeconds) => held >= joinSeconds;

        /// <summary>True si A es la runa que manda (mayor radio de anillo; en empate, la de id menor).</summary>
        public static bool IsBig(float radiusA, float radiusB, int idA, int idB)
            => radiusA > radiusB || (radiusA == radiusB && idA < idB);

        /// <summary>De las dos runas, solo una dirige la union (para no ejecutarla dos veces): la de id menor.</summary>
        public static bool IsLeader(int idA, int idB) => idA < idB;
    }
}
