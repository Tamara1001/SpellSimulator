using UnityEngine;

namespace SpellSimulator.Player
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveForce = 50f;
        [SerializeField] private float maxSpeed = 6f;
        [SerializeField, Range(0f, 1f)] private float friction = 0.175f;
        [SerializeField] private float jumpForce = 5f;
        
        [Header("Physics Settings")]
        [SerializeField] private float customGravity = -15f;
        [SerializeField] private LayerMask groundLayer = Physics.DefaultRaycastLayers;
        [SerializeField, Tooltip("Altura total del jugador para calcular cuando toca el suelo")] 
        private float playerHeight = 2f;

        private Rigidbody rb;
        private PlayerInputReader inputReader;

        private bool isGrounded;
        private bool readyToJump = true;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            inputReader = GetComponent<PlayerInputReader>();

            // Configuracion vital del Rigidbody para FPS
            rb.freezeRotation = true; // Evita que caigamos como un bolo
            rb.useGravity = false;    // Usaremos gravedad matematica personalizada
            rb.mass = 1f;            
            rb.linearDamping = 0f;
        }

        private void Update()
        {
            CheckGrounded();
            HandleJump();
        }

        private void FixedUpdate()
        {
            HandleMovement();
            ApplyCustomGravity();
        }

        private void CheckGrounded()
        {
            // Raycast desde el centro hacia abajo. La mitad de la altura + 0.1f de margen.
            isGrounded = Physics.Raycast(transform.position, Vector3.down, (playerHeight * 0.5f) + 0.1f, groundLayer);
        }

        private void HandleMovement()
        {
            // Direccion
            Vector2 input = inputReader.MoveValue;
            
            // Usar la camara principal como referencia para saber hacia donde estamos mirando
            Transform refTransform = Camera.main != null ? Camera.main.transform : transform;
            
            Vector3 forward = refTransform.forward;
            Vector3 right = refTransform.right;
            
            // Ignorar la inclinacion vertical (Pitch) para no volar ni hundirnos
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 moveDir = right * input.x + forward * input.y;
            moveDir.Normalize();

            // Velocidad actual plana (ignorando la Y de gravedad)
            Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

            // Counter movement (Friccion custom) para frenar rapido cuando soltamos las teclas
            if (input.magnitude < 0.1f)
            {
                rb.AddForce(-flatVel * moveForce * friction, ForceMode.Acceleration);
            }

            // Aplicar fuerza de movimiento
            if (isGrounded)
            {
                rb.AddForce(moveDir * moveForce, ForceMode.Acceleration);
            }
            else
            {
                // En el aire tenemos mucho menos control (20%)
                rb.AddForce(moveDir * moveForce * 0.2f, ForceMode.Acceleration);
            }

            // Limitar velocidad maxima para no acelerar infinitamente
            flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            if (flatVel.magnitude > maxSpeed)
            {
                Vector3 limitedVel = flatVel.normalized * maxSpeed;
                rb.linearVelocity = new Vector3(limitedVel.x, rb.linearVelocity.y, limitedVel.z);
            }
        }

        private void HandleJump()
        {
            if (inputReader.IsJumping && isGrounded && readyToJump)
            {
                readyToJump = false;
                
                // Resetear velocidad Y antes de saltar para que caidas previas no lo anulen
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
                
                Invoke(nameof(ResetJump), 0.25f);
            }
        }

        private void ResetJump()
        {
            readyToJump = true;
        }

        private void ApplyCustomGravity()
        {
            rb.AddForce(Vector3.up * customGravity, ForceMode.Acceleration);
        }
    }
}
