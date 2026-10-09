using UnityEngine;

namespace SpellSimulator.Output
{
    /// <summary>Matematica pura del hechizo de cristal: radio de la esfera y duracion de la cristalizacion.</summary>
    public static class CrystalMath
    {
        /// <summary>Alcance base: metros de radio por unidad de radio del sello.</summary>
        public const float Range = 4f;
        public const float MinRadius = 0.6f;
        public const float MaxRadius = 8f;
        public const float MinSeconds = 8f;
        public const float MaxSeconds = 25f;

        /// <summary>Radio de la esfera de efecto en metros: crece con el sello y con la raiz de la potencia de tinta.</summary>
        public static float Radius(float sealRadius, float inkPower)
        {
            float power = Mathf.Sqrt(Mathf.Max(0f, inkPower));
            return Mathf.Clamp(sealRadius * Range * power, MinRadius, MaxRadius);
        }

        /// <summary>Segundos que dura la cristalizacion: mas grande, mas dura (8 a 25 s).</summary>
        public static float Seconds(float radius) =>
            Mathf.Lerp(MinSeconds, MaxSeconds, Mathf.InverseLerp(MinRadius, MaxRadius, radius));
    }
}
