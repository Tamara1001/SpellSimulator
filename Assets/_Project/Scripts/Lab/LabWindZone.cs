using System;
using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Output;
using SpellSimulator.Reactions;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Zona de viento del laboratorio: una caja de 4×3×4 m apoyada sobre su transform (la base en el pivote) que empuja con
    /// arrastre cuadratico (<see cref="WindMath.Drag"/>) a los Rigidbody no cinematicos que tiene adentro, sesga el fuego del
    /// pasto hacia donde sopla y avisa por <see cref="Changed"/> para que <see cref="LabWindVisual"/> lo dibuje (estelas, hojas,
    /// polvo, chevrones y manga). Expira a los 30 s.
    /// No es un <see cref="MagicField"/> ni fabrica un SpellIR (todo cast pasa por SpellCaster.Cast): no apaga velas ni lo ve
    /// Flutter (LABORATORIO_PLAN riesgo 13).
    /// Direccion: <see cref="YawDeg"/> es una rotacion alrededor del eje Y del mundo aplicada al eje +Z del mundo
    /// (0° = hacia +Z, 90° = hacia +X), independiente de la rotacion del transform.
    /// </summary>
    public class LabWindZone : MonoBehaviour
    {
        /// <summary>Medio tamaño de la caja de viento (m): 4×3×4.</summary>
        private static readonly Vector3 HalfExtents = new Vector3(2f, 1.5f, 2f);
        private const int MaxOverlaps = 32;
        /// <summary>Cada cuanto se refresca el sesgo del fuego (s); GrassFire lo olvida a los 0,4 s. Igual que WindCone.BiasInterval.</summary>
        private const float BiasInterval = 0.1f;
        /// <summary>Velocidad a la que el sesgo del fuego es pleno (m/s). Igual que WindCone.FullBiasSpeed.</summary>
        private const float FullBiasSpeed = 10f;
        /// <summary>Radio del sesgo del fuego (m): la media diagonal de la caja.</summary>
        private const float BiasRadius = 2.9f;
        /// <summary>Area (m2) si el collider no tiene bounds utiles (como MagicField.WindDrag).</summary>
        private const float FallbackArea = 0.5f;

        private static readonly List<LabWindZone> all = new List<LabWindZone>(4);

        /// <summary>Una zona aparecio, se reconfiguro o desaparecio (el visual vuelve a elegir cual seguir).</summary>
        public static event Action Changed;

        [SerializeField, Tooltip("Segundos de vida antes de desaparecer.")]
        private float lifetime = 30f;
        [SerializeField, Tooltip("Direccion inicial (grados sobre Y desde +Z del mundo).")]
        private float yawDeg;
        [SerializeField, Tooltip("Velocidad inicial del viento (m/s).")]
        private float speed = 6f;

        private readonly Collider[] overlaps = new Collider[MaxOverlaps];
        private readonly Rigidbody[] pushed = new Rigidbody[MaxOverlaps];
        private Vector3 direction = Vector3.forward;
        private float expireAt, nextBias;
        private Material particleMaterial;

        /// <summary>Zonas de viento activas (lista preasignada; alta en OnEnable, baja en OnDisable).</summary>
        public static IReadOnlyList<LabWindZone> All => all;

        /// <summary>Tamaño de la caja de viento (m), con la base en el pivote.</summary>
        public static Vector3 Size => HalfExtents * 2f;

        /// <summary>Material de particulas pedido por el spawner (lo usa el visual si no hay look de viento); puede ser null.</summary>
        public Material ParticleMaterial => particleMaterial;

        /// <summary>Direccion del viento: grados sobre el eje Y del mundo, desde +Z.</summary>
        public float YawDeg => yawDeg;

        /// <summary>Velocidad del viento (m/s).</summary>
        public float Speed => speed;

        /// <summary>Centro de la caja en el mundo (la base esta en el pivote).</summary>
        public Vector3 Center => transform.position + Vector3.up * HalfExtents.y;

        /// <summary>Direccion horizontal unitaria hacia donde sopla.</summary>
        public Vector3 Direction => direction;

        // Con Enter Play Mode sin recarga de dominio la lista estatica sobreviviria entre sesiones de Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => all.Clear();

        private void Awake()
        {
            expireAt = Time.time + lifetime;
            direction = DirectionFromYaw(yawDeg);
        }

        private void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
            Changed?.Invoke();
        }

        private void OnDisable()
        {
            all.Remove(this);
            Changed?.Invoke();
        }

        /// <summary>Material de particulas de respaldo para el visual (solo se usa si no hay look de viento).</summary>
        public void SetParticleMaterial(Material material) => particleMaterial = material;

        /// <summary>Fija direccion (grados sobre Y desde +Z del mundo) y velocidad (m/s, ≥ 0) y reinicia la vida.</summary>
        public void Configure(float yawDeg, float speed)
        {
            this.yawDeg = Mathf.Repeat(yawDeg, 360f);
            this.speed = Mathf.Max(0f, speed);
            direction = DirectionFromYaw(this.yawDeg);
            expireAt = Time.time + lifetime;
            Changed?.Invoke();
        }

        /// <summary>Rotacion de +Z del mundo alrededor de Y.</summary>
        public static Vector3 DirectionFromYaw(float yawDeg) => Quaternion.Euler(0f, yawDeg, 0f) * Vector3.forward;

        private void FixedUpdate()
        {
            if (Time.time >= expireAt)
            {
                Destroy(gameObject);
                return;
            }
            if (speed <= 0f) return;

            Vector3 center = Center;
            Vector3 windVelocity = direction * speed;
            int count = Physics.OverlapBoxNonAlloc(center, HalfExtents, overlaps, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            int pushedCount = 0;
            for (int i = 0; i < count; i++)
            {
                Collider col = overlaps[i];
                overlaps[i] = null;
                if (col == null) continue;
                Rigidbody rb = col.attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;
                // Un cuerpo con varios colliders se empuja una sola vez.
                bool seen = false;
                for (int j = 0; j < pushedCount; j++) if (pushed[j] == rb) { seen = true; break; }
                if (seen) continue;
                pushed[pushedCount++] = rb;

                float area = WindMath.ProjectedArea(col.bounds.size);
                if (area < 0.01f) area = FallbackArea;
                // La masa grande casi no se mueve: eso lo da Drag (a = F / m).
                Vector3 a = WindMath.Drag(windVelocity, rb.linearVelocity, rb.mass, area, WindMath.DefaultCd, MagicField.MaxAcceleration);
                rb.AddForce(a, ForceMode.Acceleration);
            }
            for (int j = 0; j < pushedCount; j++) pushed[j] = null;

            if (Time.time >= nextBias)
            {
                nextBias = Time.time + BiasInterval;
                GrassFire grass = GrassFire.Current;
                if (grass != null) grass.AddWindBias(center, direction, Mathf.Clamp01(speed / FullBiasSpeed), BiasRadius);
            }
        }
    }
}
