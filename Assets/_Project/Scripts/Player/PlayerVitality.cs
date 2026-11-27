using UnityEngine;
using SpellSimulator.Combat;

namespace SpellSimulator.Player
{
    /// <summary>
    /// Vida del jugador en el modo Libre (paso V, D28): recibe el daño de los hechizos y la quemadura (MagicWorld), se
    /// recupera sola tras un rato sin daño y, al llegar a 0, reaparece en el Nexo con la vida llena. Con el interruptor
    /// «sin daño» (NoDamageSetting, palanca del laboratorio) no recibe daño. En Duelo no hace nada: manda NetDuel.
    /// Revisa el estado cada <see cref="TickSeconds"/> en FixedUpdate (sin allocations).
    /// </summary>
    [RequireComponent(typeof(PlayerSpawn))]
    public class PlayerVitality : MonoBehaviour
    {
        private const float TickSeconds = 0.25f;

        private Health health;
        private PlayerSpawn spawn;
        private float lastDamageTime = -999f;
        private float lastWarningTime = -999f;
        private float tick;

        public Health Health => health;

        private void Awake()
        {
            spawn = GetComponent<PlayerSpawn>();
            health = GetComponent<Health>();
            if (health == null) health = gameObject.AddComponent<Health>();
            health.SetMax(VitalityRules.MaxHealth);
            ApplyProtection();
        }

        private void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            NoDamageSetting.Changed += ApplyProtection;
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
            NoDamageSetting.Changed -= ApplyProtection;
        }

        private static bool InDuel => Net.NetDuel.Active != null;

        private void FixedUpdate()
        {
            tick += Time.fixedDeltaTime;
            if (tick < TickSeconds) return;
            float dt = tick;
            tick = 0f;
            ApplyProtection();
            if (InDuel || health.IsDead) return;
            health.Heal(VitalityRules.RegenAmount(Time.time - lastDamageTime, dt));
        }

        private void ApplyProtection() => health.Invulnerable = VitalityRules.Protected(NoDamageSetting.On, InDuel);

        private void OnDamaged(Health h, float amount)
        {
            lastDamageTime = Time.time;
            if (InDuel || !VitalityRules.ShouldWarnLow(h.Fraction, Time.time - lastWarningTime)) return;
            lastWarningTime = Time.time;
            Hud.Notify("vitality", "¡Cuidado! Te queda poca vida: alejate del fuego y los hechizos.", 3f);
        }

        private void OnDied(Health h)
        {
            if (InDuel) return;
            Hud.Notify("vitality", "Te desmayaste: volvés al Nexo.\nEn el laboratorio hay una palanca para jugar sin daño.", 5f);
            spawn.Respawn();
            health.ResetHealth();
            lastDamageTime = -999f;
        }
    }
}
