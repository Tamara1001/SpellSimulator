using UnityEngine;

namespace SpellSimulator.Player
{
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerLook : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("Transform de la camara (o el pivot de Cinemachine)")]
        private Transform cameraTransform;

        [Header("Settings")]
        [SerializeField] private float lookSensitivity = 0.2f; // Sensibilidad base bajada aun mas
        [SerializeField, Tooltip("Multiplicador de sensibilidad al dibujar para mayor precision (estilo Sniper ADS)")] 
        private float drawingSensitivityMultiplier = 0.3f;
        [SerializeField] private float topClamp = 85f;
        [SerializeField] private float bottomClamp = -85f;

        private PlayerInputReader inputReader;
        
        // Almacena la rotacion vertical (Pitch)
        private float xRotation = 0f;

        private void Awake()
        {
            inputReader = GetComponent<PlayerInputReader>();
            
            // Bloquear el cursor en el centro y ocultarlo para una experiencia FPS real
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            // En VR, el tracking del casco mueve la camara directamente.
            // Si corremos PlayerLook en VR, pelearia contra el XR Origin y daria resultados raros.
            if (PlatformDetector.IsVR) return;

            if (cameraTransform == null)
            {
                Debug.LogWarning("[PlayerLook] Falta asignar el CameraTransform en el inspector.");
                return;
            }

            // Si estamos dibujando, bajamos la sensibilidad automaticamente para ser precisos
            float currentSens = inputReader.IsUsingTool ? (lookSensitivity * drawingSensitivityMultiplier) : lookSensitivity;

            // Obtener el delta del mouse.
            // OJO: Multiplicar el delta del raton por Time.deltaTime es un error comun
            // que causa latencia y tirones, ya que el delta del mouse ya cambia con el framerate.
            Vector2 look = inputReader.LookValue * currentSens;

            // 1. Yaw (Rotacion horizontal): Rotamos todo el cuerpo del jugador
            transform.Rotate(Vector3.up * look.x);

            // 2. Pitch (Rotacion vertical): Rotamos solo la cabeza/camara
            xRotation -= look.y;
            xRotation = Mathf.Clamp(xRotation, bottomClamp, topClamp);

            cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }
    }
}
