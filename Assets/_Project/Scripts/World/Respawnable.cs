using UnityEngine;
using SpellSimulator.World.Map;

namespace SpellSimulator.World
{
    /// <summary>
    /// Devuelve el objeto a su pose inicial si cae fuera del mundo o si se pide un reinicio (modo Libre).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Respawnable : MonoBehaviour
    {
        [SerializeField, Tooltip("Altura (m) por debajo de la cual el objeto vuelve a su lugar.")]
        private float killHeight = -8f;

        // Revision barata (cada 0.5 s, sin allocations) de objetos que atravesaron el terreno o se alejaron demasiado.
        private const float CheckPeriod = 0.5f;
        private const float MaxDistance = 150f;
        private const float TowerRadius = 7f;
        private const float GroundMargin = 1.5f;

        private static readonly System.Collections.Generic.List<Interfaces.IResettable> resettables = new System.Collections.Generic.List<Interfaces.IResettable>();

        private Rigidbody rb;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private float checkTimer;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            // Deteccion continua: los objetos chicos y rapidos (lapices, piedras) no atraviesan el piso al chocar fuerte.
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            startPosition = transform.position;
            startRotation = transform.rotation;
        }

        private void FixedUpdate()
        {
            if (rb.position.y < killHeight)
            {
                Respawn("cayo por debajo de la altura minima");
                return;
            }
            checkTimer += Time.fixedDeltaTime;
            if (checkTimer < CheckPeriod) return;
            checkTimer = 0f;
            if (IsLost(rb.position)) Respawn("se considera perdido (lejos del origen o bajo el terreno)");
        }

        /// <summary>
        /// Perdido: a mas de MaxDistance del origen, o por debajo del terreno fuera de la torre (dentro de la torre el piso
        /// esta por encima del terreno, asi que ahi no se compara).
        /// </summary>
        private static bool IsLost(Vector3 p)
        {
            if (p.sqrMagnitude > MaxDistance * MaxDistance) return true;
            float dx = p.x - ValleyShape.TowerCenter.x;
            float dz = p.z - ValleyShape.TowerCenter.y;
            if (dx * dx + dz * dz <= TowerRadius * TowerRadius) return false;
            return p.y < ValleyShape.Height(p.x, p.z) - GroundMargin;
        }

        public void Respawn(string reason = "reinicio del mundo")
        {
            Debug.Log($"[Respawnable] {name} vuelve a su lugar: {reason} (pos {rb.position})", this);
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.position = startPosition;
            rb.rotation = startRotation;
            transform.SetPositionAndRotation(startPosition, startRotation);
            // Todos los componentes con estado propio (lienzo, cuaderno con paginas, estampa...).
            GetComponents(resettables);
            for (int i = 0; i < resettables.Count; i++) resettables[i].ResetToInitial();
        }

        /// <summary>
        /// Reinicia los objetos reposicionables de la escena. Los cinematicos (herramientas en la mano, el jugador)
        /// no se tocan: el reinicio no te saca lo que tenes agarrado.
        /// </summary>
        public static void RespawnAll()
        {
            foreach (var r in FindObjectsByType<Respawnable>(FindObjectsSortMode.None))
            {
                if (r.rb == null || IsHeldByPlayer(r.transform)) continue;
                r.Respawn();
            }
        }

        /// <summary>Lo que cuelga de la camara o del jugador (manos, herramienta en la mano) no se reinicia.</summary>
        private static bool IsHeldByPlayer(Transform t) => Player.PlayerHeld.IsAttachedToAnyPlayer(t);
    }
}
