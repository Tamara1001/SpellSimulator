namespace SpellSimulator.Learning
{
    /// <summary>Reglas puras del grimorio.</summary>
    public static class GrimoireMath
    {
        /// <summary>Segundos lejos antes de que el libro suelto vuelva al inventario.</summary>
        public const float ReturnSeconds = 2f;

        /// <summary>
        /// True si el libro debe volver al inventario: suelto (ni sostenido ni abierto en la mano), mas lejos que
        /// <see cref="Grimoire.ReturnDistance"/> durante al menos <see cref="ReturnSeconds"/>.
        /// </summary>
        public static bool ShouldReturn(float distance, bool held, bool open, float awaySeconds)
            => !held && !open && distance > Grimoire.ReturnDistance && awaySeconds >= ReturnSeconds;
    }
}
