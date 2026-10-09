using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Compiler;
using SpellSimulator.Output;
using SpellSimulator.Reactions;

namespace SpellSimulator.World
{
    /// <summary>
    /// Recipiente que se llena de agua (balde, cubo): capacidad en litros, nivel visible (disco que sube y baja adentro) y
    /// <see cref="Fill"/>. Lo llenan las gotas que entran por la boca (trigger "BocaAgua" + modulo Trigger de las pistas de
    /// agua, ver <see cref="WaterDropSink"/>). Lleno pesa mas (1 kg por litro, con tope) y, volcado mas de
    /// WaterMath.SpillAngle grados, derrama un chorro de gotas (un ParticleSystem propio, creado una vez) que forma charcos.
    /// Vuelve a su nivel inicial al reiniciar el mundo. Se agrega por codigo (Editor/WaterContainerSetup).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class WaterContainer : MonoBehaviour
    {
        public static readonly List<WaterContainer> All = new List<WaterContainer>();

        [Header("Capacidad")]
        [SerializeField, Tooltip("Litros que entran.")] private float capacityLiters = 10f;
        [SerializeField, Tooltip("Litros al empezar y al reiniciar el mundo.")] private float startLiters;
        [SerializeField, Tooltip("Masa maxima (kg) que suma el agua.")] private float maxExtraMass = 8f;

        [Header("Forma (espacio local)")]
        [SerializeField, Tooltip("Altura del fondo interior (m).")] private float bottomHeight = 0.03f;
        [SerializeField, Tooltip("Altura del borde (m).")] private float rimHeight = 0.26f;
        [SerializeField, Tooltip("Radio interior en el fondo (m).")] private float bottomRadius = 0.12f;
        [SerializeField, Tooltip("Radio de la boca (m).")] private float openingRadius = 0.145f;

        [Header("Derrame")]
        [SerializeField, Tooltip("Litros por segundo que derrama volcado.")] private float spillRate = 3f;
        [SerializeField, Tooltip("Gotas por segundo del derrame.")] private float spillDrops = 70f;

        private const float AcceptTilt = 80f;
        private const float MassStep = 0.1f;

        public float Liters { get; private set; }
        public float Capacity => capacityLiters;
        public float Fraction => capacityLiters > 0f ? Liters / capacityLiters : 0f;
        /// <summary>Collider trigger de la boca: lo usan las pistas de agua en su modulo Trigger.</summary>
        public Collider Mouth { get; private set; }

        private Rigidbody body;
        private float baseMass;
        private float shownMassLiters = -1f;
        private Transform levelDisc;
        private ParticleSystem spill;
        private bool spilling;
        private bool wasFull;
        private bool lookResolved;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            baseMass = body.mass;

            var mouthGo = new GameObject("BocaAgua");
            mouthGo.layer = 2; // Ignore Raycast: no estorba al puntero ni a las particulas
            mouthGo.transform.SetParent(transform, false);
            var sphere = mouthGo.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = openingRadius;
            sphere.center = new Vector3(0f, rimHeight - openingRadius * 0.4f, 0f);
            Mouth = sphere;

            levelDisc = WaterMeshes.CreateDisc("NivelAgua", transform, null).transform;
            levelDisc.gameObject.layer = 2;
            SetLiters(startLiters);
        }

        private void OnEnable()
        {
            All.Add(this);
            MagicWorld.WorldReset += OnWorldReset;
        }

        private void OnDisable()
        {
            All.Remove(this);
            MagicWorld.WorldReset -= OnWorldReset;
        }

        private void Start() => ResolveLook();

        private void OnWorldReset()
        {
            SetLiters(startLiters);
            wasFull = false;
        }

