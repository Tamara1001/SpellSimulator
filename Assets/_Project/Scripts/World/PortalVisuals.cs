using UnityEngine;

namespace SpellSimulator.World
{
    /// <summary>
    /// Animacion de un portal circular (lo agrega MapStructures al generar): el disco (shader SpellSimulator/PortalSurface),
    /// los grabados de la identidad (URP Lit emisivo) y una sola luz puntual tenue. Escucha Portal.StateChanged y
    /// Portal.RouteOpened y anima con MaterialPropertyBlock segun <see cref="PortalVisualCurve"/>, sin allocations por frame.
    /// Dormido no hace nada por frame (queda aplicado el ultimo valor).
    /// </summary>
    [DisallowMultipleComponent]
    public class PortalVisuals : MonoBehaviour
    {
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int OpenId = Shader.PropertyToID("_Open");
        private static readonly int RimId = Shader.PropertyToID("_Rim");
        private static readonly int StateId = Shader.PropertyToID("_State");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private string portalId;
        [SerializeField] private Renderer disc;
        [SerializeField] private Renderer[] glyphs;
        [SerializeField] private Light glow;

        [Header("Ajustes")]
        [SerializeField, ColorUsage(false)] private Color glyphColor = new Color(1f, 0.78f, 0.38f);
        [SerializeField, Tooltip("Emision de los grabados encendidos del todo.")] private float glyphMaxEmission = 2.2f;
        [SerializeField] private float lightMaxIntensity = 1.4f;
        [SerializeField, Range(0.5f, PortalVisualCurve.MaxLightRange)] private float lightRange = 3.5f;

        private Portal portal;
        private MaterialPropertyBlock discBlock;
        private MaterialPropertyBlock glyphBlock;
        private Color ownTint;
        private Color routeTint;
        private bool animating;
        private float cooldownStart;

        /// <summary>Lo llama el builder: id del portal (para el color), disco, grabados y la luz (puede ser null).</summary>
        public void Configure(string id, Renderer discRenderer, Renderer[] glyphRenderers, Light light)
        {
            portalId = id;
            disc = discRenderer;
            glyphs = glyphRenderers;
            glow = light;
            ownTint = PortalVisualCurve.TintFor(portalId);
            routeTint = ownTint;
            SetupLight();
        }

        private void Awake()
        {
            discBlock = new MaterialPropertyBlock();
            glyphBlock = new MaterialPropertyBlock();
            portal = GetComponent<Portal>();
            ownTint = PortalVisualCurve.TintFor(portalId);
            routeTint = ownTint;
            SetupLight();
        }

        private void OnEnable()
        {
            Portal.StateChanged += OnStateChanged;
            Portal.RouteOpened += OnRouteOpened;
            animating = portal != null && portal.State != PortalState.Dormant;
            Refresh();
        }

        private void OnDisable()
        {
            Portal.StateChanged -= OnStateChanged;
            Portal.RouteOpened -= OnRouteOpened;
        }

        private void OnStateChanged(Portal changed)
        {
            if (changed == null || changed != portal) return;
            PortalState state = changed.State;
            if (state == PortalState.Open && !string.IsNullOrEmpty(changed.DestinationId))
                routeTint = PortalVisualCurve.TintFor(changed.DestinationId);
            else if (state == PortalState.Dormant || state == PortalState.Charging)
                routeTint = ownTint;
            if (state == PortalState.Cooldown) cooldownStart = Time.time;
            animating = true;
            Refresh();
        }

        private void OnRouteOpened(Portal from, Portal to)
        {
            // El tinte del disco abierto es el del otro extremo de la ruta.
            if (from == portal && to != null) routeTint = PortalVisualCurve.TintFor(to.Id);
            else if (to == portal && from != null) routeTint = PortalVisualCurve.TintFor(from.Id);
            else return;
            animating = true;
        }

        private void Update()
        {
            if (animating) Refresh();
        }

        private void Refresh()
        {
            if (discBlock == null) return;
            PortalState state = portal != null ? portal.State : PortalState.Dormant;
            float now = Time.time;
            float progress = 0f, secondsLeft = PortalVisualCurve.WarnSeconds;
            if (portal != null)
            {
                if (state == PortalState.Charging) progress = portal.Progress01;
                else if (state == PortalState.Open) secondsLeft = portal.OpenSecondsLeft;
                else if (state == PortalState.Cooldown) progress = (now - cooldownStart) / PortalVisualCurve.CooldownFadeSeconds;
            }

            PortalLevels levels = PortalVisualCurve.Evaluate(state, progress, secondsLeft, now);
            float pulse = PortalVisualCurve.Pulse(state, levels.Charge, now);
            Color tint = state == PortalState.Open || state == PortalState.Cooldown ? routeTint : ownTint;

            if (disc != null)
            {
                disc.GetPropertyBlock(discBlock);
                discBlock.SetColor(TintId, tint);
                discBlock.SetFloat(OpenId, levels.Disc);
                discBlock.SetFloat(RimId, levels.Rim * pulse);
                discBlock.SetFloat(StateId, (float)state);
                disc.SetPropertyBlock(discBlock);
            }

            if (glyphs != null)
            {
                for (int i = 0; i < glyphs.Length; i++)
                {
                    Renderer r = glyphs[i];
                    if (r == null) continue;
                    float level = state == PortalState.Charging
                        ? PortalVisualCurve.Staggered(levels.Charge, i, glyphs.Length)
                        : levels.Glyphs;
                    r.GetPropertyBlock(glyphBlock);
                    glyphBlock.SetColor(EmissionId, glyphColor * (glyphMaxEmission * level * pulse));
                    r.SetPropertyBlock(glyphBlock);
                }
            }

            if (glow != null)
            {
                float intensity = lightMaxIntensity * levels.Light * pulse;
                bool on = intensity > 0.01f;
                if (glow.enabled != on) glow.enabled = on;
                if (on)
                {
                    glow.intensity = intensity;
                    glow.color = tint;
                }
            }

            // Dormido queda quieto: no hace falta seguir actualizando hasta el proximo cambio de estado.
            if (state == PortalState.Dormant) animating = false;
            else if (state == PortalState.Cooldown && progress >= 1f) animating = false;
        }

        private void SetupLight()
        {
            if (glow == null) return;
            glow.type = LightType.Point;
            glow.range = Mathf.Min(lightRange, PortalVisualCurve.MaxLightRange);
            glow.shadows = LightShadows.None;
        }
    }
}
