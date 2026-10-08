using System;
using UnityEngine;

namespace SpellSimulator.Learning
{
    /// <summary>
    /// Opciones que se cambian desde el grimorio y no son del jugador (esas viven en Player.PlayerSettings): volumen
    /// general, de musica y de efectos, guardados en PlayerPrefs. El general se aplica en el acto con
    /// <see cref="AudioListener.volume"/>; musica y efectos los debe leer el director de audio (<see cref="VolumesChanged"/>).
    /// Los pasos y formatos son logica pura (tests EditMode).
    /// </summary>
    public static class GrimoireOptions
    {
        public const string MasterKey = "volMaster";
        public const string MusicKey = "volMusic";
        public const string EffectsKey = "volSfx";

        /// <summary>Paso de los botones - y + del volumen (10 %).</summary>
        public const float VolumeStep = 0.1f;
        /// <summary>Paso y rango de la sensibilidad del mouse (igual que PlayerSettings.LookSensitivity).</summary>
        public const float SensitivityStep = 0.1f, SensitivityMin = 0.3f, SensitivityMax = 2.5f;

        /// <summary>Algun volumen cambio (categoria: 0 general, 1 musica, 2 efectos).</summary>
        public static event Action<int> VolumesChanged;

        private static float master = -1f, music = -1f, effects = -1f;

        public static float MasterVolume
        {
            get => Load(ref master, MasterKey, 1f);
            set { Save(ref master, MasterKey, value); ApplyMaster(); VolumesChanged?.Invoke(0); }
        }

        public static float MusicVolume
        {
            get => Load(ref music, MusicKey, 1f);
            set { Save(ref music, MusicKey, value); VolumesChanged?.Invoke(1); }
        }

        public static float EffectsVolume
        {
            get => Load(ref effects, EffectsKey, 1f);
            set { Save(ref effects, EffectsKey, value); VolumesChanged?.Invoke(2); }
        }

        /// <summary>Aplica el volumen general guardado al oyente (al arrancar el juego).</summary>
        public static void ApplyMaster() => AudioListener.volume = MasterVolume;

        // ---------------------------------------------------------------- logica pura

        /// <summary>Un paso de volumen hacia arriba (dir &gt; 0) o abajo, redondeado a decimas y limitado a 0..1.</summary>
        public static float StepVolume(float value, int dir)
        {
            float next = Mathf.Round((value + Math.Sign(dir) * VolumeStep) * 10f) / 10f;
            return Mathf.Clamp01(next);
        }

        /// <summary>Un paso de sensibilidad, redondeado a decimas y limitado a 0.3..2.5.</summary>
        public static float StepSensitivity(float value, int dir)
        {
            float next = Mathf.Round((value + Math.Sign(dir) * SensitivityStep) * 10f) / 10f;
            return Mathf.Clamp(next, SensitivityMin, SensitivityMax);
        }

        /// <summary>"80 %" (porcentaje entero).</summary>
        public static string Percent(float value) => Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + " %";

        /// <summary>"1,2x" (sensibilidad con una decimal, coma decimal).</summary>
        public static string Multiplier(float value)
        {
            int tenths = Mathf.RoundToInt(value * 10f);
            return (tenths / 10) + "," + (tenths % 10) + "x";
        }

        private static float Load(ref float cache, string key, float fallback)
        {
            if (cache < 0f) cache = Mathf.Clamp01(PlayerPrefs.GetFloat(key, fallback));
            return cache;
        }

        private static void Save(ref float cache, string key, float value)
        {
            cache = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(key, cache);
        }
    }
}
