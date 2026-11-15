using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Output;
using SpellSimulator.Reactions;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Zona de viento del laboratorio: una caja de 4×3×4 m apoyada sobre su transform (la base en el pivote) que empuja con
    /// arrastre cuadratico (<see cref="WindMath.Drag"/>) a los Rigidbody no cinematicos que tiene adentro, sesga el fuego del
    /// pasto hacia donde sopla y muestra rayas en la direccion del viento. Expira a los 30 s.
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
        /// <summary>Largo del recorrido visible de cada raya (m).</summary>
        private const float StreakTravel = 4f;

        private static readonly List<LabWindZone> all = new List<LabWindZone>(4);
        private static Material fallbackParticleMaterial;

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
        private ParticleSystem streaks;
        private Material particleMaterial;

        /// <summary>Zonas de viento activas (lista preasignada; alta en OnEnable, baja en OnDisable).</summary>
        public static IReadOnlyList<LabWindZone> All => all;

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
        }

        private void OnDisable() => all.Remove(this);

        private void Start()
        {
            if (streaks == null) BuildStreaks();
            UpdateStreaks();
        }

        /// <summary>Material de las rayas; llamarlo antes de <see cref="Configure"/> (si no, se usa URP Particles/Unlit).</summary>
        public void SetParticleMaterial(Material material)
        {
            particleMaterial = material;
            if (streaks != null && material != null) streaks.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        }

        /// <summary>Fija direccion (grados sobre Y desde +Z del mundo) y velocidad (m/s, ≥ 0) y reinicia la vida.</summary>
        public void Configure(float yawDeg, float speed)
        {
            this.yawDeg = Mathf.Repeat(yawDeg, 360f);
            this.speed = Mathf.Max(0f, speed);
            direction = DirectionFromYaw(this.yawDeg);
            expireAt = Time.time + lifetime;
            if (streaks == null) BuildStreaks();
            UpdateStreaks();
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

        // ---------------------------------------------------------------- rayas (solo al crear o reconfigurar)

        private void BuildStreaks()
        {
            var go = new GameObject("Rayas");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            streaks = go.AddComponent<ParticleSystem>();
            streaks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = streaks.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSize = 0.06f;
            main.startColor = new Color(0.92f, 0.96f, 1f, 0.7f);
            main.maxParticles = 120;
            main.gravityModifier = 0f;

            ParticleSystem.ShapeModule shape = streaks.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(HalfExtents.x * 2f, HalfExtents.y * 1.6f, 0.1f);

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Stretch;
            psr.lengthScale = 6f;
            psr.velocityScale = 0.05f;
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psr.receiveShadows = false;
            Material material = particleMaterial != null ? particleMaterial : FallbackMaterial();
            if (material != null) psr.sharedMaterial = material;
            else psr.enabled = false; // sin shader de particulas en el build: el viento funciona igual, sin rayas
        }

        private void UpdateStreaks()
        {
            if (streaks == null) return;
            // Emisor en la cara de barlovento, mirando hacia donde sopla.
            Transform t = streaks.transform;
            t.rotation = Quaternion.LookRotation(direction, Vector3.up);
            t.position = Center - direction * HalfExtents.x;

            float s = Mathf.Max(speed, 0.5f);
            ParticleSystem.MainModule main = streaks.main;
            main.startSpeed = speed;
            main.startLifetime = Mathf.Clamp(StreakTravel / s, 0.2f, 4f);
            ParticleSystem.EmissionModule emission = streaks.emission;
            emission.rateOverTime = speed > 0f ? Mathf.Lerp(8f, 40f, Mathf.Clamp01(speed / 20f)) : 0f;

            if (!streaks.gameObject.activeSelf) streaks.gameObject.SetActive(true);
            if (!streaks.isPlaying) streaks.Play(true);
        }

        private static Material FallbackMaterial()
        {
            if (fallbackParticleMaterial != null) return fallbackParticleMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return null;
            fallbackParticleMaterial = new Material(shader) { name = "LabWindStreaks" };
            return fallbackParticleMaterial;
        }
    }
}
