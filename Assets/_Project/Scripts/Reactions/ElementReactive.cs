using System;
using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Reactions
{
    /// <summary>
    /// Objeto que reacciona a la magia segun su material: arde, se apaga, se moja, se carboniza y se rompe.
    /// MagicWorld le suma exposicion y lo avanza en cada tick; aca solo hay estado y aspecto.
    /// </summary>
    public class ElementReactive : MonoBehaviour, ISurfaceTint, IElementMatter
    {
        /// <summary>Todos los reactivos de la escena, activos o rotos (para el reinicio).</summary>
        public static readonly List<ElementReactive> All = new List<ElementReactive>();

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private MaterialProfile profile;
        [SerializeField, Tooltip("Pintar carbonizado/mojado en el renderer. Apagarlo si otro componente maneja el color (ej. SealFeedback en el papel).")]
        private bool tintRenderer = true;

        [SerializeField] private ReactiveState state = ReactiveState.Fresh;

        /// <summary>Se emite con cada cambio de fase (encendido, apagado, empapado, roto...).</summary>
        public event Action<ElementReactive, ReactionEvents> Reacted;

        public MaterialProfile Profile => profile;
        /// <summary>Lo que arde es fuego; si no, el elemento de su material.</summary>
        public Compiler.ElementType Element => state.Burning ? Compiler.ElementType.Fire : profile != null ? profile.element : Compiler.ElementType.None;
        public ReactiveState State => state;
        public Bounds WorldBounds => rend != null ? rend.bounds : new Bounds(transform.position, Vector3.one * 0.5f);

        private Exposure pending;
        private Renderer rend;
        private MaterialPropertyBlock block;
        private Color baseColor = Color.white;
        private ParticleSystem flames;
        private Rigidbody body;

        private void Awake()
        {
            rend = GetComponentInChildren<Renderer>();
            body = GetComponent<Rigidbody>();
            block = new MaterialPropertyBlock();
            if (rend != null && rend.sharedMaterial != null && rend.sharedMaterial.HasProperty(BaseColorId))
                baseColor = rend.sharedMaterial.GetColor(BaseColorId);
            All.Add(this);
        }

        private void OnDestroy() => All.Remove(this);

        /// <summary>Asigna el material (objetos creados en runtime, ej. rocas que emergen).</summary>
        public void SetProfile(MaterialProfile material)
        {
            profile = material;
            RefreshLook();
        }

        public void AddExposure(in Exposure exposure, float scale) => pending.Add(exposure, scale);

        /// <summary>Avanza el estado con lo acumulado desde el ultimo tick. Lo llama MagicWorld.</summary>
        public ReactionEvents Tick(float dt)
        {
            if (profile == null) { pending = default; return ReactionEvents.None; }
            ReactionEvents events = ReactionModel.Step(ref state, profile.parameters, pending, dt);
            pending = default;
            if (events != ReactionEvents.None) Reacted?.Invoke(this, events);
            RefreshLook();
            return events;
        }

        /// <summary>Color con carbon y humedad aplicados sobre un color base (lo usa SealFeedback para el papel).</summary>
        public Color Tint(Color color)
        {
            if (profile == null) return color;
            color = Color.Lerp(color, profile.charColor, state.BurnProgress * 0.9f);
            Color wet = color * (1f - profile.wetDarkening);
            wet.a = color.a;
            return Color.Lerp(color, wet, state.Wetness);
        }

        /// <summary>Vuelve a su estado inicial (reinicio del mundo).</summary>
        public void ResetState()
        {
            state = ReactiveState.Fresh;
            pending = default;
            gameObject.SetActive(true);
            SetFlames(null, false);
            RefreshLook();
        }

        /// <summary>Prende o apaga las llamas visibles. MagicWorld pasa el look del fuego.</summary>
        public void SetFlames(Output.ElementLook fireLook, bool on)
        {
            if (on && flames == null && fireLook != null) flames = BurnEffect.Create(transform, WorldBounds, fireLook);
            if (flames == null) return;
            if (on && !flames.isPlaying) flames.Play();
            else if (!on && flames.isPlaying) flames.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void RefreshLook()
        {
            if (!tintRenderer || rend == null) return;
            rend.GetPropertyBlock(block);
            block.SetColor(BaseColorId, Tint(baseColor));
            rend.SetPropertyBlock(block);
        }

        /// <summary>Masa para repartir entre fragmentos.</summary>
        public float Mass => body != null ? body.mass : 1f;
    }
}
