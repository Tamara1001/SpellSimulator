using UnityEngine;

namespace SpellSimulator.Lab
{
    /// <summary>Parametros visuales del viento del laboratorio para una velocidad y direccion dadas (ver <see cref="WindVisualMath.Params"/>).</summary>
    public readonly struct WindVisualParams
    {
        /// <summary>Particulas totales (estelas + hojas + polvo), ≤ <see cref="WindVisualMath.MaxParticles"/>.</summary>
        public readonly int ParticleCount;
        /// <summary>Estelas rizadas simultaneas, ≤ <see cref="WindVisualMath.MaxTrails"/>.</summary>
        public readonly int TrailCount;
        /// <summary>Hojas/petalos simultaneos.</summary>
        public readonly int LeafCount;
        /// <summary>Bocanadas de polvo simultaneas (0 si no hay polvo).</summary>
        public readonly int DustCount;
        /// <summary>Vida de cada particula (s): lo que tarda en cruzar la zona; baja con la velocidad.</summary>
        public readonly float Lifetime;
        /// <summary>Largo de cada estela (m).</summary>
        public readonly float TrailLength;
        /// <summary>Ancho de cada estela (m).</summary>
        public readonly float TrailWidth;
        /// <summary>Opacidad de estelas y hojas (0-1).</summary>
        public readonly float Alpha;
        /// <summary>Intensidad del ruido (rizado) de las estelas.</summary>
        public readonly float Noise;
        /// <summary>True si sopla a mas de <see cref="WindVisualMath.DustSpeed"/>: polvo a ras del piso.</summary>
        public readonly bool Dust;
        /// <summary>Tension de la manga y los banderines (0 = cuelgan, 1 = horizontales).</summary>
        public readonly float SockTension;
        /// <summary>Velocidad de desplazamiento de los chevrones del piso (m/s).</summary>
        public readonly float ChevronSpeed;
        /// <summary>Direccion normalizada a [0, 360) grados sobre Y desde +Z.</summary>
        public readonly float YawDeg;
        /// <summary>Direccion horizontal unitaria (igual que <see cref="LabWindZone.DirectionFromYaw"/>).</summary>
        public readonly Vector3 Direction;

        public WindVisualParams(int trailCount, int leafCount, int dustCount, float lifetime, float trailLength, float trailWidth,
            float alpha, float noise, bool dust, float sockTension, float chevronSpeed, float yawDeg, Vector3 direction)
        {
            TrailCount = trailCount;
            LeafCount = leafCount;
            DustCount = dustCount;
            ParticleCount = trailCount + leafCount + dustCount;
            Lifetime = lifetime;
            TrailLength = trailLength;
            TrailWidth = trailWidth;
            Alpha = alpha;
            Noise = noise;
            Dust = dust;
            SockTension = sockTension;
            ChevronSpeed = chevronSpeed;
            YawDeg = yawDeg;
            Direction = direction;
        }
    }

    /// <summary>
    /// Matematica pura del visual del viento del laboratorio: cuantas particulas, cuanto viven, que tan largas y opacas son
    /// las estelas, si hay polvo y cuanto se tensa la manga. Todo es monotonico en la velocidad y esta acotado por los topes
    /// de Quest (≤ 150 particulas, ≤ 40 estelas). Sin MonoBehaviour: se prueba en EditMode.
    /// </summary>
    public static class WindVisualMath
    {
        /// <summary>Tope de particulas del visual (presupuesto Quest).</summary>
        public const int MaxParticles = 150;
        /// <summary>Tope de estelas (presupuesto Quest).</summary>
        public const int MaxTrails = 40;
        /// <summary>Velocidad desde la que se levanta polvo del piso (m/s, estrictamente mayor).</summary>
        public const float DustSpeed = 10f;
        /// <summary>Velocidad de referencia del visual (m/s): el tope de la mesa de variables (LabCatalog, WindSpeed).</summary>
        public const float ReferenceSpeed = 20f;
        /// <summary>
        /// Velocidad a la que la manga queda horizontal (m/s). Una manga de aerodromo se extiende del todo con ~15 nudos
        /// (≈ 7,7 m/s); se redondea a 8.
        /// </summary>
        public const float FullSockSpeed = 8f;
        /// <summary>Recorrido de cada particula (m): el largo de la caja de LabWindZone (4 m).</summary>
        public const float Travel = 4f;

        private const int MinTrails = 8, MinLeaves = 4, MaxLeaves = 30, MaxDust = 60;
        private const float MinLifetime = 0.35f, MaxLifetime = 2.5f;
        private const float MinTrailLength = 0.4f, MaxTrailLength = 2.2f;
        private const float MinTrailWidth = 0.025f, MaxTrailWidth = 0.06f;
        private const float MinAlpha = 0.3f, MaxAlpha = 0.85f;
        private const float MinNoise = 0.15f, MaxNoise = 0.9f;
        private const float ChevronSpeedFactor = 0.25f;

        /// <summary>Parametros visuales para <paramref name="speed"/> (m/s; negativo o NaN = calma) y <paramref name="yawDeg"/>.</summary>
        public static WindVisualParams Params(float speed, float yawDeg)
        {
            float s = speed > 0f ? speed : 0f; // NaN tambien cae aca
            float t = Mathf.Clamp01(s / ReferenceSpeed);
            float yaw = float.IsNaN(yawDeg) || float.IsInfinity(yawDeg) ? 0f : Mathf.Repeat(yawDeg, 360f);
            Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

            if (s <= 0f)
                return new WindVisualParams(0, 0, 0, MaxLifetime, 0f, MinTrailWidth, 0f, 0f, false, 0f, 0f, yaw, dir);

            int trails = Mathf.Min(MaxTrails, Mathf.RoundToInt(Mathf.Lerp(MinTrails, MaxTrails, t)));
            int leaves = Mathf.RoundToInt(Mathf.Lerp(MinLeaves, MaxLeaves, t));
            bool dust = s > DustSpeed;
            int dustCount = dust ? Mathf.RoundToInt(Mathf.Lerp(0f, MaxDust, Mathf.Clamp01((s - DustSpeed) / (ReferenceSpeed - DustSpeed)))) : 0;
            // Tope duro por si se cambian las constantes: primero se recorta el polvo, despues las hojas.
            int over = trails + leaves + dustCount - MaxParticles;
            if (over > 0) { int cut = Mathf.Min(over, dustCount); dustCount -= cut; over -= cut; }
            if (over > 0) leaves = Mathf.Max(0, leaves - over);

            float lifetime = Mathf.Clamp(Travel / s, MinLifetime, MaxLifetime);
            float length = Mathf.Lerp(MinTrailLength, MaxTrailLength, t);
            float width = Mathf.Lerp(MinTrailWidth, MaxTrailWidth, t);
            float alpha = Mathf.Lerp(MinAlpha, MaxAlpha, t);
            float noise = Mathf.Lerp(MinNoise, MaxNoise, t);
            float tension = Mathf.Clamp01(s / FullSockSpeed);
            return new WindVisualParams(trails, leaves, dustCount, lifetime, length, width, alpha, noise, dust, tension,
                Mathf.Min(s, ReferenceSpeed) * ChevronSpeedFactor, yaw, dir);
        }
    }
}
