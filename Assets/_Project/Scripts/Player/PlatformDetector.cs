using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace SpellSimulator.Player
{
    /// <summary>
    /// Detecta si hay un headset VR activo al inicio, activa el rig correspondiente y maneja el cursor.
    /// Es el UNICO lugar del proyecto con logica de plataforma: el resto lee IsVR.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PlatformDetector : MonoBehaviour
    {
        [Header("Rigs")]
        [SerializeField, Tooltip("GameObject con PlayerLook y la camara de PC.")]
        private GameObject pcRig;
        [SerializeField, Tooltip("GameObject con el XR Origin y sus controladores.")]
        private GameObject vrRig;

        /// <summary>True si el juego corre en un headset VR.</summary>
        public static bool IsVR { get; private set; }

        private void Awake()
        {
            // Con Quest Link en el Editor, XRSettings.isDeviceActive puede ser false en Awake:
            // lo robusto es preguntar si XR Management termino de inicializar.
            var xr = UnityEngine.XR.Management.XRGeneralSettings.Instance;
            bool xrInitialized = xr != null && xr.Manager != null && xr.Manager.isInitializationComplete;
            IsVR = xrInitialized || XRSettings.isDeviceActive;

            if (pcRig != null) pcRig.SetActive(!IsVR);
            if (vrRig != null) vrRig.SetActive(IsVR);
            SetCursorLocked(!IsVR);

            Debug.Log($"[PlatformDetector] Modo: {(IsVR ? "VR" : "PC")}");
        }

        private void Update()
        {
            if (IsVR || Keyboard.current == null) return;

            // PC: Escape libera el cursor (para el Inspector o salir), click lo vuelve a capturar.
            if (Keyboard.current.escapeKey.wasPressedThisFrame) SetCursorLocked(false);
            else if (Cursor.lockState != CursorLockMode.Locked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                SetCursorLocked(true);
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked && !IsVR;
        }

        /// <summary>True si el jugador de PC tiene el cursor capturado (vista FPS activa).</summary>
        public static bool IsLookCaptured => IsVR || Cursor.lockState == CursorLockMode.Locked;
    }
}
