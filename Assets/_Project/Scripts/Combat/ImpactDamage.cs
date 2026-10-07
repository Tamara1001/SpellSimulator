using UnityEngine;

namespace SpellSimulator.Combat
{
    /// <summary>Convierte golpes fisicos fuertes (rocas, fragmentos, cajas lanzadas) en daño sobre la Health del objeto.</summary>
    [RequireComponent(typeof(Health))]
    public class ImpactDamage : MonoBehaviour
    {
        [SerializeField] private DamageTable table;

        private Health health;

        private void Awake() => health = GetComponent<Health>();

        private void OnCollisionEnter(Collision collision)
        {
            if (table == null) return;
            var other = collision.rigidbody;
            // Golpes contra lo estatico (piso, paredes) cuentan con masa alta: caer fuerte tambien duele.
            float mass = other != null ? other.mass : 20f;
            health.Damage(DamageModel.ImpactDamage(table.values, collision.relativeVelocity.magnitude, mass));
        }
    }
}
