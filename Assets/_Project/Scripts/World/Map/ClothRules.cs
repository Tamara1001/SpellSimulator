namespace SpellSimulator.World.Map
{
    /// <summary>
    /// Reglas puras de la tela pesada que tapa la bajada del piso 1 de la torre-tutorial (Docs/MAPA_FLUJO_PLAN.md, §17).
    /// La tela se vuela cuando algo que no es el jugador la mueve: viento (la separa de su lugar o la acelera), un golpe de un
    /// objeto lanzado, o un empujon fuerte. El jugador solo la toca: para ella es un muro.
    /// </summary>
    public static class ClothRules
    {
        /// <summary>Distancia (m) a la que la tela, apartada de su lugar por el viento, se suelta.</summary>
        public const float OpenDistance = 0.55f;
        /// <summary>Rapidez (m/s) a partir de la cual se suelta (un golpe o una rafaga fuerte).</summary>
        public const float OpenSpeed = 2.2f;
        /// <summary>Impulso de choque (N·s) de un objeto lanzado que la suelta: un papel o una pluma no alcanzan, una caja si.</summary>
        public const float OpenImpulse = 6f;

        /// <summary>True si la tela colgada debe soltarse. Con el jugador tocandola nunca: ahi hace de muro.</summary>
        public static bool ShouldOpen(float displacement, float speed, float impulse, bool playerTouching)
        {
            if (playerTouching) return false;
            return displacement >= OpenDistance || speed >= OpenSpeed || impulse >= OpenImpulse;
        }
    }
}
