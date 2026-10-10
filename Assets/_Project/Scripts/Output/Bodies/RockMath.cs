using UnityEngine;

namespace SpellSimulator.Output
{
    /// <summary>Numeros del hechizo de piedra (logica pura, con test EditMode). Ver PLAN_OCTAVA_TANDA seccion 3.</summary>
    public static class RockMath
    {
        public const float MinRadius = 0.08f, MaxRadius = 2f;
        /// <summary>Radio de la roca = anillo * este factor (prueba manual: las rocas salian muy chicas).</summary>
        public const float RingToRockFactor = 1.4f;
        public const float MinFormSeconds = 0.6f, MaxFormSeconds = 1.6f;
        public const float Density = 1800f, MinMass = 1f, MaxMass = 600f;
        /// <summary>Segundos de vida de una roca antes de empezar a achicarse.</summary>
        public const float LifeSeconds = 120f;
        public const float ShrinkSeconds = 1.5f;
        public const int MaxRocks = 10;
        /// <summary>Maximo de rocas vivas por duenio (anti-agotamiento en multijugador).</summary>
        public const int MaxRocksPerOwner = 4;

        /// <summary>
        /// True si, con esos conteos de rocas vivas, hay que achicar la mas vieja. El tope por duenio solo rige con varios
        /// jugadores (<paramref name="limitPerOwner"/>); en solitario solo cuenta el total.
        /// </summary>
        public static bool OverLimit(int aliveTotal, int aliveOfOwner, bool limitPerOwner) =>
            aliveTotal > MaxRocks || (limitPerOwner && aliveOfOwner > MaxRocksPerOwner);

        /// <summary>Radio de la roca: 0.9 x anillo, mas grande con tinta poderosa (raiz cubica: crece el volumen, no el diametro).</summary>
        public static float Radius(float ringRadius, float inkPower)
        {
            float ink = Mathf.Max(inkPower, 0.0001f);
            return Mathf.Clamp(ringRadius * RingToRockFactor * Mathf.Pow(ink, 1f / 3f), MinRadius, MaxRadius);
        }

        // ---------------------------------------------------------------- formas con columna (muro, puente), estacas y lanzamiento

        /// <summary>Segundos que dura un muro, puente o estaca antes de hundirse (salvo que el sello lleve convergencia: queda fijo).</summary>
        public const float SolidLifeSeconds = 60f;
        public const float SolidShrinkSeconds = 2f;
        /// <summary>Tope de piezas solidas vivas (muros, estacas) y, dentro de ellas, de las fijas (convergencia).</summary>
        public const int MaxSolidPieces = 48, MaxPermanentPieces = 24;
        /// <summary>Con un lente de convergencia por encima de este valor (SpellPlan.Focus), la forma queda fija.</summary>
        public const float PermanentFocus = 1.25f;

        public static bool IsPermanent(float planFocus) => planFocus >= PermanentFocus;

        /// <summary>Ancho del muro (m): crece con el anillo y la tinta.</summary>
        public static float WallWidth(float ringRadius, float inkPower) =>
            Mathf.Clamp(ringRadius * 7f * Mathf.Pow(Mathf.Max(inkPower, 0.0001f), 1f / 3f), 1.4f, 6f);

        /// <summary>Alto del muro o largo del puente (m) a partir del largo del chorro de la columna.</summary>
        public static float WallHeight(float coneLength) => Mathf.Clamp(coneLength * 0.45f, 1.2f, 5f);

        public static float BridgeLength(float coneLength) => Mathf.Clamp(coneLength * 0.7f, 2f, 7f);

        /// <summary>Rocas que forman un muro del ancho dado (cada una de ~0,9 m).</summary>
        public static int WallRocks(float width) => Mathf.Clamp(Mathf.RoundToInt(width / 0.9f), 2, 7);

        /// <summary>Estacas del abanico (dispersion) alrededor del sello.</summary>
        public static int SpikeCount(float ringRadius) => Mathf.Clamp(Mathf.RoundToInt(ringRadius * 40f), 6, 14);

        /// <summary>Velocidad (m/s) con la que sale lanzada una roca con el signo de region.</summary>
        public static float LaunchSpeed(float force) => 8f + 14f * Mathf.Clamp01(force);

        /// <summary>Cuanto tarda en formarse: las grandes tardan mas.</summary>
        public static float FormSeconds(float rockRadius)
        {
            float t = Mathf.InverseLerp(MinRadius, MaxRadius, rockRadius);
            return Mathf.Lerp(MinFormSeconds, MaxFormSeconds, t);
        }

        /// <summary>Masa (kg): densidad x volumen de esfera, acotada.</summary>
        public static float Mass(float rockRadius)
        {
            float volume = 4f / 3f * Mathf.PI * rockRadius * rockRadius * rockRadius;
            return Mathf.Clamp(Density * volume, MinMass, MaxMass);
        }
    }
}
