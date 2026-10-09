using UnityEngine;
using SpellSimulator.Player;
using SpellSimulator.Reactions;

namespace SpellSimulator.World
{
    /// <summary>
    /// Cuerpo temporal (rocas): pasado <c>lifeSeconds</c> desde su creacion se achica y se destruye. Si el jugador lo lleva
    /// en ese momento, se suelta primero. Tambien desaparece al reiniciar el mundo y si cae fuera del mapa.
    /// </summary>
    [DisallowMultipleComponent]
    public class ExpiringBody : MonoBehaviour
    {
        private const float VoidHeight = -50f;

        private float lifeSeconds, shrinkSeconds;
        private float age, shrinkT;
        private bool expiring;
        private Vector3 shrinkFrom;
        private Rigidbody rb;

        /// <summary>True una vez que empezo a achicarse (ya no cuenta como "viva").</summary>
        public bool IsExpiring => expiring;

        public void Init(float life, float shrink)
        {
            lifeSeconds = life;
            shrinkSeconds = Mathf.Max(0.05f, shrink);
            age = 0f;
            TryGetComponent(out rb);
        }

        private void OnEnable() => MagicWorld.WorldReset += OnWorldReset;
        private void OnDisable() => MagicWorld.WorldReset -= OnWorldReset;

        private void OnWorldReset() => Destroy(gameObject);

        /// <summary>Empieza a achicarse ya (limite de rocas vivas, fin de vida).</summary>
        public void BeginExpire()
        {
            if (expiring) return;
            expiring = true;
            shrinkT = 0f;
            shrinkFrom = transform.localScale;
            ReleaseIfHeld();
        }

        private void ReleaseIfHeld()
        {
            if (rb == null) return;
            PlayerHeld.ReleaseBody(rb);
            if (TryGetComponent(out Movable movable)) movable.OnDrop();
            PinnedBody.Unpin(rb);
        }

        private void Update()
        {
            if (transform.position.y < VoidHeight) { Destroy(gameObject); return; }
            if (!expiring)
            {
                age += Time.deltaTime;
                if (age >= lifeSeconds) BeginExpire();
                return;
            }
            shrinkT += Time.deltaTime / shrinkSeconds;
            if (shrinkT >= 1f) { Destroy(gameObject); return; }
            transform.localScale = shrinkFrom * (1f - Mathf.SmoothStep(0f, 1f, shrinkT));
        }
    }
}
