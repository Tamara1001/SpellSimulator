namespace SpellSimulator.World
{
    /// <summary>Material de una runa (bloque 4 del plan): define durabilidad, enfriamiento, peso y resistencia.</summary>
    public enum RuneKind
    {
        /// <summary>Piedra comun: poca durabilidad (2 usos en total), barata y comun.</summary>
        Stone = 0,
        /// <summary>Metal: durabilidad media (5), pesada, resiste agua y fuego.</summary>
        Metal = 1,
        /// <summary>Oro: mucha durabilidad (10), enfriamiento corto, rara y con brillo propio.</summary>
        Gold = 2,
    }
}
