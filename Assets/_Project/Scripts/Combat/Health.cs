using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.Combat
{
    /// <summary>
    /// Vida de algo que puede recibir daño (muñecos de prueba en Libre; jugadores en Duelo). MagicWorld le aplica el
    /// daño de los hechizos y la quemadura; los golpes fisicos los suma ImpactDamage. Muestra numeros flotantes.
    /// </summary>
    public class Health : MonoBehaviour
    {
        public static readonly List<Health> All = new List<Health>();

        [SerializeField] private float maxHealth = 100f;
        [SerializeField, Tooltip("Cada cuanto se muestra el daño acumulado como numero flotante (s).")]
        private float popupInterval = 0.4f;

        public float Max => maxHealth;
        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;
        public float Fraction => maxHealth > 0f ? Current / maxHealth : 0f;

        /// <summary>(salud, daño) en cada golpe.</summary>
        public event Action<Health, float> Damaged;
        public event Action<Health> Died;
        /// <summary>Cualquier Health de la escena que llega a 0 (audio).</summary>
        public static event Action<Health> AnyDied;

        private float pendingPopup;
        private float nextPopup;
        private Collider body;

        private void Awake()
        {
            Current = maxHealth;
            body = GetComponentInChildren<Collider>();
            All.Add(this);
        }

        private void OnDestroy() => All.Remove(this);

        /// <summary>Centro del blanco (para medir la exposicion a un hechizo).</summary>
        public Vector3 Center => body != null ? body.bounds.center : transform.position;

        public void Damage(float amount)
        {
            if (amount <= 0f || IsDead) return;
            Current = Mathf.Max(0f, Current - amount);
            pendingPopup += amount;
            Damaged?.Invoke(this, amount);
            if (IsDead)
            {
                FlushPopup();
                Died?.Invoke(this);
                AnyDied?.Invoke(this);
            }
        }

        /// <summary>Cambia la vida maxima y llena la barra (coberturas, jugadores creados por codigo).</summary>
        public void SetMax(float max)
        {
            maxHealth = Mathf.Max(1f, max);
            Current = maxHealth;
        }

        public void ResetHealth()
        {
            Current = maxHealth;
            pendingPopup = 0f;
        }

        private void Update()
        {
            if (pendingPopup <= 0f || Time.time < nextPopup) return;
            FlushPopup();
        }

        private void FlushPopup()
        {
            if (pendingPopup >= 0.5f) DamageNumbers.Show(Center + Vector3.up * 0.4f, pendingPopup);
            pendingPopup = 0f;
            nextPopup = Time.time + popupInterval;
        }
    }
}
