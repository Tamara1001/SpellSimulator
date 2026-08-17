using UnityEngine;
using UnityEngine.XR;

namespace SpellSimulator.Player
{
    /// <summary>
    /// Detecta si hay un headset VR activo al inicio y activa el Rig correcto.
    /// Toda la lógica de plataforma vive exclusivamente en este script.
    /// Sin if/else de plataforma en ningún otro lugar del proyecto.
    /// </summary>
    public class PlatformDetector : MonoBehaviour
    {
        [Header("Rigs")]
        [SerializeField, Tooltip("El GameObject que contiene PlayerLook y la cámara de PC.")]
        private GameObject pcRig;

        [SerializeField, Tooltip("El GameObject que contiene el XR Origin con sus controladores.")]
        private GameObject vrRig;

        /// <summary>True si el juego está corriendo en un headset VR.</summary>
        public static bool IsVR { get; private set; }

        private void Awake()
        {
            // En el Editor con Quest Link, XRSettings.isDeviceActive a veces devuelve false en Awake.
            // La forma más robusta en Unity moderno es chequear si el XR Management se inicializó.
            var xrSettings = UnityEngine.XR.Management.XRGeneralSettings.Instance;
            bool xrInitialized = xrSettings != null && xrSettings.Manager != null && xrSettings.Manager.isInitializationComplete;
            
            IsVR = xrInitialized || XRSettings.isDeviceActive;

            pcRig.SetActive(!IsVR);
            vrRig.SetActive(IsVR);

            // El cursor bloqueado solo tiene sentido en PC (FPS).
            // En VR no existe cursor.
            if (!IsVR)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = false;
            }

            Debug.Log($"[PlatformDetector] Modo: {(IsVR ? "VR" : "PC")}");
        }
    }
}
