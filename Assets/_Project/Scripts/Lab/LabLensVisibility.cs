namespace SpellSimulator.Lab
{
    /// <summary>
    /// Regla pura de visibilidad de los datos del Laboratorio (v4, punto 10): la informacion de reconocimiento y del dibujo
    /// (calidad, valido/invalido, elemento leido, estado previsto, banda de resultado, overlays) solo se ve con las lentes de
    /// tinta puestas, de cerca y mirando mas o menos hacia el elemento. Con histeresis para que no parpadee en el borde: se
    /// muestra dentro de <see cref="ShowDistance"/> m y <see cref="ShowAngleDeg"/>°, y una vez visible recien se oculta pasando
    /// <see cref="HideDistance"/> m o <see cref="HideAngleDeg"/>°.
    /// </summary>
    public static class LabLensVisibility
    {
        /// <summary>Distancia maxima (m) para empezar a mostrar.</summary>
        public const float ShowDistance = 3f;
        /// <summary>Angulo maximo (°) entre la mirada y el elemento para empezar a mostrar.</summary>
        public const float ShowAngleDeg = 50f;
        /// <summary>Margen de histeresis: lo visible se oculta recien un 10 % mas lejos (distancia) y 8° mas afuera (angulo).</summary>
        public const float DistanceHysteresis = 1.1f, AngleHysteresisDeg = 8f;
        /// <summary>Umbrales de ocultado con los valores por defecto.</summary>
        public const float HideDistance = ShowDistance * DistanceHysteresis, HideAngleDeg = ShowAngleDeg + AngleHysteresisDeg;

        /// <summary>Visible con los umbrales por defecto (3 m, 50°).</summary>
        public static bool IsVisible(bool lensesOn, float distance, float angleDeg, bool wasVisible)
            => IsVisible(lensesOn, distance, angleDeg, wasVisible, ShowDistance);

        /// <summary>
        /// Visible con otra distancia de muestra (p. ej. el pizarron de contadores, que se lee de mas lejos). Sin lentes nunca;
        /// distancia o angulo invalidos (negativos o NaN) cuentan como fuera de rango.
        /// </summary>
        public static bool IsVisible(bool lensesOn, float distance, float angleDeg, bool wasVisible, float showDistance)
        {
            if (!lensesOn) return false;
            if (!(distance >= 0f) || !(angleDeg >= 0f)) return false;
            float maxDistance = wasVisible ? showDistance * DistanceHysteresis : showDistance;
            float maxAngle = wasVisible ? ShowAngleDeg + AngleHysteresisDeg : ShowAngleDeg;
            return distance <= maxDistance && angleDeg <= maxAngle;
        }
    }
}
