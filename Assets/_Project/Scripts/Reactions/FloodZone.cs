using System;
using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Interfaces;
using SpellSimulator.Output;

namespace SpellSimulator.Reactions
{
    /// <summary>
    /// Zona inundada temporal (hechizo de agua grande y fuerte apuntado al suelo): volumen plano (BoxCollider trigger de
    /// ~radio x 6 de lado) que se llena, ondula, se mantiene y drena en 12-20 s (WaterMath.FloodLevel). Hace flotar los
    /// Rigidbody livianos (masa menor a WaterMath.MaxFloatMass) con empuje segun la parte sumergida y frena todo lo que
    /// esta adentro; moja y apaga lo que toca. Congelada (agua + cristal) levanta una placa de hielo en la superficie que
    /// soporta peso y pausa el drenaje. Maximo MaxZones vivas; se borran al reiniciar el mundo.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class FloodZone : MonoBehaviour, IFreezable
    {
        public const int MaxZones = 2;
        /// <summary>Alto del agua llena (m).</summary>
        public const float Depth = 0.35f;
        private const int MaxBodies = 32;
        private const float TriggerMargin = 0.4f;
        private const float BobAmplitude = 0.025f;
        private const float WaterDrag = 2.5f;
        private const float DefaultBuoyancy = 1.3f;
        private const float WetInterval = 0.2f;

        private static readonly List<FloodZone> zones = new List<FloodZone>();
        private static readonly Exposure Immersion = new Exposure { Heat = -4f, Wet = 3f };

        public static int ActiveCount => zones.Count;
        public static IReadOnlyList<FloodZone> Zones => zones;
        /// <summary>Se creo una zona / se congelo una zona (en red, el anfitrion lo anuncia a los clientes).</summary>
        public static event Action<FloodZone> Spawned;
        public static event Action<FloodZone, float> Frozen;
        private static int nextNetId = 1;

        /// <summary>Identificador compartido en red (lo asigna el anfitrion; los clientes copian el suyo).</summary>
        public int NetId { get; set; }
        public Vector3 Ground => new Vector3(transform.position.x, groundY, transform.position.z);
        public float Size => halfSize * 2f;
        public float Life => life;
        public float Delay => startDelay;
        public float Age => age;

        private readonly Rigidbody[] bodies = new Rigidbody[MaxBodies];
        private readonly Collider[] bodyColliders = new Collider[MaxBodies];
        private readonly float[] bodyBuoyancy = new float[MaxBodies];
        private int bodyCount;
        private float halfSize;
        private float groundY;
        private float life;
        private float age;
        private float startDelay;
        private float frozenUntil;
        private float level;
        private float nextWet;
        private Transform surface;

        public float SurfaceY => groundY + level * Depth + (level > 0f ? Mathf.Sin(Time.time * 1.7f) * BobAmplitude * level : 0f);

        /// <summary>Crea una zona apoyada en <paramref name="ground"/>, de lado <paramref name="size"/> m, que dura <paramref name="seconds"/>.</summary>
        public static FloodZone Create(Vector3 ground, float size, float seconds, float delay, ElementLook look)
            => Create(ground, size, seconds, delay, look != null ? look.material : null, 0f);

        public static FloodZone Create(Vector3 ground, float size, float seconds, float delay, Material material, float elapsed)
        {
            if (Net.NetCast.IsClientOnly && !Net.NetEffectsSync.Applying) return null; // en red solo el anfitrion crea zonas; los clientes reciben el aviso
            zones.RemoveAll(z => z == null);
            while (zones.Count >= MaxZones)
            {
                // La mas vieja se va: sin drenaje lento para no pasarse del presupuesto.
                Destroy(zones[0].gameObject);
                zones.RemoveAt(0);
            }

            var go = new GameObject("ZonaInundada");
            go.layer = 2; // Ignore Raycast: el puntero y las particulas la atraviesan.
            go.transform.position = ground;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var zone = go.AddComponent<FloodZone>(); // RequireComponent agrega el BoxCollider
            var box = go.GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, (Depth + TriggerMargin) * 0.5f, 0f);
            box.size = new Vector3(size, Depth + TriggerMargin, size);

            zone.halfSize = size * 0.5f;
            zone.groundY = ground.y;
            zone.life = seconds;
            zone.startDelay = delay;
            zone.age = Mathf.Max(0f, elapsed);
            zone.NetId = nextNetId++;
            var disc = WaterMeshes.CreateDisc("Superficie", go.transform, material);
            disc.gameObject.layer = 2;
            zone.surface = disc.transform;
            zone.surface.localScale = Vector3.zero;
            zones.Add(zone);
            Spawned?.Invoke(zone);
            return zone;
        }

