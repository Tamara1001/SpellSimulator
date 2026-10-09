using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Compiler;
using SpellSimulator.Player;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Columna de aire ascendente de un hechizo de viento que sale hacia arriba. Levanta los Rigidbody que entran
    /// (los livianos suben mas) y, via <see cref="UpdraftAt"/>, deja planear al jugador (PlayerMovement la consulta).
    /// Vive mientras vive el hechizo: se registra en OnEnable y se desregistra en OnDisable (el reinicio del mundo destruye el hechizo).
    /// FUTURO: zapatos de viento (doble salto / bota cohete) a lo Witch Hat Atelier: reutilizar esta columna como impulso.
    /// </summary>
    public class WindColumn : MonoBehaviour
    {
        private const int MaxHits = 48;
        private static readonly Collider[] hits = new Collider[MaxHits];
        private static readonly Rigidbody[] bodies = new Rigidbody[MaxHits];

        /// <summary>Columnas vivas (consulta del jugador).</summary>
        public static readonly List<WindColumn> Active = new List<WindColumn>();

        /// <summary>Tope del multiplicador de tinta sobre la velocidad de la columna (evita lanzar al jugador).</summary>
        public const float MaxInkBoost = 1.6f;
        /// <summary>Fraccion superior de la columna en la que la corriente se desvanece (no empuja contra el techo).</summary>
        private const float TopFade = 0.3f;

        private SpellManifestation manifestation;
        private Vector3 origin;
        private float radius;
        private float height;
        private float speedBoost = 1f;

        public void Init(SpellIR spell, SealFrame frame, float inkPower)
        {
            manifestation = GetComponent<SpellManifestation>();
            origin = frame.Origin;
            radius = Mathf.Max(frame.Radius * 1.5f, 0.05f);
            speedBoost = Mathf.Clamp(inkPower, 1f, MaxInkBoost);
            // inkPower tambien alarga la columna, como el cono.
            height = frame.Scale * (6f + 12f * spell.range) * Mathf.Max(inkPower, 1f);
        }

        public float Radius => radius;
        public float Height => height;
        public float MaxSpeed => WindMath.MaxUpdraftSpeed * speedBoost;

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        /// <summary>
        /// Velocidad vertical objetivo (m/s, 0 si no esta dentro de ninguna columna) en una posicion del mundo.
        /// Sin allocations. La consulta el jugador para planear.
        /// </summary>
        public static float UpdraftAt(Vector3 position)
        {
            float best = 0f;
            for (int i = 0; i < Active.Count; i++)
            {
                float v = Active[i].TargetAt(position);
                if (v > best) best = v;
            }
            return best;
        }

        private float Envelope()
        {
            if (manifestation == null) return 0f;
            float t = manifestation.SinceStrike;
            if (t < 0f || !manifestation.InBody) return 0f;
            return manifestation.Envelope(t);
        }

        private float TargetAt(Vector3 position)
        {
            float env = Envelope();
            if (env <= 0f) return 0f;
            Vector3 d = position - origin;
            if (d.y < -0.2f || d.y > height) return 0f;
            float horizontal = new Vector2(d.x, d.z).magnitude;
            if (horizontal > radius) return 0f;
            float fade = Mathf.Clamp01((height - d.y) / (height * TopFade));
            return WindMath.UpdraftTarget(horizontal / radius, MaxSpeed) * env * fade;
        }

        private void FixedUpdate()
        {
            float env = Envelope();
            if (env <= 0f) return;
            Vector3 center = origin + Vector3.up * (height * 0.5f);
            int count = Physics.OverlapSphereNonAlloc(center, height * 0.5f + radius, hits, ~0, QueryTriggerInteraction.Ignore);
            int unique = 0;
            for (int i = 0; i < count; i++)
            {
                var rb = hits[i].attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;
                bool seen = false;
                for (int j = 0; j < unique; j++) if (bodies[j] == rb) { seen = true; break; }
                if (!seen) bodies[unique++] = rb;
            }
            for (int i = 0; i < unique; i++)
            {
                var rb = bodies[i];
                // El jugador planea desde PlayerMovement (gravedad reducida): aca no se duplica el empuje.
                if (rb.TryGetComponent(out PlayerMovement _)) continue;
                Vector3 d = rb.worldCenterOfMass - origin;
                if (d.y < -0.2f || d.y > height) continue;
                float horizontal = new Vector2(d.x, d.z).magnitude;
                if (horizontal > radius) continue;
                float fade = Mathf.Clamp01((height - d.y) / (height * TopFade));
                float a = WindMath.UpdraftAcceleration(horizontal / radius, rb.linearVelocity.y, rb.mass, MaxSpeed * env * fade);
                if (a > 0f) rb.AddForce(Vector3.up * a, ForceMode.Acceleration);
            }
        }
    }
}
