using UnityEngine;

namespace SpellSimulator.Casting
{
    /// <summary>Retardo de lanzamiento de un sello: cuanto mas grande el anillo, mas tarda en cargar. Logica pura.</summary>
    public static class SealCharge
    {
        public const float MinSeconds = 0.5f;
        public const float MaxSeconds = 2f;
        /// <summary>Radio (metros) a partir del cual el retardo es el minimo.</summary>
        public const float SmallRadius = 0.05f;
        /// <summary>Radio (metros) a partir del cual el retardo es el maximo.</summary>
        public const float LargeRadius = 0.5f;

        public static float Duration(float ringRadiusWorld)
        {
            float t = Mathf.InverseLerp(SmallRadius, LargeRadius, ringRadiusWorld);
            return Mathf.Lerp(MinSeconds, MaxSeconds, t);
        }
    }
}
