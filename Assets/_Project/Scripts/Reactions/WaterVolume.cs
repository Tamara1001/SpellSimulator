using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Compiler;
using SpellSimulator.Output;

namespace SpellSimulator.Reactions
{
    /// <summary>
    /// Cuerpo de agua (lago, estanque). Un BoxCollider trigger define el volumen y la superficie es su cara de arriba.
    /// - Flotacion: empuje segun la parte sumergida y la flotabilidad del material; arrastre del agua.
    /// - Ondas y salpicaduras al caer objetos o al tocarla un hechizo; vapor si es fuego.
    /// - Succion de agua (canon: el pull atrae su mismo elemento): un hechizo de agua con pull cerca tira un chorro desde
    ///   la superficie hacia el sello.
    /// Lo que esta adentro se moja (MagicWorld). Sin GC por frame.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class WaterVolume : MonoBehaviour
    {
        public static readonly List<WaterVolume> All = new List<WaterVolume>();

        private const int MaxRipples = 16;
        private const float MinIntakeLifetime = 0.45f;
        private const int MaxBodies = 64;
        private static readonly int RipplesId = Shader.PropertyToID("_Ripples");
        private static readonly int RippleCountId = Shader.PropertyToID("_RippleCount");

        [SerializeField, Tooltip("Renderer de la superficie (material StylizedWater).")]
        private Renderer surface;
        [SerializeField, Tooltip("Looks de los elementos (salpicaduras, vapor).")]
        private SpellOutputController output;

        [Header("Fisica")]
        [SerializeField, Tooltip("Flotabilidad de lo que no tiene material (agua/densidad).")]
        private float defaultBuoyancy = 1.2f;
        [SerializeField, Tooltip("Arrastre del agua (1/s) sobre lo sumergido.")]
        private float waterDrag = 2.5f;
        [SerializeField, Tooltip("Velocidad minima de entrada para salpicar (m/s).")]
        private float splashSpeed = 1.2f;

        [Header("Hechizos")]
        [SerializeField, Tooltip("Cada cuanto se revisa si un hechizo toca la superficie (s).")]
        private float spellProbeInterval = 0.15f;
        [SerializeField, Tooltip("Velocidad del agua succionada (m/s).")]
        private float intakeSpeed = 5f;

        private BoxCollider volume;
        private readonly Rigidbody[] bodies = new Rigidbody[MaxBodies];
        private readonly Collider[] bodyColliders = new Collider[MaxBodies];
        private int bodyCount;
        private readonly Vector4[] ripples = new Vector4[MaxRipples];
        private int nextRipple;
        private MaterialPropertyBlock block;
        private ParticleSystem splash, steam, stream;
        private float nextProbe;

        public float SurfaceY => volume.bounds.max.y;
        public Bounds Bounds => volume.bounds;

        private void Awake()
        {
            volume = GetComponent<BoxCollider>();
            volume.isTrigger = true;
            block = new MaterialPropertyBlock();
            for (int i = 0; i < MaxRipples; i++) ripples[i] = new Vector4(0f, 0f, -100f, 0f);
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        private void Start()
        {
            if (output == null) return;
            var water = output.LookFor(ElementType.Water);
            var wind = output.LookFor(ElementType.Wind);
            if (water != null) splash = WaterEffects.Create("Splash", transform, water, 0.6f, 1.5f);
            if (wind != null) steam = WaterEffects.Create("Steam", transform, wind, 1.2f, -0.3f);
            if (water != null) stream = WaterEffects.Create("Intake", transform, water, 0.8f, 0f);
        }

        /// <summary>True si el punto esta debajo de la superficie, dentro del volumen.</summary>
        public bool Contains(Vector3 point) => volume.bounds.Contains(point);

        /// <summary>Fraccion (0-1) de unos bounds que esta bajo la superficie.</summary>
        public float Submerged(Bounds b)
        {
            Bounds water = volume.bounds;
            if (b.max.x < water.min.x || b.min.x > water.max.x || b.max.z < water.min.z || b.min.z > water.max.z) return 0f;
            return Mathf.Clamp01((SurfaceY - b.min.y) / Mathf.Max(b.size.y, 1e-3f));
        }

        public void AddRipple(Vector3 at, float strength)
        {
            ripples[nextRipple] = new Vector4(at.x, at.z, Time.timeSinceLevelLoad, Mathf.Clamp01(strength));
            nextRipple = (nextRipple + 1) % MaxRipples;
            if (surface == null) return;
            surface.GetPropertyBlock(block);
            block.SetVectorArray(RipplesId, ripples);
            block.SetFloat(RippleCountId, MaxRipples);
            surface.SetPropertyBlock(block);
        }

        public void Splash(Vector3 at, float strength)
        {
            at.y = SurfaceY;
            AddRipple(at, strength);
            WaterEffects.Burst(splash, at, Mathf.RoundToInt(12f + 30f * strength), 1.5f + 2f * strength);
        }

        // ---------------------------------------------------------------- cuerpos

        private void OnTriggerEnter(Collider other)
        {
            var rb = other.attachedRigidbody;
            if (rb == null || rb.isKinematic || bodyCount >= MaxBodies) return;
            for (int i = 0; i < bodyCount; i++) if (bodies[i] == rb) return;
            bodies[bodyCount] = rb;
            bodyColliders[bodyCount] = other;
            bodyCount++;
            float speed = -rb.linearVelocity.y;
            if (speed > splashSpeed) Splash(rb.worldCenterOfMass, Mathf.Clamp01(speed / 8f) * Mathf.Clamp(rb.mass, 0.3f, 2f));
        }

        private void OnTriggerExit(Collider other)
        {
            for (int i = 0; i < bodyCount; i++)
            {
                if (bodyColliders[i] != other) continue;
                bodyCount--;
                bodies[i] = bodies[bodyCount];
                bodyColliders[i] = bodyColliders[bodyCount];
                bodies[bodyCount] = null;
                bodyColliders[bodyCount] = null;
                return;
            }
        }

        private void FixedUpdate()
        {
            for (int i = bodyCount - 1; i >= 0; i--)
            {
                var rb = bodies[i];
                var col = bodyColliders[i];
                if (rb == null || col == null || !col.enabled || !rb.gameObject.activeInHierarchy || rb.isKinematic)
                {
                    bodyCount--;
                    bodies[i] = bodies[bodyCount];
                    bodyColliders[i] = bodyColliders[bodyCount];
                    continue;
                }
                float submerged = Submerged(col.bounds);
                if (submerged <= 0f) continue;
                float buoyancy = BuoyancyOf(rb);
                // Empuje de Arquimedes simplificado + arrastre (el agua frena y estabiliza).
                rb.AddForce(-Physics.gravity * buoyancy * submerged, ForceMode.Acceleration);
                rb.AddForce(-rb.linearVelocity * waterDrag * submerged, ForceMode.Acceleration);
                rb.AddTorque(-rb.angularVelocity * waterDrag * 0.5f * submerged, ForceMode.Acceleration);
            }
        }

        private float BuoyancyOf(Rigidbody rb)
        {
            var reactive = rb.GetComponent<ElementReactive>();
            return reactive != null && reactive.Profile != null ? reactive.Profile.buoyancy : defaultBuoyancy;
        }

        // ---------------------------------------------------------------- hechizos

        private void Update()
        {
            if (Time.time < nextProbe) return;
            nextProbe = Time.time + spellProbeInterval;
            ProbeSpells();
        }

        /// <summary>Busca donde los hechizos tocan la superficie: ondas, vapor (fuego) y succion (agua + pull).</summary>
        private void ProbeSpells()
        {
            var fields = MagicField.Active;
            Bounds water = volume.bounds;
            for (int f = 0; f < fields.Count; f++)
            {
                var field = fields[f];
                Vector3 origin = field.Frame.Origin;
                // Punto de la superficie mas cercano al sello.
                Vector3 closest = new Vector3(Mathf.Clamp(origin.x, water.min.x, water.max.x), SurfaceY, Mathf.Clamp(origin.z, water.min.z, water.max.z));
                float distance = Vector3.Distance(origin, closest);
                if (distance > field.ReachRadius) continue;

                if (field.Element == ElementType.Water && field.HasIntake)
                {
                    Intake(field, closest, distance);
                    continue;
                }

                // Un hechizo que toca el agua: buscar el punto de la superficie con mas exposicion (muestreo 3x3).
                Vector3 best = closest;
                float bestExposure = 0f;
                Vector3 step = water.size / 4f;
                for (int x = 1; x <= 3; x++)
                for (int z = 1; z <= 3; z++)
                {
                    var p = new Vector3(water.min.x + step.x * x, SurfaceY, water.min.z + step.z * z);
                    float e = field.ExposureAt(p);
                    if (e > bestExposure) { bestExposure = e; best = p; }
                }
                float near = field.ExposureAt(closest);
                if (near > bestExposure) { bestExposure = near; best = closest; }
                if (bestExposure <= 0.05f) continue;

                AddRipple(best, bestExposure);
                if (field.Element == ElementType.Fire)
                    WaterEffects.Burst(steam, best, Mathf.RoundToInt(6f + 20f * bestExposure), 1.2f);
                else
                    WaterEffects.Burst(splash, best, Mathf.RoundToInt(4f + 10f * bestExposure), 1f + 2f * bestExposure);
            }
        }

        private void Intake(MagicField field, Vector3 from, float distance)
        {
            if (stream == null) return;
            Vector3 to = field.Frame.Origin;
            Vector3 dir = (to - from) / Mathf.Max(distance, 1e-3f);
            // Cerca del agua el viaje es muy corto: duracion minima para que el chorro se vea (y mas lento).
            float lifetime = Mathf.Max(distance / intakeSpeed, MinIntakeLifetime);
            WaterEffects.Stream(stream, from, dir * (distance / lifetime), lifetime, 18);
            AddRipple(from, 0.6f);
        }
    }
}
