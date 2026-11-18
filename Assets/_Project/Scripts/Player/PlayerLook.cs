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

        /// <summary>Grados de cada giro a saltos en VR.</summary>
        public const float VrSnapTurnDegrees = 45f;

        private PlayerInputReader inputReader;
        private ObjectMover objectMover;
        private PlayerInteractor interactor;
        private Camera mainCamera;
        private Rigidbody body;
        private float baseFov;
        private float currentFov;
        private float pitch;

        private void Awake()
        {
            inputReader = GetComponent<PlayerInputReader>();
            objectMover = GetComponent<ObjectMover>();
            interactor = GetComponent<PlayerInteractor>();
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
            if (PlatformDetector.IsVR)
            {
                HandleSnapTurn();
                return;
            }
            UpdateZoom();

            if (cameraTransform == null || !PlatformDetector.IsLookCaptured) return;
            // Clic derecho + G: el mouse rota el objeto sostenido y la vista queda quieta.
            if (objectMover != null && objectMover.IsRotating) return;
            // Manipulando una perilla o deslizador: el mouse mueve el control y la vista queda quieta.
            if (interactor != null && interactor.IsManipulating) return;

            float sensitivity = inputReader.IsUsingTool ? lookSensitivity * drawingSensitivityMultiplier : lookSensitivity;
            // Con zoom la sensibilidad baja en la misma proporcion que el campo de vision (mas precision).
            if (mainCamera != null && baseFov > 0f) sensitivity *= currentFov / baseFov;
            // Opcion del jugador (0.3 a 2.5), guardada en PlayerPrefs.
            sensitivity *= PlayerSettings.LookSensitivity;
            // El delta del mouse ya depende del framerate: multiplicarlo por deltaTime agrega latencia.
            Vector2 look = inputReader.LookValue * sensitivity;

            transform.Rotate(Vector3.up * look.x);
            // Mouse hacia arriba mira hacia arriba (signo invertido respecto del delta); InvertY lo da vuelta.
            float pitchSign = PlayerSettings.InvertY ? 1f : -1f;
            pitch = Mathf.Clamp(pitch + pitchSign * look.y, bottomClamp, topClamp);
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        /// <summary>
        /// VR: golpe del stick derecho a un costado = giro a saltos alrededor de la cabeza (cómodo, sin marear). Si la mano lleva algo
        /// con paginas, ese mismo golpe pasa la pagina (lo hace el interactor).
        /// </summary>
        private void HandleSnapTurn()
        {
            int flick = inputReader.VrFlickX;
            if (flick == 0 || (interactor != null && (interactor.HoldsPageable || interactor.IsManipulating))) return;
            Camera cam = Camera.main;
            Vector3 pivot = cam != null ? cam.transform.position : transform.position;
            transform.RotateAround(pivot, Vector3.up, flick * VrSnapTurnDegrees);
            // Con interpolacion el Rigidbody pisa el transform en el cuadro siguiente: se le pasa la nueva pose.
            if (body == null) body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.position = transform.position;
                body.rotation = transform.rotation;
                body.angularVelocity = Vector3.zero;
            }
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
