using UnityEngine;
using SpellSimulator.Output;

namespace SpellSimulator.Reactions
{
    /// <summary>
    /// Charcos del agua fisica (octava tanda, seccion 3): pool estatico de MaxPuddles <see cref="WaterPuddle"/> creados una vez.
    /// Las gotas que chocan (<see cref="WaterDropSink"/>) dejan impactos en un buffer; una vez por frame se procesan: alimentan
    /// el charco cercano o activan uno nuevo. Un solo Update (el del corredor oculto) avanza todos: evaporacion, deslizamiento
    /// cuesta abajo (4 rayos para la pendiente, a baja frecuencia), fusion de charcos vecinos y mojar lo que los pisa.
    /// Sin allocations por frame. Se vacia al reiniciar el mundo.
    /// </summary>
    public static class WaterPuddleManager
    {
        public const int MaxPuddles = 16;
        public const int MaxImpacts = 128;
        /// <summary>Normal minima del suelo donde se forma un charco (paredes y techos no).</summary>
        public const float MinGroundNormalY = 0.55f;
        /// <summary>Distancia extra (m) a la que una gota alimenta un charco existente en vez de crear otro.</summary>
        public const float FeedGap = 0.25f;

        private const float SampleInterval = 0.1f;
        private const float MergeInterval = 0.25f;
        private const float WetInterval = 0.2f;
        private const float RayLift = 0.5f;
        private const float RayLength = 1.4f;
        private const float WetHeight = 0.35f;
        private const int MaxWetHits = 32;

        /// <summary>Exposicion por segundo de pisar o tocar un charco: moja y enfria (apaga fuego).</summary>
        private static readonly Exposure PuddleExposure = new Exposure { Heat = -3f, Wet = 2.5f };

        private struct Impact
        {
            public Vector3 Point;
            public Vector3 Normal;
            public float Liters;
            public float InkPower;
        }

        private static readonly WaterPuddle[] pool = new WaterPuddle[MaxPuddles];
        private static readonly Impact[] impacts = new Impact[MaxImpacts];
        private static readonly Collider[] wetHits = new Collider[MaxWetHits];
        private static readonly ElementReactive[] wetFound = new ElementReactive[MaxWetHits];
        private static int impactCount;
        private static Runner runner;
        private static Material waterMaterial;
        private static int groundMask;
        private static float nextSample, nextMerge, nextWet;

