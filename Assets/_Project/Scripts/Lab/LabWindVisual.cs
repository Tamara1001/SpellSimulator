using UnityEngine;
using SpellSimulator.Compiler;
using SpellSimulator.Output;
using SpellSimulator.Rendering;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Visual del viento del laboratorio. Sigue a la <see cref="LabWindZone"/> mas reciente (evento <see cref="LabWindZone.Changed"/>)
    /// y dibuja, con el <see cref="ElementLook"/> del viento de los hechizos: estelas rizadas (capa con trails + ruido suave),
    /// hojas/petalos girando (capa con giro), polvo a ras del piso por encima de 10 m/s, chevrones en el piso que marcan la
    /// direccion, y una manga de viento con banderines que se orientan y se tensan con la velocidad (10 Hz).
    /// Sin zona activa: particulas apagadas, manga caida y, si hay <see cref="previewAnchor"/>, chevrones atenuados con la
    /// direccion de la mesa de variables (<see cref="LabParameters"/>) como vista previa.
    /// Los modulos de particulas se reconfiguran solo al cambiar el viento (nunca por frame) y se pre-simulan para no arrancar vacios.
    /// El componente va en el lugar de la manga: si no se le pasa una, la arma con primitivas sobre su propio transform.
    /// </summary>
    [DisallowMultipleComponent]
    public class LabWindVisual : MonoBehaviour
    {
        /// <summary>Periodo de actualizacion de la manga y los banderines (s): 10 Hz.</summary>
        private const float TickInterval = 0.1f;
        private const int ChevronCount = 5;
        /// <summary>Altura de los chevrones sobre la base de la zona (m), para que no titilen contra el piso.</summary>
        private const float ChevronHeight = 0.03f;
        /// <summary>Ancho de cada chevron (m).</summary>
        private const float ChevronSize = 0.6f;
        /// <summary>Atenuacion del color de los chevrones en vista previa (sin zona).</summary>
        private const float PreviewDim = 0.45f;
        private const float PoleHeight = 3f, PoleRadius = 0.04f;
        private const int SockSegmentCount = 4;
        private const float SockSegmentLength = 0.32f, SockRadius = 0.15f, SockTaper = 0.8f;
        /// <summary>Caida total de la manga sin viento (grados desde la horizontal).</summary>
        private const float MaxSockDroop = 85f;
        /// <summary>Caida de cada banderin sin viento (grados).</summary>
        private const float MaxPennantDroop = 75f;
        /// <summary>Velocidad de giro de la manga hacia la nueva direccion (grados/s).</summary>
        private const float YawRate = 120f;
        /// <summary>Velocidad de cambio de la tension (1/s).</summary>
        private const float TensionRate = 1.5f;
        private const float DustHeight = 0.3f, DustMinSize = 0.25f, DustMaxSize = 0.5f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color DustColor = new Color(0.78f, 0.68f, 0.5f);
        private static readonly Color SockRed = new Color(0.86f, 0.3f, 0.22f), SockWhite = new Color(0.96f, 0.93f, 0.86f);
        private static readonly Color PoleColor = new Color(0.72f, 0.58f, 0.3f);
        private static Mesh chevronMesh;

        [Header("Look (lo asigna el generador)")]
        [SerializeField, Tooltip("Look del viento de los hechizos (Data/Elements/Wind). Vacio = el de SpellOutputController.")]
        private ElementLook windLook;
        [SerializeField, Tooltip("Material de chevrones y manga (uno existente; el color va por MaterialPropertyBlock).")]
        private Material propMaterial;

        [Header("Manga y banderines")]
        [SerializeField, Tooltip("Pivote de la manga (gira en Y). Vacio = se arma una con primitivas sobre este transform.")]
        private Transform sockPivot;
        [SerializeField, Tooltip("Segmentos de la manga encadenados a lo largo de +Z local (se doblan con la caida).")]
        private Transform[] sockSegments;
        [SerializeField, Tooltip("Banderines: pivote en el mastil, la tela a lo largo de +Z local.")]
        private Transform[] pennants;

        [Header("Vista previa")]
        [SerializeField, Tooltip("Base donde aparece la zona de viento (el ancla del spawner): chevrones de vista previa sin zona.")]
        private Transform previewAnchor;

        private ElementLook streakLook, leafLook;
        private Transform rig;
        private ParticleSystem streaks, leaves, dust;
        private readonly Transform[] chevrons = new Transform[ChevronCount];
        private readonly MeshRenderer[] chevronRenderers = new MeshRenderer[ChevronCount];
        private MaterialPropertyBlock block;
        private AnimationCurve trailWidthCurve;
        private Gradient dustGradient;
        private Color windColor = new Color(0.9f, 0.97f, 1f);

        private LabParameters parameters;
        private LabWindZone followed;
        private float shownSpeed = -1f, shownYaw;
        private bool built, hasZone, chevronsVisible;
        private WindVisualParams current;
        private float chevronPhase, nextTick;
        private float sockYaw, sockTension, targetYaw, targetTension;

        /// <summary>Parametros visuales vigentes (velocidad 0 si no hay zona). Para el harness.</summary>
        public WindVisualParams Current => current;

        /// <summary>True si sigue a una zona de viento activa.</summary>
        public bool HasZone => hasZone;

        /// <summary>
        /// Lo llama el generador de escena despues de AddComponent. <paramref name="sock"/> null = arma una manga con primitivas
        /// sobre este transform; <paramref name="segments"/> son opcionales (si faltan, la manga entera se inclina).
        /// </summary>
        public void Setup(ElementLook look, Material material, Transform preview = null, Transform sock = null,
            Transform[] segments = null, Transform[] flags = null)
        {
            windLook = look;
            propMaterial = material;
            previewAnchor = preview;
            sockPivot = sock;
            sockSegments = segments;
            pennants = flags;
        }

        private void Awake() => block = new MaterialPropertyBlock();

        private void OnEnable()
        {
            LabWindZone.Changed += Refresh;
            TrySubscribeParameters();
            if (built) Refresh();
        }

        private void OnDisable()
        {
            LabWindZone.Changed -= Refresh;
            if (parameters != null) parameters.ValueChanged -= OnParameterChanged;
            parameters = null;
        }

        private void Start()
        {
            ResolveLooks();
            Build();
            built = true;
            if (parameters != null) sockYaw = targetYaw = WindVisualMath.Params(0f, parameters.Get(LabVariableId.WindYaw)).YawDeg;
            followed = null;
            Refresh();
            TickSock();
        }

        private void OnDestroy()
        {
            if (rig != null) Destroy(rig.gameObject);
        }

        private void Update()
        {
            if (!built) return;
            if (hasZone && chevronsVisible && current.ChevronSpeed > 0f)
            {
                chevronPhase += current.ChevronSpeed * Time.deltaTime;
                LayoutChevrons();
            }
            if (Time.time < nextTick) return;
            nextTick = Time.time + TickInterval;
            if (parameters == null) TrySubscribeParameters();
            TickSock();
        }

        // ---------------------------------------------------------------- seguir la zona (solo por eventos)

        private void Refresh()
        {
            if (!built) return;
            LabWindZone zone = Latest();
            if (zone == null)
            {
                followed = null;
                shownSpeed = -1f;
                ShowPreview();
                return;
            }
            if (zone == followed && Mathf.Approximately(zone.Speed, shownSpeed) && Mathf.Approximately(zone.YawDeg, shownYaw)) return;
            followed = zone;
            shownSpeed = zone.Speed;
            shownYaw = zone.YawDeg;
            ApplyWind(zone.Speed, zone.YawDeg, zone.transform.position);
        }

        private static LabWindZone Latest()
        {
            var all = LabWindZone.All;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                LabWindZone z = all[i];
                if (z != null && z.isActiveAndEnabled) return z;
            }
            return null;
        }

        private void ApplyWind(float speed, float yawDeg, Vector3 basePosition)
        {
            current = WindVisualMath.Params(speed, yawDeg);
            hasZone = true;
            rig.SetPositionAndRotation(basePosition, Quaternion.LookRotation(current.Direction, Vector3.up));
            targetYaw = current.YawDeg;
            targetTension = current.SockTension;
            ConfigureSystems(speed > 0f ? speed : 0f);
            SetChevrons(true, windColor);
            LayoutChevrons();
        }

        private void ShowPreview()
        {
            hasZone = false;
            targetTension = 0f;
            StopAll();
            float yaw = parameters != null ? parameters.Get(LabVariableId.WindYaw) : current.YawDeg;
            current = WindVisualMath.Params(0f, yaw);
            if (parameters == null || previewAnchor == null)
            {
                SetChevrons(false, windColor);
                return;
            }
            rig.SetPositionAndRotation(previewAnchor.position, Quaternion.LookRotation(current.Direction, Vector3.up));
            chevronPhase = 0f;
            SetChevrons(parameters.Get(LabVariableId.WindSpeed) > 0f, windColor * PreviewDim);
            LayoutChevrons();
        }

        private void TrySubscribeParameters()
        {
            if (parameters != null) return;
            parameters = LabParameters.Current;
            if (parameters != null) parameters.ValueChanged += OnParameterChanged;
        }

        private void OnParameterChanged(LabVariableId id, float previous, float next)
        {
            if (!built || hasZone) return;
            if (id == LabVariableId.WindYaw || id == LabVariableId.WindSpeed) ShowPreview();
        }

        // ---------------------------------------------------------------- particulas (se arman una vez)

        private void ResolveLooks()
        {
            if (windLook == null)
            {
                var output = FindAnyObjectByType<SpellOutputController>();
                if (output != null) windLook = output.LookFor(ElementType.Wind);
            }
            if (windLook == null)
            {
                Debug.LogWarning("[LabWindVisual] Sin ElementLook de viento: solo chevrones y manga.");
                return;
            }
            if (windLook.layers != null)
            {
                foreach (ElementLook layer in windLook.layers)
                {
                    if (layer == null) continue;
                    if (layer.trails && streakLook == null) streakLook = layer;
                    else if (layer.spin > 0f && leafLook == null) leafLook = layer;
                }
            }
            if (streakLook == null) streakLook = windLook; // sin capa de estelas: rayas estiradas del look base
            if (windLook.color != null) windColor = windLook.color.Evaluate(0.35f);
            windColor.a = 1f;
        }

        private void Build()
        {
            rig = new GameObject("LabWindVisual_Rig").transform;
            trailWidthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f);
            dustGradient = new Gradient();
            dustGradient.SetKeys(
                new[] { new GradientColorKey(DustColor, 0f), new GradientColorKey(DustColor * 0.9f, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.2f), new GradientAlphaKey(0f, 1f) });

            Vector3 size = LabWindZone.Size;
            if (windLook != null)
            {
                streaks = BuildEmitter("Estelas", streakLook, new Vector3(size.x, size.y * 0.8f, 0.1f), size.y * 0.5f, size.z);
                if (leafLook != null)
                    leaves = BuildEmitter("Hojas", leafLook, new Vector3(size.x, size.y * 0.5f, 0.1f), size.y * 0.3f, size.z);
                dust = BuildEmitter("Polvo", windLook, new Vector3(size.x, DustHeight, 0.1f), DustHeight * 0.5f, size.z);
                ParticleSystem.MainModule main = dust.main;
                main.startSize = new ParticleSystem.MinMaxCurve(DustMinSize, DustMaxSize);
                main.gravityModifier = -0.02f;
                ParticleSystem.ColorOverLifetimeModule col = dust.colorOverLifetime;
                col.enabled = true;
                col.color = new ParticleSystem.MinMaxGradient(dustGradient);
                var psr = dust.GetComponent<ParticleSystemRenderer>();
                psr.renderMode = ParticleSystemRenderMode.Billboard;
            }
            BuildChevrons();
            if (sockPivot == null) BuildSock();
        }

        /// <summary>Emisor en la cara de barlovento de la zona, mirando hacia donde sopla (+Z del rig).</summary>
        private ParticleSystem BuildEmitter(string name, ElementLook look, Vector3 boxScale, float height, float length)
        {
            ParticleSystem ps = ElementVfx.Create(name, rig, look, rig.position, rig.rotation, 1f);
            ps.transform.localPosition = new Vector3(0f, height, -length * 0.5f);
            ps.transform.localRotation = Quaternion.identity;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = boxScale;
            shape.randomDirectionAmount = 0f;
            return ps;
        }

        /// <summary>Unica reconfiguracion de modulos: al aparecer la zona o cambiar su velocidad/direccion.</summary>
        private void ConfigureSystems(float speed)
        {
            WindVisualParams p = current;
            float scale = Mathf.Clamp01(GraphicsQuality.ParticleScale);
            if (streaks != null)
            {
                ParticleSystem.TrailModule trails = streaks.trails;
                if (trails.enabled)
                {
                    float travel = Mathf.Max(0.01f, speed * p.Lifetime);
                    trails.lifetime = Mathf.Clamp(p.TrailLength / travel, 0.05f, 1f);
                    trails.widthOverTrail = new ParticleSystem.MinMaxCurve(p.TrailWidth, trailWidthCurve);
                }
                ParticleSystem.NoiseModule noise = streaks.noise;
                noise.enabled = p.Noise > 0f;
                noise.strength = p.Noise;
                ConfigureEmitter(streaks, Scaled(p.TrailCount, scale), p.Lifetime, speed, p.Alpha);
            }
            if (leaves != null)
                ConfigureEmitter(leaves, Scaled(p.LeafCount, scale), p.Lifetime, speed * 0.8f, Mathf.Min(1f, p.Alpha + 0.15f));
            if (dust != null)
                ConfigureEmitter(dust, p.Dust ? Scaled(p.DustCount, scale) : 0, p.Lifetime, speed * 0.9f, 1f);
        }

        private static void ConfigureEmitter(ParticleSystem ps, int count, float lifetime, float startSpeed, float alpha)
        {
            ParticleSystem.MainModule main = ps.main;
            main.maxParticles = Mathf.Max(1, count);
            main.startLifetime = lifetime;
            main.startSpeed = startSpeed;
            main.startColor = new Color(1f, 1f, 1f, alpha);
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = count > 0 ? count / lifetime : 0f;
            if (count <= 0)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                return;
            }
            // Pre-simulacion: la zona aparece ya llena de viento.
            ps.Simulate(lifetime, true, true);
            ps.Play(true);
        }

        private static int Scaled(int count, float scale) => count <= 0 ? 0 : Mathf.Clamp(Mathf.RoundToInt(count * scale), 1, count);

        private void StopAll()
        {
            if (streaks != null) streaks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (leaves != null) leaves.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (dust != null) dust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // ---------------------------------------------------------------- chevrones del piso

        private void BuildChevrons()
        {
            Material material = propMaterial != null ? propMaterial : windLook != null ? windLook.material : null;
            if (material == null) return;
            if (chevronMesh == null) chevronMesh = BuildChevronMesh();
            for (int i = 0; i < ChevronCount; i++)
            {
                var go = new GameObject("Chevron_" + i);
                go.transform.SetParent(rig, false);
                go.transform.localScale = Vector3.one * ChevronSize;
                go.AddComponent<MeshFilter>().sharedMesh = chevronMesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.enabled = false;
                chevrons[i] = go.transform;
                chevronRenderers[i] = mr;
            }
        }

        private void SetChevrons(bool visible, Color color)
        {
            chevronsVisible = visible;
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            for (int i = 0; i < ChevronCount; i++)
            {
                MeshRenderer mr = chevronRenderers[i];
                if (mr == null) continue;
                mr.enabled = visible;
                mr.SetPropertyBlock(block);
            }
        }

        /// <summary>Reparte los chevrones a lo largo de la zona y los corre con el viento (solo transformaciones, sin allocations).</summary>
        private void LayoutChevrons()
        {
            float length = LabWindZone.Size.z;
            float spacing = length / ChevronCount;
            for (int i = 0; i < ChevronCount; i++)
            {
                Transform t = chevrons[i];
                if (t == null) continue;
                float z = Mathf.Repeat(chevronPhase + i * spacing, length) - length * 0.5f;
                t.localPosition = new Vector3(0f, ChevronHeight, z);
            }
        }

        /// <summary>Chevron plano (una «V» que apunta a +Z), normal hacia arriba, low-poly: 6 vertices, 4 triangulos.</summary>
        private static Mesh BuildChevronMesh()
        {
            const float half = 0.5f, depth = 0.25f, thick = 0.25f;
            var mesh = new Mesh { name = "LabWindChevron" };
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, depth), new Vector3(0f, 0f, depth - thick),       // punta (frente, fondo)
                new Vector3(-half, 0f, -depth), new Vector3(-half, 0f, -depth - thick), // ala izquierda
                new Vector3(half, 0f, -depth), new Vector3(half, 0f, -depth - thick),   // ala derecha
            };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.uv = new[]
            {
                new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(1f, 0f),
            };
            // Horario visto desde arriba (cara frontal en Unity).
            mesh.triangles = new[] { 0, 1, 3, 0, 3, 2, 0, 4, 5, 0, 5, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------------------------------------------------------------- manga y banderines (10 Hz)

        private void TickSock()
        {
            sockYaw = Mathf.MoveTowardsAngle(sockYaw, targetYaw, YawRate * TickInterval);
            sockTension = Mathf.MoveTowards(sockTension, targetTension, TensionRate * TickInterval);
            float time = Time.time;
            // Aleteo: mas rapido y amplio cuanto mas tensa; nada en calma.
            float amplitude = sockTension > 0.01f ? 2f + 6f * sockTension : 0f;
            float flutter = Mathf.Sin(time * (3f + 4f * sockTension)) * amplitude;
            float slack = 1f - sockTension;

            bool hasSegments = sockSegments != null && sockSegments.Length > 0;
            if (sockPivot != null)
                sockPivot.rotation = Quaternion.Euler(hasSegments ? 0f : slack * MaxSockDroop, sockYaw + flutter, 0f);
            if (hasSegments)
            {
                float droop = slack * MaxSockDroop / sockSegments.Length;
                for (int i = 0; i < sockSegments.Length; i++)
                {
                    Transform s = sockSegments[i];
                    if (s == null) continue;
                    float wobble = Mathf.Sin(time * (4f + 5f * sockTension) + i * 0.9f) * amplitude * 0.5f;
                    s.localRotation = Quaternion.Euler(droop, wobble, 0f);
                }
            }
            if (pennants != null)
            {
                for (int i = 0; i < pennants.Length; i++)
                {
                    Transform f = pennants[i];
                    if (f == null) continue;
                    float wave = Mathf.Sin(time * (5f + 6f * sockTension) + i * 1.3f) * amplitude * 1.5f;
                    f.rotation = Quaternion.Euler(slack * MaxPennantDroop, sockYaw + wave, 0f);
                }
            }
        }

        /// <summary>Manga low-poly con primitivas: mastil y cuatro anillos a rayas que se afinan (solo si no se asigno una).</summary>
        private void BuildSock()
        {
            Transform pole = Primitive("Mastil", transform, PoleColor);
            pole.localPosition = new Vector3(0f, PoleHeight * 0.5f, 0f);
            pole.localScale = new Vector3(PoleRadius * 2f, PoleHeight * 0.5f, PoleRadius * 2f);

            sockPivot = new GameObject("Manga").transform;
            sockPivot.SetParent(transform, false);
            sockPivot.localPosition = new Vector3(0f, PoleHeight, 0f);

            sockSegments = new Transform[SockSegmentCount];
            Transform parent = sockPivot;
            float radius = SockRadius;
            for (int i = 0; i < SockSegmentCount; i++)
            {
                var seg = new GameObject("Segmento_" + i).transform;
                seg.SetParent(parent, false);
                seg.localPosition = new Vector3(0f, 0f, i == 0 ? 0f : SockSegmentLength);
                Transform ring = Primitive("Tela", seg, i % 2 == 0 ? SockRed : SockWhite);
                ring.localRotation = Quaternion.Euler(90f, 0f, 0f); // el cilindro va en Y: se acuesta sobre +Z
                ring.localPosition = new Vector3(0f, 0f, SockSegmentLength * 0.5f);
                ring.localScale = new Vector3(radius * 2f, SockSegmentLength * 0.5f, radius * 2f);
                sockSegments[i] = seg;
                parent = seg;
                radius *= SockTaper;
            }
        }

        private Transform Primitive(string name, Transform parent, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            if (go.TryGetComponent(out Collider col)) Destroy(col);
            go.transform.SetParent(parent, false);
            var mr = go.GetComponent<MeshRenderer>();
            if (propMaterial != null) mr.sharedMaterial = propMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor(BaseColorId, color);
            mpb.SetColor(ColorId, color);
            mr.SetPropertyBlock(mpb);
            return go.transform;
        }
    }
}
