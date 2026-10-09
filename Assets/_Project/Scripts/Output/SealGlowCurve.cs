using UnityEngine;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Curvas del brillo del anillo mientras un sello carga (logica pura, testeada en EditMode).
    /// t = tiempo de carga normalizado (0..1). Las intensidades son multiplicadores HDR del color del elemento.
    /// </summary>
    public static class SealGlowCurve
    {
        /// <summary>Intensidad al empezar la carga.</summary>
        public const float StartIntensity = 0.35f;
        /// <summary>Intensidad al terminar la carga (justo antes del destello).</summary>
        public const float EndIntensity = 1.6f;
        /// <summary>Frecuencia del pulso al empezar (Hz).</summary>
        public const float StartPulseHz = 2f;
        /// <summary>Frecuencia del pulso al terminar (Hz): se acelera a medida que el sello se llena.</summary>
        public const float EndPulseHz = 9f;

        /// <summary>Intensidad del destello al lanzarse.</summary>
        public const float FlashPeak = 3f;
        /// <summary>Segundos que el destello se mantiene arriba.</summary>
        public const float FlashHold = 0.15f;
        /// <summary>Segundos que tarda el destello en apagarse despues de mantenerse.</summary>
        public const float FlashDecay = 0.25f;
        /// <summary>Duracion total del destello (mantener + apagar).</summary>
        public const float FlashDuration = FlashHold + FlashDecay;
        /// <summary>Segundos del fundido si la carga se cancela (sin destello).</summary>
        public const float FadeOutSeconds = 0.1f;

        /// <summary>Intensidad durante la carga: crece cuadratico (lento al principio, se enciende al final).</summary>
        public static float Intensity(float t)
        {
            t = Mathf.Clamp01(t);
            return Mathf.Lerp(StartIntensity, EndIntensity, t * t);
        }

        /// <summary>Frecuencia del pulso durante la carga.</summary>
        public static float PulseHz(float t)
        {
            return Mathf.Lerp(StartPulseHz, EndPulseHz, Mathf.Clamp01(t));
        }

        /// <summary>Intensidad del destello tras lanzarse: FlashPeak durante FlashHold y luego baja suave a 0.</summary>
        public static float FlashIntensity(float timeSinceEnd)
        {
            if (timeSinceEnd <= FlashHold) return FlashPeak;
            float k = (timeSinceEnd - FlashHold) / FlashDecay;
            if (k >= 1f) return 0f;
            float remaining = 1f - k;
            return FlashPeak * remaining * remaining;
        }

        /// <summary>Fundido lineal a 0 en FadeOutSeconds desde la intensidad que tenia al cancelarse.</summary>
        public static float FadeOut(float startIntensity, float timeSinceEnd)
        {
            if (timeSinceEnd <= 0f) return startIntensity;
            float k = timeSinceEnd / FadeOutSeconds;
            return k >= 1f ? 0f : startIntensity * (1f - k);
        }
    }
}
