using System;
using UnityEngine;

namespace SpellSimulator.Player
{
    /// <summary>
    /// Opciones del jugador que viven entre sesiones (PlayerPrefs). Mano principal: la que lleva lo de la barra de objetos
    /// (lapiz, goma, estampas). Lo agarrado con E (la mano secundaria) se arrastra en el mundo.
    /// </summary>
    public static class PlayerSettings
    {
        private const string MainHandKey = "mainHandRight";
        private const string SensitivityKey = "lookSensitivity";
        private const string InvertYKey = "invertY";

        /// <summary>Cualquier opcion cambio (el interactor y la HUD lo escuchan).</summary>
        public static event Action Changed;

        private static int mainHandRight = -1;
        private static float sensitivity = -1f;
        private static int invertY = -1;

        /// <summary>True si la mano principal es la derecha (por defecto).</summary>
        public static bool MainHandRight
        {
            get
            {
                if (mainHandRight < 0) mainHandRight = PlayerPrefs.GetInt(MainHandKey, 1);
                return mainHandRight == 1;
            }
            set
            {
                mainHandRight = value ? 1 : 0;
                PlayerPrefs.SetInt(MainHandKey, mainHandRight);
                Changed?.Invoke();
            }
        }

        /// <summary>Multiplicador de la sensibilidad del mouse (0.3 a 2.5).</summary>
        public static float LookSensitivity
        {
            get
            {
                if (sensitivity < 0f) sensitivity = PlayerPrefs.GetFloat(SensitivityKey, 1f);
                return sensitivity;
            }
            set
            {
                sensitivity = Mathf.Clamp(value, 0.3f, 2.5f);
                PlayerPrefs.SetFloat(SensitivityKey, sensitivity);
                Changed?.Invoke();
            }
        }

        public static bool InvertY
        {
            get
            {
                if (invertY < 0) invertY = PlayerPrefs.GetInt(InvertYKey, 0);
                return invertY == 1;
            }
            set
            {
                invertY = value ? 1 : 0;
                PlayerPrefs.SetInt(InvertYKey, invertY);
                Changed?.Invoke();
            }
        }
    }
}
