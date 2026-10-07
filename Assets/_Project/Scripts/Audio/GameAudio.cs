using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Combat;
using SpellSimulator.Compiler;
using SpellSimulator.Reactions;
using SpellSimulator.World;

namespace SpellSimulator.Audio
{
    /// <summary>
    /// Traduce eventos del juego a sonidos (AudioDirector). Ningun sistema depende del audio: este componente escucha
    /// sus eventos. Sello preparado, lanzamiento por elemento, chisporroteo si falla, pluma/goma/trapo mientras se usan,
    /// recarga de tinta, fuego que arde (loop), apagado, roturas, salpicaduras, crateres, sellos rotos, reinicio.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        [SerializeField] private SpellCaster caster;
        [SerializeField, Tooltip("Cada cuanto suena el trapo mientras se frota (s).")]
        private float ragInterval = 0.35f;

        private readonly List<PhysicalTool> ragsInUse = new List<PhysicalTool>();
        private float nextRag;

        private AudioDirector Director => AudioDirector.Instance;

        private void OnEnable()
        {
            if (caster != null)
            {
                caster.SealPhaseChanged += OnPhase;
                caster.SpellCompiled += OnCompiled;
            }
            PhysicalTool.UseChanged += OnToolUse;
            PhysicalTool.Refilled += OnRefill;
            PhysicalTool.PickedUp += OnPickUp;
            ElementReactive.AnyReacted += OnReacted;
            WaterVolume.Splashed += OnSplash;
            GroundMarks.Marked += OnMarked;
            SealBreaker.SealBroken += OnSealBroken;
            Health.AnyDied += OnDied;
            MagicWorld.WorldReset += OnReset;
        }

        private void OnDisable()
        {
            if (caster != null)
            {
                caster.SealPhaseChanged -= OnPhase;
                caster.SpellCompiled -= OnCompiled;
            }
            PhysicalTool.UseChanged -= OnToolUse;
            PhysicalTool.Refilled -= OnRefill;
            PhysicalTool.PickedUp -= OnPickUp;
            ElementReactive.AnyReacted -= OnReacted;
            WaterVolume.Splashed -= OnSplash;
            GroundMarks.Marked -= OnMarked;
            SealBreaker.SealBroken -= OnSealBroken;
            Health.AnyDied -= OnDied;
            MagicWorld.WorldReset -= OnReset;
        }

        private void Update()
        {
            if (ragsInUse.Count == 0 || Time.time < nextRag || Director == null) return;
            nextRag = Time.time + ragInterval;
            for (int i = 0; i < ragsInUse.Count; i++)
                if (ragsInUse[i] != null) Director.Play("rag_wipe", ragsInUse[i].transform.position);
        }

        // ---------------------------------------------------------------- sello

        private void OnPhase(DrawingCanvas canvas, SealPhase phase)
        {
            if (phase == SealPhase.Prepared && Director != null) Director.Play("seal_prepared", canvas.transform.position);
        }

        private void OnCompiled(SpellIR spell, DrawingCanvas canvas)
        {
            if (Director == null) return;
            Vector3 at = canvas.transform.position;
            if (spell.valid && spell.active) Director.Play(CastCue(spell.element), at);
            else Director.Play("cast_fizzle", at); // Cast solo ocurre con el anillo cerrado: invalido = sello fallido.
        }

        public static string CastCue(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return "cast_fire";
                case ElementType.Water: return "cast_water";
                case ElementType.Wind: return "cast_wind";
                case ElementType.Earth: return "cast_earth";
                case ElementType.Light: return "cast_light";
                default: return "cast_fizzle";
            }
        }

        private void OnSealBroken(DrawingCanvas canvas)
        {
            if (Director != null) Director.Play("seal_break", canvas.transform.position);
        }

        // ---------------------------------------------------------------- herramientas

        private void OnToolUse(PhysicalTool tool, bool using_)
        {
            if (Director == null) return;
            if (tool.Kind == PhysicalTool.ToolKind.Rag)
            {
                if (using_) { if (!ragsInUse.Contains(tool)) ragsInUse.Add(tool); }
                else ragsInUse.Remove(tool);
                return;
            }
            if (using_) Director.StartLoop(tool, tool.Kind == PhysicalTool.ToolKind.Eraser ? "eraser_loop" : "pen_loop", tool.transform);
            else Director.StopLoop(tool);
        }

        private void OnRefill(PhysicalTool tool)
        {
            if (Director != null) Director.Play("ink_refill", tool.transform.position);
        }

        private void OnPickUp(PhysicalTool tool)
        {
            if (Director != null) Director.Play("pickup", tool.transform.position);
        }

        // ---------------------------------------------------------------- mundo

        private void OnReacted(ElementReactive reactive, ReactionEvents events)
        {
            if (Director == null) return;
            Vector3 at = reactive.WorldBounds.center;
            if ((events & ReactionEvents.Ignited) != 0)
            {
                Director.Play("ignite", at);
                Director.StartLoop(reactive, "fire_loop", reactive.transform);
            }
            if ((events & (ReactionEvents.Extinguished | ReactionEvents.BurnedOut | ReactionEvents.Broken)) != 0)
                Director.StopLoop(reactive);
            if ((events & ReactionEvents.Extinguished) != 0) Director.Play("hiss", at);
            if ((events & ReactionEvents.Broken) != 0)
                Director.Play(reactive.Profile != null && reactive.Profile.element == ElementType.Earth ? "crack_stone" : "crack_wood", at);
        }

        private void OnSplash(Vector3 at, float strength)
        {
            if (Director != null) Director.Play("splash", at, Mathf.Lerp(0.4f, 1f, strength));
        }

        private void OnMarked(ElementType element, Vector3 at)
        {
            if (Director != null && element == ElementType.Earth) Director.Play("rock_rise", at);
        }

        private void OnDied(Health health)
        {
            if (Director != null) Director.Play("thud", health.Center);
        }

        private void OnReset()
        {
            if (Director == null) return;
            var l = FindAnyObjectByType<AudioListener>();
            Director.Play("reset_chime", l != null ? l.transform.position : Vector3.zero);
        }
    }
}
