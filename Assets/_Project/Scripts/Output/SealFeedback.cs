using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Compiler;
using SpellSimulator.Interfaces;
using SpellSimulator.Player;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Feedback diegetico del sello sobre el papel: pulso suave mientras esta preparado (anillo abierto),
    /// destello calido al lanzarse y rojo breve si el sello cerrado fue invalido. Tinta el material del lienzo.
    /// </summary>
    [RequireComponent(typeof(DrawingCanvas))]
    public class SealFeedback : MonoBehaviour
    {
        [SerializeField] private SpellCaster caster;
        [SerializeField] private Color preparedColor = new Color(0.75f, 0.9f, 1f);
        [SerializeField] private Color castColor = new Color(1f, 0.85f, 0.45f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.6f, 0.6f);
        [SerializeField, Tooltip("Pulsos por segundo del estado preparado.")]
        private float pulseSpeed = 2f;
        [SerializeField, Tooltip("Segundos que dura el destello al lanzar o fallar.")]
        private float flashSeconds = 0.8f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private DrawingCanvas canvas;
        private Renderer canvasRenderer;
        private MaterialPropertyBlock block;
        private Color baseColor = Color.white;
        private SealPhase phase;
        private Color flashColor;
        private float flashUntil = -1f;
        private ISurfaceTint surfaceTint;

        private void Awake()
        {
            canvas = GetComponent<DrawingCanvas>();
            surfaceTint = GetComponent<ISurfaceTint>();
            canvasRenderer = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
            if (canvasRenderer.sharedMaterial != null && canvasRenderer.sharedMaterial.HasProperty(BaseColorId))
                baseColor = canvasRenderer.sharedMaterial.GetColor(BaseColorId);
        }

        private void OnEnable()
        {
            if (caster == null) return;
            caster.SealPhaseChanged += OnPhaseChanged;
            caster.SpellCompiled += OnSpellCompiled;
        }

        private void OnDisable()
        {
            if (caster == null) return;
            caster.SealPhaseChanged -= OnPhaseChanged;
            caster.SpellCompiled -= OnSpellCompiled;
        }

        private void OnPhaseChanged(DrawingCanvas target, SealPhase newPhase)
        {
            if (target == canvas) phase = newPhase;
        }

        private void OnSpellCompiled(SpellIR spell, DrawingCanvas target)
        {
            if (target != canvas) return;
            bool launched = spell.valid && spell.active;
            flashColor = launched ? castColor : invalidColor;
            flashUntil = Time.time + flashSeconds;
            if (launched) Haptics.Pulse(HandType.Right, Haptics.CastAmplitude, 0.2f);
        }

        private void Update()
        {
            Color tint = surfaceTint != null ? surfaceTint.Tint(baseColor) : baseColor;
            if (Time.time < flashUntil)
            {
                float t = (flashUntil - Time.time) / flashSeconds;
                tint = Color.Lerp(tint, flashColor, t);
            }
            else if (phase == SealPhase.Prepared)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f);
                tint = Color.Lerp(tint, preparedColor, pulse * 0.6f);
            }

            canvasRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, tint);
            canvasRenderer.SetPropertyBlock(block);
        }
    }
}