        public static int ActiveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < MaxPuddles; i++) if (pool[i] != null && pool[i].IsActive) n++;
                return n;
            }
        }

        /// <summary>Capas del suelo para los rayos (como la colision de las particulas: sin jugador, UI ni triggers).</summary>
        public static int GroundMask
        {
            get
            {
                if (groundMask == 0) groundMask = ~LayerMask.GetMask("Ignore Raycast", "Player", "UI", "TransparentFX");
                return groundMask;
            }
        }

        /// <summary>Material de la superficie (el del look de agua). Se puede llamar en cada lanzamiento.</summary>
        public static void SetLook(ElementLook look)
        {
            if (look == null || look.material == null || look.material == waterMaterial) return;
            waterMaterial = look.material;
            for (int i = 0; i < MaxPuddles; i++) if (pool[i] != null) pool[i].SetMaterial(waterMaterial);
        }

        /// <summary>Encola una gota que choco con el mundo (lo llama WaterDropSink). Sin allocations; si el buffer se llena, se descarta.</summary>
        public static void AddImpact(Vector3 point, Vector3 normal, float liters, float inkPower)
        {
            if (impactCount >= MaxImpacts) return;
            EnsureRunner();
            impacts[impactCount++] = new Impact { Point = point, Normal = normal, Liters = liters, InkPower = inkPower };
        }

        /// <summary>Borra todos los charcos (reinicio del mundo).</summary>
        public static void ClearAll()
        {
            impactCount = 0;
            for (int i = 0; i < MaxPuddles; i++) if (pool[i] != null) pool[i].Deactivate();
        }

        /// <summary>
        /// Moja lo que esta en la caja (centro, semiejes): suma <paramref name="exposure"/> por segundo, escalada para un llamado
        /// cada <paramref name="interval"/> s (MagicWorld consume la exposicion en cada tick de 1/TickRate). La usan los charcos y las zonas.
        /// </summary>
        public static void WetArea(Vector3 center, Vector3 halfExtents, in Exposure exposure, float scale, float interval)
        {
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, wetHits, Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
            int unique = 0;
            float k = scale * interval * MagicWorld.TickRate;
            for (int i = 0; i < count; i++)
            {
                var reactive = wetHits[i].GetComponentInParent<ElementReactive>();
                if (reactive == null || !reactive.isActiveAndEnabled) continue;
                bool seen = false;
                for (int j = 0; j < unique; j++) if (wetFound[j] == reactive) { seen = true; break; }
                if (seen) continue;
                wetFound[unique++] = reactive;
                reactive.AddExposure(exposure, k);
            }
            for (int j = 0; j < unique; j++) wetFound[j] = null;
        }

        /// <summary>True si el punto esta dentro de agua que ya existe (lago o zona inundada): ahi no se forman charcos.</summary>
        public static bool InsideWater(Vector3 point)
        {
            var waters = WaterVolume.All;
            for (int i = 0; i < waters.Count; i++)
            {
                Bounds b = waters[i].Bounds;
                b.Expand(new Vector3(0f, 0.2f, 0f));
                if (b.Contains(point)) return true;
            }
            return FloodZone.Contains(point);
        }

        // ---------------------------------------------------------------- avance

        private static void EnsureRunner()
        {
            if (runner != null) return;
            var go = new GameObject("Agua_Charcos");
            runner = go.AddComponent<Runner>();
            for (int i = 0; i < MaxPuddles; i++)
            {
                var p = new GameObject($"Charco_{i:00}");
                p.transform.SetParent(go.transform, false);
                var puddle = p.AddComponent<WaterPuddle>();
                puddle.Build(waterMaterial);
                p.SetActive(false);
                pool[i] = puddle;
            }
        }

        private static void Step(float dt)
        {
            ProcessImpacts();

            bool sample = Time.time >= nextSample;
            if (sample) nextSample = Time.time + SampleInterval;
            for (int i = 0; i < MaxPuddles; i++)
            {
                var puddle = pool[i];
                if (puddle == null || !puddle.IsActive) continue;
                if (!puddle.Tick(dt)) { puddle.Deactivate(); continue; }
                if (puddle.IsFrozen) continue;
                if (sample && !SampleGround(puddle)) { puddle.Deactivate(); continue; }
                Vector2 v = puddle.Slide;
                if (v.sqrMagnitude <= 0f) continue;
                Vector3 p = puddle.transform.position;
                p.x += v.x * dt;
                p.z += v.y * dt;
                // La altura sigue al terreno de a poco (se corrige con el proximo muestreo).
                p.y = Mathf.MoveTowards(p.y, puddle.GroundY, dt * 1.5f);
                puddle.MoveTo(p);
            }

            if (Time.time >= nextMerge)
            {
                nextMerge = Time.time + MergeInterval;
                Merge();
            }
            if (Time.time >= nextWet)
            {
                nextWet = Time.time + WetInterval;
                for (int i = 0; i < MaxPuddles; i++)
                {
                    var puddle = pool[i];
                    if (puddle == null || !puddle.IsActive || puddle.IsFrozen) continue;
                    float r = puddle.Radius;
                    Vector3 c = puddle.transform.position + Vector3.up * (WetHeight * 0.5f);
                    WetArea(c, new Vector3(r, WetHeight * 0.5f, r), PuddleExposure, 1f, WetInterval);
                }
            }
        }

        private static void ProcessImpacts()
        {
            for (int k = 0; k < impactCount; k++)
            {
                Impact impact = impacts[k];
                WaterPuddle best = FindNear(impact.Point, FeedGap);
                if (best != null)
                {
                    best.AddLiters(impact.Liters);
                    continue;
                }
                if (impact.Normal.y < MinGroundNormalY || InsideWater(impact.Point)) continue;
                if (IsOnIce(impact.Point)) continue;
                var free = FreePuddle();
                if (free == null) continue;
                free.Activate(impact.Point, impact.Liters, WaterMath.EvaporationRate(impact.InkPower));
            }
            impactCount = 0;
        }

        /// <summary>Charco liquido mas cercano cuyo borde (+gap) contiene el punto.</summary>
        private static WaterPuddle FindNear(Vector3 point, float gap)
        {
            WaterPuddle best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < MaxPuddles; i++)
            {
                var puddle = pool[i];
                if (puddle == null || !puddle.IsActive || puddle.IsFrozen) continue;
                Vector3 p = puddle.transform.position;
                if (Mathf.Abs(p.y - point.y) > 0.5f) continue;
                float dx = p.x - point.x, dz = p.z - point.z;
                float d = dx * dx + dz * dz;
                float reach = puddle.Radius + gap;
                if (d <= reach * reach && d < bestD) { bestD = d; best = puddle; }
            }
            return best;
        }

        private static bool IsOnIce(Vector3 point)
        {
            for (int i = 0; i < MaxPuddles; i++)
            {
                var puddle = pool[i];
                if (puddle == null || !puddle.IsActive || !puddle.IsFrozen) continue;
                Vector3 p = puddle.transform.position;
                float dx = p.x - point.x, dz = p.z - point.z;
                if (dx * dx + dz * dz <= puddle.Radius * puddle.Radius) return true;
            }
            return false;
        }

        private static WaterPuddle FreePuddle()
        {
            for (int i = 0; i < MaxPuddles; i++)
                if (pool[i] != null && !pool[i].IsActive) return pool[i];
            return null;
        }

        /// <summary>Pendiente bajo el charco con 4 rayos (+1 al centro para la altura). False si quedo sin suelo o cayo al agua.</summary>
        private static bool SampleGround(WaterPuddle puddle)
        {
            Vector3 p = puddle.transform.position;
            if (!GroundHeight(p, out float center) || InsideWater(p)) return false;
            float d = Mathf.Max(puddle.Radius * 0.7f, 0.15f);
            float east = GroundHeight(p + new Vector3(d, 0f, 0f), out float h) ? h : center;
            float west = GroundHeight(p - new Vector3(d, 0f, 0f), out h) ? h : center;
            float north = GroundHeight(p + new Vector3(0f, 0f, d), out h) ? h : center;
            float south = GroundHeight(p - new Vector3(0f, 0f, d), out h) ? h : center;
            puddle.GroundY = center;
            puddle.Slide = WaterMath.SlideVelocity(WaterMath.Gradient(east, west, north, south, d));
            return true;
        }

        private static bool GroundHeight(Vector3 at, out float height)
        {
            if (Physics.Raycast(at + Vector3.up * RayLift, Vector3.down, out RaycastHit hit, RayLength, GroundMask, QueryTriggerInteraction.Ignore))
            {
                height = hit.point.y;
                return true;
            }
            height = at.y;
            return false;
        }

        private static void Merge()
        {
            for (int i = 0; i < MaxPuddles; i++)
            {
                var a = pool[i];
                if (a == null || !a.IsActive || a.IsFrozen) continue;
                for (int j = i + 1; j < MaxPuddles; j++)
                {
                    var b = pool[j];
                    if (b == null || !b.IsActive || b.IsFrozen) continue;
                    Vector3 pa = a.transform.position, pb = b.transform.position;
                    if (Mathf.Abs(pa.y - pb.y) > 0.4f) continue;
                    if (!WaterMath.ShouldMerge(new Vector2(pa.x, pa.z), a.Radius, new Vector2(pb.x, pb.z), b.Radius)) continue;
                    Vector3 c = WaterMath.MergedCenter(pa, a.Liters, pb, b.Liters);
                    float rate = Mathf.Min(a.EvaporationRate, b.EvaporationRate);
                    a.MoveTo(c);
                    a.EvaporationRate = rate;
                    a.AddLiters(b.Liters);
                    b.Deactivate();
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            for (int i = 0; i < MaxPuddles; i++) pool[i] = null;
            impactCount = 0;
            runner = null;
            waterMaterial = null;
            nextSample = nextMerge = nextWet = 0f;
        }

        /// <summary>Corredor oculto: un solo Update para todos los charcos y el reinicio del mundo.</summary>
        private class Runner : MonoBehaviour
        {
            private void OnEnable() => MagicWorld.WorldReset += ClearAll;
            private void OnDisable() => MagicWorld.WorldReset -= ClearAll;

            private void OnDestroy()
            {
                if (runner != this) return;
                runner = null;
                for (int i = 0; i < MaxPuddles; i++) pool[i] = null;
            }

            private void LateUpdate() => Step(Time.deltaTime);
        }
    }
}
