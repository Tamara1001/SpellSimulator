using UnityEngine;
using SpellSimulator.Combat;
using SpellSimulator.Compiler;
using SpellSimulator.Output;

namespace SpellSimulator.Reactions
{
    /// <summary>
    /// Mundo reactivo (SYSTEMS_DESIGN 5): a TickRate Hz cruza los hechizos vivos (MagicField) con los objetos
    /// reactivos, propaga el fuego entre vecinos y avanza el estado de cada uno. Buffers preasignados: sin GC por tick.
    /// </summary>
    public class MagicWorld : MonoBehaviour
    {
        public const float TickRate = 15f;
        private const int MaxHits = 64;

        [SerializeField] private ReactionTable reactions;
        [SerializeField, Tooltip("De aca salen los looks (llamas de lo que arde).")]
        private SpellOutputController output;
        [SerializeField, Tooltip("Alcance del fuego hacia los vecinos, ademas del tamanio del objeto (m).")]
        private float spreadReach = 0.35f;
        [SerializeField, Tooltip("Impulso de los fragmentos al romperse.")]
        private float shatterImpulse = 2.5f;
        [SerializeField, Tooltip("Daño de los hechizos y la quemadura sobre lo que tiene vida (muñecos, jugadores en Duelo).")]
        private DamageTable damage;

        private static readonly Collider[] hits = new Collider[MaxHits];
        private static readonly ElementReactive[] found = new ElementReactive[MaxHits];
        private float accumulator;

        private void Update()
        {
            accumulator += Time.deltaTime;
            float step = 1f / TickRate;
            // Un tick por frame como maximo: si el juego va lento, el mundo reacciona mas lento (no en rafagas).
            if (accumulator < step) return;
            float dt = Mathf.Min(accumulator, step * 3f);
            accumulator = 0f;
            Tick(dt);
        }

        private void Tick(float dt)
        {
            if (reactions == null) return;
            ExposeToSpells();
            ExposeToWater();
            SpreadFire();
            SnuffSmallFlames();
            DamageHealth(dt);

            var all = ElementReactive.All;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var reactive = all[i];
                if (reactive == null || !reactive.isActiveAndEnabled) continue;
                ReactionEvents events = reactive.Tick(dt);
                if (events == ReactionEvents.None) continue;
                Handle(reactive, events);
            }
        }

        private void ExposeToSpells()
        {
            var fields = MagicField.Active;
            for (int f = 0; f < fields.Count; f++)
            {
                var field = fields[f];
                int n = Gather(field.Frame.Origin, field.ReachRadius);
                for (int i = 0; i < n; i++)
                {
                    var reactive = found[i];
                    if (field.Source != null && reactive.gameObject == field.Source) continue;
                    float intensity = field.ExposureAt(reactive.WorldBounds.center);
                    if (intensity <= 0f) continue;
                    reactive.AddExposure(reactions.For(field.Element, reactive.Profile), intensity);
                }
            }
        }

        /// <summary>Daño de los hechizos (segun su intensidad en el blanco) y de la quemadura a cada Health viva.</summary>
        private void DamageHealth(float dt)
        {
            if (damage == null) return;
            var healths = Health.All;
            var fields = MagicField.Active;
            for (int h = 0; h < healths.Count; h++)
            {
                var health = healths[h];
                if (health == null || !health.isActiveAndEnabled || health.IsDead) continue;
                Vector3 center = health.Center;
                float total = 0f;
                for (int f = 0; f < fields.Count; f++)
                {
                    var field = fields[f];
                    if (field.Source != null && field.Source == health.gameObject) continue;
                    total += DamageModel.SpellDamage(damage.values, field.Element, field.ExposureAt(center), dt);
                }
                var reactive = health.GetComponent<ElementReactive>();
                total += DamageModel.BurnDamage(damage.values, reactive != null && reactive.State.Burning, dt);
                health.Damage(total);
            }
        }

        /// <summary>Lo que esta en un cuerpo de agua se moja y se enfria segun cuanto esta sumergido.</summary>
        private void ExposeToWater()
        {
            var waters = WaterVolume.All;
            for (int w = 0; w < waters.Count; w++)
            {
                var water = waters[w];
                Bounds b = water.Bounds;
                int n = Gather(b.center, b.extents.magnitude);
                for (int i = 0; i < n; i++)
                {
                    float submerged = water.Submerged(found[i].WorldBounds);
                    if (submerged > 0f) found[i].AddExposure(Immersion, submerged);
                }
            }
        }

        /// <summary>Enfriamiento brusco con el que un soplo de viento apaga una llama chica (vela, antorcha).</summary>
        private static readonly Exposure Snuff = new Exposure { Heat = -40f };

