namespace SpellSimulator.Player
{
    /// <summary>
    /// Reglas de la vida del jugador en el modo Libre (Docs/MAPA_FLUJO_PLAN.md, paso V; D28 y D33). Lógica pura, sin escena:
    /// la aplica <see cref="PlayerVitality"/> y la cubre VitalityRulesTests. En Duelo manda NetDuel: estas reglas no corren.
    /// </summary>
    public static class VitalityRules
    {
        /// <summary>Vida del jugador en Libre (igual que la de un duelista).</summary>
        public const float MaxHealth = 100f;

        /// <summary>Segundos sin recibir daño antes de empezar a recuperarse.</summary>
        public const float RegenDelay = 6f;

        /// <summary>Vida por segundo que se recupera pasado <see cref="RegenDelay"/> (de 0 a lleno en ~12 s).</summary>
        public const float RegenPerSecond = 8f;

        /// <summary>Por debajo de esta fracción de vida se avisa en el HUD.</summary>
        public const float LowFraction = 0.35f;

        /// <summary>Segundos mínimos entre dos avisos de vida baja.</summary>
        public const float LowWarningCooldown = 8f;

        /// <summary>Cuánta vida recuperar en este paso: nada hasta <see cref="RegenDelay"/> sin daño, después un ritmo fijo.</summary>
        public static float RegenAmount(float secondsSinceDamage, float dt) =>
            secondsSinceDamage >= RegenDelay && dt > 0f ? RegenPerSecond * dt : 0f;

        /// <summary>¿El jugador queda a salvo del daño? Solo con el interruptor encendido y fuera de un duelo.</summary>
        public static bool Protected(bool noDamageOn, bool inDuel) => noDamageOn && !inDuel;

        /// <summary>¿Hay que avisar que la vida está baja? (cruzó el umbral y pasó el enfriamiento del aviso).</summary>
        public static bool ShouldWarnLow(float fraction, float secondsSinceWarning) =>
            fraction > 0f && fraction < LowFraction && secondsSinceWarning >= LowWarningCooldown;
    }
}
