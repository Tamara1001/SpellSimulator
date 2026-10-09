using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.World;

namespace SpellSimulator.Reactions
{
    /// <summary>
    /// Va en cada ParticleSystem que lleva agua (pistas del hechizo, derrame de un recipiente). Recibe:
    /// - OnParticleCollision: gotas que chocan con el mundo estatico -> impactos para los charcos (buffer del
    ///   <see cref="WaterPuddleManager"/>) o agua para un recipiente estatico.
    /// - OnParticleTrigger: gotas que entran por la boca de un <see cref="WaterContainer"/> (modulo Trigger) -> lo llenan y mueren.
    /// Tambien aplica UNA vez el multiplicador de caudal de la tinta cuando la pista empieza a emitir.
    /// Listas estaticas preasignadas: sin allocations por frame.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class WaterDropSink : MonoBehaviour
    {
        private const int EventCapacity = 256;
        private const int TriggerCapacity = 512;

        private static readonly List<ParticleCollisionEvent> collisions = new List<ParticleCollisionEvent>(EventCapacity);
        private static readonly List<ParticleSystem.Particle> entered = new List<ParticleSystem.Particle>(TriggerCapacity);

        private ParticleSystem ps;
        private float litersPerDrop = WaterMath.DropLiters;
        private float inkPower = 1f;
        private float flowMultiplier = 1f;
        private bool boostPending;
        private WaterContainer ignore;

        /// <summary>Configura la pista: litros por gota, tinta, multiplicador de caudal y un recipiente a ignorar (el que derrama).</summary>
        public void Init(float inkPower, float flowMultiplier, WaterContainer ignoreContainer = null)
        {
            ps = GetComponent<ParticleSystem>();
            this.inkPower = Mathf.Max(inkPower, 0f);
            litersPerDrop = WaterMath.DropVolume(this.inkPower);
            this.flowMultiplier = flowMultiplier;
            boostPending = Mathf.Abs(flowMultiplier - 1f) > 0.01f;
            ignore = ignoreContainer;
        }

        private void Update()
        {
            if (!boostPending || ps == null) return;
            // SpellManifestation fija la tasa en el golpe: se multiplica una sola vez cuando aparece.
            var emission = ps.emission;
            float rate = emission.rateOverTimeMultiplier;
            if (rate > 0f)
            {
                emission.rateOverTimeMultiplier = rate * flowMultiplier;
                boostPending = false;
            }
            else if (ps.particleCount > 0 && flowMultiplier > 1f)
            {
                // Rafaga (Emit en el golpe, sin tasa): se suma la parte extra de una vez.
                ps.Emit(Mathf.RoundToInt(ps.particleCount * (flowMultiplier - 1f)));
                boostPending = false;
            }
        }

        private void OnParticleCollision(GameObject other)
        {
            if (ps == null) ps = GetComponent<ParticleSystem>();
            int n = ps.GetCollisionEvents(other, collisions);
            if (n <= 0) return;
            var container = other.GetComponentInParent<WaterContainer>();
            if (container != null && container != ignore)
            {
                container.Fill(litersPerDrop * n);
                return;
            }
            for (int i = 0; i < n; i++)
            {
                var e = collisions[i];
                WaterPuddleManager.AddImpact(e.intersection, e.normal, litersPerDrop, inkPower);
            }
        }

        private void OnParticleTrigger()
        {
            if (ps == null) ps = GetComponent<ParticleSystem>();
            int n = ps.GetTriggerParticles(ParticleSystemTriggerEventType.Enter, entered);
            if (n <= 0) return;
            var containers = WaterContainer.All;
            for (int i = 0; i < n; i++)
            {
                var p = entered[i];
                for (int c = 0; c < containers.Count; c++)
                {
                    var container = containers[c];
                    if (container == null || container == ignore || !container.Accepts(p.position)) continue;
                    container.Fill(litersPerDrop);
                    p.remainingLifetime = 0f; // la gota queda adentro
                    entered[i] = p;
                    break;
                }
            }
            ps.SetTriggerParticles(ParticleSystemTriggerEventType.Enter, entered);
        }
    }
}