        /// <summary>El cono de un hechizo de viento apaga las llamas chicas que alcanza.</summary>
        private void SnuffSmallFlames()
        {
            var all = ElementReactive.All;
            for (int i = 0; i < all.Count; i++)
            {
                var r = all[i];
                if (r == null || !r.isActiveAndEnabled || !r.State.Burning) continue;
                if (Output.WindCone.IsSmallFlame(r) && Output.WindCone.ShouldSnuff(r.WorldBounds.center)) r.AddExposure(Snuff, 1f);
            }
        }

        /// <summary>Exposicion por segundo de estar completamente sumergido.</summary>
        private static readonly Exposure Immersion = new Exposure { Heat = -4f, Wet = 3f };

        private void SpreadFire()
        {
            var all = ElementReactive.All;
            for (int i = 0; i < all.Count; i++)
            {
                var burning = all[i];
                if (burning == null || !burning.isActiveAndEnabled || !burning.State.Burning || burning.Profile == null) continue;
                Bounds b = burning.WorldBounds;
                float heat = burning.Profile.parameters.spreadHeatPerSecond;
                if (heat <= 0f) continue;
                float reach = b.extents.magnitude + spreadReach;
                int n = Gather(b.center, reach);
                for (int k = 0; k < n; k++)
                {
                    var neighbour = found[k];
                    if (neighbour == burning) continue;
                    float distance = Vector3.Distance(neighbour.WorldBounds.ClosestPoint(b.center), b.center);
                    float falloff = 1f - Mathf.Clamp01(distance / reach);
                    neighbour.AddExposure(new Exposure { Heat = heat }, falloff);
                }
            }
        }

        private void Handle(ElementReactive reactive, ReactionEvents events)
        {
            if ((events & ReactionEvents.Ignited) != 0)
            {
                reactive.SetFlames(output != null ? output.LookFor(ElementType.Fire) : null, true);
                Debug.Log($"[Mundo] {reactive.name} se prendio fuego.");
            }
            if ((events & (ReactionEvents.Extinguished | ReactionEvents.BurnedOut)) != 0)
                reactive.SetFlames(null, false);
            if ((events & ReactionEvents.Extinguished) != 0) Debug.Log($"[Mundo] {reactive.name} se apago.");
            if ((events & ReactionEvents.Broken) != 0)
            {
                Debug.Log($"[Mundo] {reactive.name} se rompio.");
                Shatter.Break(reactive, reactive.WorldBounds.center + Vector3.down * 0.1f, shatterImpulse * Mathf.Sqrt(reactive.Mass));
            }
        }

        /// <summary>Reactivos distintos cuyo collider toca la esfera.</summary>
        private static int Gather(Vector3 center, float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(center, radius, hits, ~0, QueryTriggerInteraction.Collide);
            int unique = 0;
            for (int i = 0; i < count; i++)
            {
                var reactive = hits[i].GetComponentInParent<ElementReactive>();
                if (reactive == null || !reactive.isActiveAndEnabled) continue;
                bool seen = false;
                for (int j = 0; j < unique; j++) if (found[j] == reactive) { seen = true; break; }
                if (!seen) found[unique++] = reactive;
            }
            return unique;
        }

        /// <summary>Se emite al reiniciar el mundo.</summary>
        public static event System.Action WorldReset;

        /// <summary>Reinicio del mundo: objetos sanos y en su lugar, sin fragmentos ni hechizos.</summary>
        public void ResetWorld()
        {
            // En red un cliente no reinicia por su cuenta: se lo pide al anfitrion, que reinicia a todos.
            if (Net.NetWorld.InterceptReset()) return;
            WorldReset?.Invoke();
            if (output != null) output.ClearAll();
            Shatter.ClearAll();
            GroundMarks.ClearAll();
            // Todos los lienzos vuelven a quedar en blanco (menos las paginas del grimorio).
            var caster = Object.FindFirstObjectByType<Casting.SpellCaster>();
            foreach (var canvas in Object.FindObjectsByType<SpellSimulator.Canvas.DrawingCanvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas == null || canvas.GetComponentInParent<Learning.Grimoire>() != null) continue;
                canvas.Clear();
                if (caster != null) caster.OnSealEdited(canvas);
            }
            foreach (var reactive in ElementReactive.All) if (reactive != null) reactive.ResetState();
            foreach (var health in Health.All) if (health != null) health.ResetHealth();
            foreach (var dummy in World.TrainingDummy.All) if (dummy != null) dummy.Restore();
            // Lo que el jugador arrastra (G) se suelta antes de reubicar: si no, el reinicio lo deja a medio camino.
            Player.PlayerHeld.ReleaseAll();
            World.Respawnable.RespawnAll();
            Debug.Log("[Mundo] Reinicio.");
        }
    }
}
