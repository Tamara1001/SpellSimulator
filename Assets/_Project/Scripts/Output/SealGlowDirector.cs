using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Compiler;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Brillo del anillo mientras un sello carga (retardo de lanzamiento). Escucha SpellCaster.SealCharging /
    /// SealChargeEnded y dibuja una banda anular aditiva del color del elemento sobre la cara con tinta del lienzo.
    /// La crea SpellCaster con AddComponent sobre si mismo. Pool fijo de anillos: mallas y materiales se crean una vez.
    /// </summary>
    [DisallowMultipleComponent]
    public class SealGlowDirector : MonoBehaviour
    {
        public const string ShaderName = "SpellSimulator/SealGlow";
        private const string ResourcesShaderName = "SealGlow";
        private const int MaxRings = 8;
        private const int Segments = 48;
        /// <summary>Separacion (m) de la superficie del papel para que no haya z-fighting.</summary>
        private const float SurfaceOffset = 0.002f;
        /// <summary>Ancho de tinta (m) si el trazo usa el del lienzo (Stroke.Width == 0).</summary>
        private const float FallbackInkWidth = 0.012f;
        /// <summary>Halo minimo (m) a cada lado de la tinta.</summary>
        private const float MinHalo = 0.01f;
        /// <summary>Halo relativo al radio del anillo.</summary>
        private const float HaloPerRadius = 0.08f;

        [SerializeField, Tooltip("Shader del brillo. Vacio = SpellSimulator/SealGlow (Resources).")]
        private Shader glowShader;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int PulseId = Shader.PropertyToID("_Pulse");

        private enum Phase { Idle, Charging, Flash, Fade }

        private sealed class Ring
        {
            public GameObject go;
            public Transform tr;
            public Mesh mesh;
            public Material material;
            public DrawingCanvas canvas;
            public Vector2 centerUV;
            public Phase phase;
            public float startTime;
            public float seconds;
            public float endTime;
            public float endIntensity;
            public float lastIntensity;
            public float pulsePhase;
        }

        private readonly Ring[] rings = new Ring[MaxRings];
        private readonly Vector3[] vertexBuffer = new Vector3[Segments * 2];
        private Vector2[] uvTemplate;
        private int[] triangleTemplate;
        private Shader resolvedShader;
        private SpellCaster caster;
        private bool shaderMissingLogged;

        private void OnEnable()
        {
            if (caster == null) caster = GetComponent<SpellCaster>();
            if (caster == null)
            {
                Debug.LogWarning("[SealGlow] Sin SpellCaster en el mismo objeto: el brillo de carga queda apagado.");
                return;
            }
            caster.SealCharging += OnSealCharging;
            caster.SealChargeEnded += OnSealChargeEnded;
        }

        private void OnDisable()
        {
            if (caster != null)
            {
                caster.SealCharging -= OnSealCharging;
                caster.SealChargeEnded -= OnSealChargeEnded;
            }
            for (int i = 0; i < rings.Length; i++) Deactivate(rings[i]);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < rings.Length; i++)
            {
                Ring r = rings[i];
                if (r == null) continue;
                if (r.go != null) Destroy(r.go);
                if (r.mesh != null) Destroy(r.mesh);
                if (r.material != null) Destroy(r.material);
                rings[i] = null;
            }
        }

        private void OnSealCharging(SealChargeInfo info)
        {
            if (info.canvas == null) return;
            Ring ring = Acquire(info.canvas);
            if (ring == null) return;

            ring.canvas = info.canvas;
            ring.centerUV = info.ringCenterUV;
            ring.seconds = Mathf.Max(0.05f, info.seconds);
            ring.startTime = Time.time;
            ring.pulsePhase = 0f;
            ring.phase = Phase.Charging;
            ring.lastIntensity = SealGlowCurve.StartIntensity;

            float radius = Mathf.Max(0.01f, info.ringRadiusWorld);
            float halfBand = InkWidth(info.canvas) * 0.5f + Mathf.Max(MinHalo, radius * HaloPerRadius);
            BuildRing(ring.mesh, Mathf.Max(0f, radius - halfBand), radius + halfBand);
            ring.material.SetColor(ColorId, ColorFor(info.element, info.sigilId));
            ring.material.SetFloat(IntensityId, ring.lastIntensity);
            ring.material.SetFloat(PulseId, 0.5f);

            UpdatePose(ring);
            if (!ring.go.activeSelf) ring.go.SetActive(true);
        }

        private void OnSealChargeEnded(DrawingCanvas canvas, bool fired)
        {
            for (int i = 0; i < rings.Length; i++)
            {
                Ring r = rings[i];
                if (r == null || r.phase != Phase.Charging || r.canvas != canvas) continue;
                r.endTime = Time.time;
                r.endIntensity = r.lastIntensity;
                r.phase = fired ? Phase.Flash : Phase.Fade;
            }
        }

        // Todo en LateUpdate: el jugador puede mover el lienzo durante la carga y el anillo lo sigue.
        private void LateUpdate()
        {
            float now = Time.time;
            float dt = Time.deltaTime;
            for (int i = 0; i < rings.Length; i++)
            {
                Ring r = rings[i];
                if (r == null || r.phase == Phase.Idle) continue;
                if (r.canvas == null || !r.canvas.isActiveAndEnabled) { Deactivate(r); continue; }

                float intensity;
                float pulse = 0.5f;
                switch (r.phase)
                {
                    case Phase.Charging:
                    {
                        float t = (now - r.startTime) / r.seconds;
                        intensity = SealGlowCurve.Intensity(t);
                        // Fase acumulada: cambiar la frecuencia no produce saltos en el pulso.
                        r.pulsePhase += SealGlowCurve.PulseHz(t) * dt * (Mathf.PI * 2f);
                        if (r.pulsePhase > Mathf.PI * 2f) r.pulsePhase -= Mathf.PI * 2f;
                        pulse = 0.5f + 0.5f * Mathf.Sin(r.pulsePhase);
                        break;
                    }
                    case Phase.Flash:
                    {
                        float s = now - r.endTime;
                        if (s >= SealGlowCurve.FlashDuration) { Deactivate(r); continue; }
                        intensity = SealGlowCurve.FlashIntensity(s);
                        break;
                    }
                    default:
                    {
                        float s = now - r.endTime;
                        if (s >= SealGlowCurve.FadeOutSeconds) { Deactivate(r); continue; }
                        intensity = SealGlowCurve.FadeOut(r.endIntensity, s);
                        break;
                    }
                }

                r.lastIntensity = intensity;
                r.material.SetFloat(IntensityId, intensity);
                r.material.SetFloat(PulseId, pulse);
                UpdatePose(r);
            }
        }

        /// <summary>Anillo del lienzo (se reinicia), uno libre, o el mas viejo si el pool esta lleno.</summary>
        private Ring Acquire(DrawingCanvas canvas)
        {
            int free = -1, oldest = -1;
            float oldestTime = float.MaxValue;
            for (int i = 0; i < rings.Length; i++)
            {
                Ring r = rings[i];
                if (r != null && r.phase != Phase.Idle && r.canvas == canvas) return r;
                if (free < 0 && (r == null || r.phase == Phase.Idle)) free = i;
                if (r != null && r.startTime < oldestTime) { oldestTime = r.startTime; oldest = i; }
            }
            int index = free >= 0 ? free : oldest;
            if (index < 0) return null;
            if (rings[index] == null) rings[index] = CreateRing(index);
            return rings[index];
        }

        private Ring CreateRing(int index)
        {
            Shader shader = ResolveShader();
            if (shader == null) return null;

            EnsureTemplates();
            var mesh = new Mesh { name = "SealGlowRing" };
            mesh.MarkDynamic();
            BuildRing(mesh, 0.09f, 0.11f);
            mesh.uv = uvTemplate;
            mesh.triangles = triangleTemplate;

            var material = new Material(shader) { name = "SealGlow (runtime)", hideFlags = HideFlags.DontSave };

            var go = new GameObject("SealGlow_" + index);
            go.SetActive(false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            return new Ring { go = go, tr = go.transform, mesh = mesh, material = material, phase = Phase.Idle };
        }

        private Shader ResolveShader()
        {
            if (resolvedShader != null) return resolvedShader;
            resolvedShader = glowShader != null ? glowShader : Shader.Find(ShaderName);
            if (resolvedShader == null) resolvedShader = Resources.Load<Shader>(ResourcesShaderName);
            if (resolvedShader == null && !shaderMissingLogged)
            {
                shaderMissingLogged = true;
                Debug.LogWarning($"[SealGlow] No se encontro el shader {ShaderName}: el brillo de carga queda apagado.");
            }
            return resolvedShader;
        }

        private void EnsureTemplates()
        {
            if (uvTemplate != null) return;
            uvTemplate = new Vector2[Segments * 2];
            triangleTemplate = new int[Segments * 6];
            for (int i = 0; i < Segments; i++)
            {
                float u = (float)i / Segments;
                uvTemplate[i * 2] = new Vector2(u, 0f);
                uvTemplate[i * 2 + 1] = new Vector2(u, 1f);
                int a = i * 2, b = a + 1, c = ((i + 1) % Segments) * 2, d = c + 1;
                int k = i * 6;
                triangleTemplate[k] = a; triangleTemplate[k + 1] = c; triangleTemplate[k + 2] = b;
                triangleTemplate[k + 3] = b; triangleTemplate[k + 4] = c; triangleTemplate[k + 5] = d;
            }
        }

        /// <summary>Reescribe las posiciones de la banda (plano XY local, Z = normal). Solo al empezar una carga.</summary>
        private void BuildRing(Mesh mesh, float innerRadius, float outerRadius)
        {
            for (int i = 0; i < Segments; i++)
            {
                float a = (float)i / Segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                vertexBuffer[i * 2] = new Vector3(cos * innerRadius, sin * innerRadius, 0f);
                vertexBuffer[i * 2 + 1] = new Vector3(cos * outerRadius, sin * outerRadius, 0f);
            }
            mesh.SetVertices(vertexBuffer);
            mesh.RecalculateBounds();
        }

        /// <summary>
        /// Pose en mundo (sin parentar: la escala no uniforme del lienzo deformaria el circulo). Sobre la cara con tinta,
        /// que es por donde sale el hechizo.
        /// </summary>
        private static void UpdatePose(Ring r)
        {
            DrawingCanvas canvas = r.canvas;
            Vector3 normal = canvas.FaceNormal * canvas.InkSign;
            Transform ct = canvas.transform;
            Vector3 up = ct.up;
            if (Mathf.Abs(Vector3.Dot(up, normal)) > 0.99f) up = ct.right;
            r.tr.SetPositionAndRotation(canvas.UVToWorld(r.centerUV) + normal * SurfaceOffset,
                                        Quaternion.LookRotation(normal, up));
        }

        private static void Deactivate(Ring r)
        {
            if (r == null) return;
            r.phase = Phase.Idle;
            r.canvas = null;
            if (r.go != null && r.go.activeSelf) r.go.SetActive(false);
        }

        /// <summary>Ancho mayor de tinta del lienzo (m). Se lee solo al empezar la carga.</summary>
        private static float InkWidth(DrawingCanvas canvas)
        {
            var strokes = canvas.Session.Strokes;
            float width = 0f;
            for (int i = 0; i < strokes.Count; i++)
                if (strokes[i] != null && strokes[i].Width > width) width = strokes[i].Width;
            return width > 0f ? width : FallbackInkWidth;
        }

        /// <summary>
        /// Color base del brillo (la intensidad HDR la pone la curva). Tabla propia: los ElementLook usan gradientes
        /// que nacen casi blancos, asi que no tienen un "color principal" fiable; estos tonos combinan con ellos.
        /// </summary>
        public static Color ColorFor(ElementType element, string sigilId)
        {
            switch (element)
            {
                case ElementType.Fire: return new Color(1f, 0.32f, 0.12f);
                case ElementType.Water: return new Color(0.35f, 0.75f, 1f);
                case ElementType.Wind: return new Color(0.72f, 1f, 0.78f);
                case ElementType.Earth:
                    return sigilId == SpellOutputController.CrystalSigil
                        ? new Color(0.62f, 0.9f, 1f)
                        : new Color(0.85f, 0.55f, 0.22f);
                case ElementType.Light: return new Color(1f, 0.88f, 0.55f);
                default: return new Color(0.9f, 0.9f, 0.9f);
            }
        }
    }
}
