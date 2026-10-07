using UnityEngine;

namespace SpellSimulator.Player
{
    /// <summary>Vista FPS para PC. En VR no hace nada: el tracking del casco mueve la camara.</summary>
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerLook : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("Transform de la camara (o el pivot de Cinemachine).")]
        private Transform cameraTransform;

        [Header("Settings")]
        [SerializeField] private float lookSensitivity = 0.2f;
        [SerializeField, Tooltip("Multiplicador de sensibilidad mientras se dibuja, para mas precision.")]
        private float drawingSensitivityMultiplier = 0.6f;
        [SerializeField] private float topClamp = 85f;
        [SerializeField] private float bottomClamp = -85f;

        private PlayerInputReader inputReader;
        private float pitch;

        private void Awake()
        {
            inputReader = GetComponent<PlayerInputReader>();
            if (cameraTransform == null) Debug.LogWarning("[PlayerLook] Falta asignar el CameraTransform.", this);
        }

        private void Update()
        {
            if (PlatformDetector.IsVR || cameraTransform == null || !PlatformDetector.IsLookCaptured) return;

            float sensitivity = inputReader.IsUsingTool ? lookSensitivity * drawingSensitivityMultiplier : lookSensitivity;
            // El delta del mouse ya depende del framerate: multiplicarlo por deltaTime agrega latencia.
            Vector2 look = inputReader.LookValue * sensitivity;

            transform.Rotate(Vector3.up * look.x);
            pitch = Mathf.Clamp(pitch - look.y, bottomClamp, topClamp);
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