        /// <summary>True si el punto esta en el agua de alguna zona viva.</summary>
        public static bool Contains(Vector3 point)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                if (z == null || z.level <= 0f) continue;
                Vector3 c = z.transform.position;
                if (Mathf.Abs(point.x - c.x) <= z.halfSize && Mathf.Abs(point.z - c.z) <= z.halfSize &&
                    point.y >= z.groundY - 0.1f && point.y <= z.SurfaceY + 0.1f) return true;
            }
            return false;
        }

        public static void ClearAll()
        {
            for (int i = zones.Count - 1; i >= 0; i--) if (zones[i] != null) Destroy(zones[i].gameObject);
            zones.Clear();
        }

        private void OnEnable() => MagicWorld.WorldReset += OnWorldReset;
        private void OnDisable() => MagicWorld.WorldReset -= OnWorldReset;
        private void OnDestroy() => zones.Remove(this);
        private void OnWorldReset() => Destroy(gameObject);

        /// <summary>Cliente: copia que el anfitrion congelo la zona (pausa el drenaje; la placa de hielo llega aparte).</summary>
        public void NetFreeze(float seconds) => frozenUntil = Mathf.Max(frozenUntil, Time.time + seconds);

        public bool Freeze(Vector3 center, float radius, float seconds)
        {
            if (level <= 0.05f || seconds <= 0f) return false;
            if (Net.NetCast.IsClientOnly && !Net.NetEffectsSync.Applying) return false;
            Vector3 c = transform.position;
            float dx = Mathf.Max(0f, Mathf.Abs(center.x - c.x) - halfSize);
            float dz = Mathf.Max(0f, Mathf.Abs(center.z - c.z) - halfSize);
            float dy = Mathf.Abs(center.y - SurfaceY);
            if (dx * dx + dz * dz + dy * dy > radius * radius) return false;
            bool already = frozenUntil > Time.time;
            frozenUntil = Mathf.Max(frozenUntil, Time.time + seconds);
            if (!already)
            {
                Vector3 top = new Vector3(c.x, groundY + level * Depth, c.z);
                IceSheet.Create(top, halfSize, halfSize, IceSheet.DefaultThickness, seconds);
                Frozen?.Invoke(this, seconds);
            }
            return true;
        }

        // ---------------------------------------------------------------- cuerpos

        private void OnTriggerEnter(Collider other)
        {
            var rb = other.attachedRigidbody;
            if (rb == null || rb.isKinematic || bodyCount >= MaxBodies) return;
            for (int i = 0; i < bodyCount; i++) if (bodies[i] == rb) return;
            bodies[bodyCount] = rb;
            bodyColliders[bodyCount] = other;
            bodyBuoyancy[bodyCount] = BuoyancyOf(rb);
            bodyCount++;
        }

        private void OnTriggerExit(Collider other)
        {
            for (int i = 0; i < bodyCount; i++)
            {
                if (bodyColliders[i] != other) continue;
                RemoveAt(i);
                return;
            }
        }

        private void RemoveAt(int i)
        {
            bodyCount--;
            bodies[i] = bodies[bodyCount];
            bodyColliders[i] = bodyColliders[bodyCount];
            bodyBuoyancy[i] = bodyBuoyancy[bodyCount];
            bodies[bodyCount] = null;
            bodyColliders[bodyCount] = null;
        }

        private void Update()
        {
            bool frozen = frozenUntil > Time.time;
            if (!frozen)
            {
                age += Time.deltaTime;
                level = WaterMath.FloodLevel(age - startDelay, life);
                if (age - startDelay >= life) { Destroy(gameObject); return; }
            }
            float y = SurfaceY - groundY;
            float r = halfSize * 1.15f * Mathf.Clamp01(level * 3f); // el agua se extiende al llenar
            surface.localPosition = new Vector3(0f, y + 0.01f, 0f);
            surface.localScale = new Vector3(r, 1f, r);
            surface.gameObject.SetActive(level > 0f && !frozen);

            if (level > 0f && Time.time >= nextWet)
            {
                nextWet = Time.time + WetInterval;
                float h = level * Depth;
                Vector3 c = new Vector3(transform.position.x, groundY + h * 0.5f, transform.position.z);
                WaterPuddleManager.WetArea(c, new Vector3(halfSize, Mathf.Max(h * 0.5f, 0.05f), halfSize), Immersion, level, WetInterval);
            }
        }

        private void FixedUpdate()
        {
            if (level <= 0f || frozenUntil > Time.time) return;
            float surfaceY = SurfaceY;
            float g = Physics.gravity.y;
            for (int i = bodyCount - 1; i >= 0; i--)
            {
                var rb = bodies[i];
                var col = bodyColliders[i];
                if (rb == null || col == null || !col.enabled || !rb.gameObject.activeInHierarchy || rb.isKinematic)
                {
                    RemoveAt(i);
                    continue;
                }
                Bounds b = col.bounds;
                float submerged = WaterMath.SubmergedFraction(b.min.y, b.size.y, surfaceY);
                if (submerged <= 0f) continue;
                float lift = WaterMath.BuoyantAcceleration(rb.mass, submerged, bodyBuoyancy[i], g);
                if (lift > 0f) rb.AddForce(Vector3.up * lift, ForceMode.Acceleration);
                rb.AddForce(-rb.linearVelocity * WaterDrag * submerged, ForceMode.Acceleration);
                rb.AddTorque(-rb.angularVelocity * WaterDrag * 0.5f * submerged, ForceMode.Acceleration);
            }
        }

        private static float BuoyancyOf(Rigidbody rb)
        {
            var reactive = rb.GetComponent<ElementReactive>();
            return reactive != null && reactive.Profile != null ? reactive.Profile.buoyancy : DefaultBuoyancy;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { zones.Clear(); nextNetId = 1; }
    }
}
