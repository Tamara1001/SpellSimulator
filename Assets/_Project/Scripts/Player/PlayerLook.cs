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

        [Header("Zoom (X mantenido, PC)")]
        [SerializeField, Tooltip("Campo de vision (grados) mientras se mantiene el zoom.")]
        private float zoomFov = 22f;
        [SerializeField, Tooltip("Velocidad (grados/s) con que el campo de vision baja y vuelve.")]
        private float zoomSpeed = 160f;

        private PlayerInputReader inputReader;
        private ObjectMover objectMover;
        private Camera mainCamera;
        private float baseFov;
        private float currentFov;
        private float pitch;

        private void Awake()
        {
            inputReader = GetComponent<PlayerInputReader>();
            objectMover = GetComponent<ObjectMover>();
            mainCamera = Camera.main;
            if (mainCamera != null)
            {
                baseFov = mainCamera.fieldOfView;
                currentFov = baseFov;
            }
            if (cameraTransform == null) Debug.LogWarning("[PlayerLook] Falta asignar el CameraTransform.", this);
        }

        private void Update()
        {
            if (PlatformDetector.IsVR) return;
            UpdateZoom();

            if (cameraTransform == null || !PlatformDetector.IsLookCaptured) return;
            // Clic derecho + G: el mouse rota el objeto sostenido y la vista queda quieta.
            if (objectMover != null && objectMover.IsRotating) return;

            float sensitivity = inputReader.IsUsingTool ? lookSensitivity * drawingSensitivityMultiplier : lookSensitivity;
            // Con zoom la sensibilidad baja en la misma proporcion que el campo de vision (mas precision).
            if (mainCamera != null && baseFov > 0f) sensitivity *= currentFov / baseFov;
            // El delta del mouse ya depende del framerate: multiplicarlo por deltaTime agrega latencia.
            Vector2 look = inputReader.LookValue * sensitivity;

            transform.Rotate(Vector3.up * look.x);
            pitch = Mathf.Clamp(pitch - look.y, bottomClamp, topClamp);
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        /// <summary>Baja el campo de vision mientras se mantiene X y lo devuelve al soltar. Solo PC.</summary>
        private void UpdateZoom()
        {
            if (mainCamera == null || baseFov <= 0f) return;
            float target = inputReader.IsZooming ? zoomFov : baseFov;
            currentFov = Mathf.MoveTowards(currentFov, target, zoomSpeed * Time.deltaTime);
            mainCamera.fieldOfView = currentFov;
        }
    }
}