        /// <summary>True si una gota en <paramref name="worldPoint"/> entra por la boca (recipiente mas o menos derecho).</summary>
        public bool Accepts(Vector3 worldPoint)
        {
            if (Vector3.Angle(transform.up, Vector3.up) > AcceptTilt) return false;
            Vector3 mouth = transform.TransformPoint(new Vector3(0f, rimHeight, 0f));
            float r = openingRadius * Mathf.Max(transform.lossyScale.x, 0.01f) * 1.3f;
            return (worldPoint - mouth).sqrMagnitude <= r * r;
        }

        /// <summary>Suma agua (litros). Lo que no entra se pierde.</summary>
        public void Fill(float liters)
        {
            if (liters <= 0f || Vector3.Angle(transform.up, Vector3.up) > AcceptTilt) return;
            SetLiters(Liters + liters);
            bool full = Liters >= capacityLiters - 1e-3f;
            if (full && !wasFull) Debug.Log($"[Agua] {name} se lleno ({capacityLiters:F0} L).");
            wasFull = full;
        }

        private void SetLiters(float liters)
        {
            Liters = Mathf.Clamp(liters, 0f, capacityLiters);
            float k = Fraction;
            levelDisc.gameObject.SetActive(Liters > 0.01f);
            levelDisc.localPosition = new Vector3(0f, Mathf.Lerp(bottomHeight, rimHeight - 0.015f, k), 0f);
            float r = Mathf.Lerp(bottomRadius, openingRadius, k) * 0.98f;
            levelDisc.localScale = new Vector3(r, 1f, r);
            if (Mathf.Abs(Liters - shownMassLiters) >= MassStep || Liters <= 0f)
            {
                shownMassLiters = Liters;
                body.mass = baseMass + WaterMath.ContainerExtraMass(Liters, maxExtraMass);
            }
        }

        private void Update()
        {
            float tilt = Vector3.Angle(transform.up, Vector3.up);
            bool shouldSpill = Liters > 0f && WaterMath.ShouldSpill(tilt);
            if (shouldSpill)
            {
                if (spill == null && !lookResolved) ResolveLook();
                // El labio mas bajo de la boca: por ahi sale el agua.
                Vector3 down = Vector3.ProjectOnPlane(Vector3.down, transform.up);
                if (down.sqrMagnitude < 1e-4f) down = -transform.forward;
                down.Normalize();
                Vector3 lip = transform.TransformPoint(new Vector3(0f, rimHeight, 0f)) + down * openingRadius;
                if (spill != null)
                {
                    spill.transform.SetPositionAndRotation(lip, Quaternion.LookRotation(Vector3.Lerp(down, Vector3.down, 0.5f)));
                    if (!spilling)
                    {
                        var emission = spill.emission;
                        emission.rateOverTime = spillDrops * Mathf.Clamp(Fraction * 2f, 0.4f, 1f);
                        if (!spill.isPlaying) spill.Play();
                    }
                }
                spilling = true;
                SetLiters(Liters - spillRate * Time.deltaTime);
            }
            else if (spilling)
            {
                spilling = false;
                if (spill != null)
                {
                    var emission = spill.emission;
                    emission.rateOverTime = 0f;
                }
            }
        }

        /// <summary>Busca el look del agua (material del nivel y gotas del derrame). Una sola vez.</summary>
        private void ResolveLook()
        {
            if (lookResolved) return;
            lookResolved = true;
            var output = FindFirstObjectByType<SpellOutputController>();
            ElementLook look = output != null ? output.LookFor(ElementType.Water) : null;
            if (look == null || look.material == null) return;
            levelDisc.GetComponent<MeshRenderer>().sharedMaterial = look.material;
            WaterPuddleManager.SetLook(look);

            spill = ElementVfx.Create("Derrame", transform, look, transform.position, Quaternion.identity, 0.35f);
            var main = spill.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
            main.gravityModifier = 1f;
            var shape = spill.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = openingRadius * 0.3f;
            WaterSpell.ConfigureDrops(spill, 1f, 1f, this);
            spill.Play();
        }
    }
}
