using System;
using UnityEngine;

namespace SpellSimulator.Player
{
    /// <summary>
    /// Interruptor «sin daño» del modo Libre (D28, D33): se desbloquea la primera vez que se usa la palanca del laboratorio
    /// y queda guardado en PlayerPrefs entre partidas. Lo leen PlayerVitality y la palanca (NoDamageLever).
    /// </summary>
    public static class NoDamageSetting
    {
        public const string UnlockedKey = "libre.nodamage.unlocked";
        public const string OnKey = "libre.nodamage.on";

        /// <summary>Cambió el estado (encendido o desbloqueado).</summary>
        public static event Action Changed;

        public static bool Unlocked => PlayerPrefs.GetInt(UnlockedKey, 0) == 1;
        public static bool On => PlayerPrefs.GetInt(OnKey, 0) == 1;

        /// <summary>Enciende o apaga el interruptor (y lo desbloquea si era la primera vez).</summary>
        public static void Set(bool on)
        {
            PlayerPrefs.SetInt(UnlockedKey, 1);
            PlayerPrefs.SetInt(OnKey, on ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void Toggle() => Set(!On);

        /// <summary>Vuelve al estado de fábrica (bloqueado y apagado). Para el harness.</summary>
        public static void ResetForTests()
        {
            PlayerPrefs.DeleteKey(UnlockedKey);
            PlayerPrefs.DeleteKey(OnKey);
            Changed?.Invoke();
        }
    }
}
