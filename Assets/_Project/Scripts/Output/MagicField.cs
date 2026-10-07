using UnityEngine;
using SpellSimulator.Compiler;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Efecto fisico de un hechizo sobre los Rigidbodies cercanos (incluido el jugador).
    /// Modelo de arrastre: cada primitiva define una velocidad de flujo y los cuerpos se aceleran HACIA ella (nunca mas
    /// rapido). Canon (ground-truth.md 2): la forma de los signos fija la velocidad (intensiva) y el area del sello fija
    /// el caudal (extensiva), asi que el area escala el acople, no la velocidad.
    /// Rafaga = cambio de velocidad radial al golpe; chorro = flujo axial; abanico = flujo radial sobre el plano;
    /// vortice = giro + ascenso; orbe = resorte que sostiene; succion = flujo hacia el sello.
    /// FixedUpdate con buffers preasignados (sin GC). Primera version del MagicWorld de SYSTEMS_DESIGN.
    /// </summary>
    [RequireComponent(typeof(SpellManifestation))]
    public class MagicField : MonoBehaviour
    {
        private const int MaxHits = 64;
        private static readonly Collider[] hits = new Collider[MaxHits];
        private static readonly Rigidbody[] bodies = new Rigidbody[MaxHits];

        /// <summary>Acople base (1/s): que tan rapido un cuerpo de 1 kg alcanza la velocidad del flujo.</summary>
        public const float BaseCoupling = 2.5f;
        /// <summary>Aceleracion maxima que puede imponer el campo (m/s2).</summary>
        public const float MaxAcceleration = 45f;
        /// <summary>Cambio de velocidad maximo de la rafaga (m/s).</summary>
        public const float MaxBurstDeltaV = 7f;
        /// <summary>Area de referencia: anillo de 0.3 m de radio.</summary>
        public const float ReferenceRadius = 0.3f;

        private SpellManifestation manifestation;
        private SpellIR spell;
        private SpellPlan plan;
        private SealFrame frame;
        private float flowSpeed;
        private float coupling;
        private float push;
        private float areaFactor;
        private bool burstApplied;

        public void Init(SpellIR compiled, SealFrame sealFrame, ElementLook look)
        {
            manifestation = GetComponent<SpellManifestation>();
            spell = compiled;
            plan = compiled.plan ?? SpellPlan.Inert();
            frame = sealFrame;
            push = look.push;
            areaFactor = Mathf.Clamp((frame.Radius * frame.Radius) / (ReferenceRadius * ReferenceRadius), 0.25f, 4f);
            // Misma velocidad que las particulas: lo que se ve es lo que empuja.
            flowSpeed = (2f + 8f * compiled.force) * look.speed;
            coupling = BaseCoupling * push * areaFactor * Mathf.Lerp(0.6f, 1f, compiled.quality);
        }

        private void FixedUpdate()
        {
            if (manifestation == null || plan == null) return;
            float t = manifestation.SinceStrike;
            if (t < 0f) return;

            if (!burstApplied && plan.Has(PlanPrimitive.Burst))
            {
                burstApplied = true;
                ApplyBurst();
            }

            if (!manifestation.InBody) return;
            float envelope = manifestation.Envelope(t);
            if (plan.Has(PlanPrimitive.Jet)) ApplyJet(envelope);
            if (plan.Has(PlanPrimitive.Fan)) ApplyFan(envelope);
            if (plan.Has(PlanPrimitive.Vortex)) ApplyVortex(envelope);
            if (plan.Has(PlanPrimitive.Hold)) ApplyHold(envelope);
            if (plan.Has(PlanPrimitive.Intake)) ApplyIntake(envelope);
        }

        private int Gather(Vector3 center, float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(center, radius, hits, ~0, QueryTriggerInteraction.Ignore);
            int unique = 0;
            for (int i = 0; i < count; i++)
            {
                var rb = hits[i].attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;
                bool seen = false;
                for (int j = 0; j < unique; j++) if (bodies[j] == rb) { seen = true; break; }
                if (!seen) bodies[unique++] = rb;
            }
            return unique;
        }

        /// <summary>Los cuerpos pesados responden menos, pero no proporcionalmente.</summary>
        private static float MassFactor(Rigidbody rb) => 1f / Mathf.Sqrt(Mathf.Max(rb.mass, 0.1f));

        /// <summary>
        /// Acelera el cuerpo a lo largo del flujo hasta alcanzar su velocidad. Solo empuja en la direccion del flujo:
        /// no frena lo que ya va mas rapido ni anula la gravedad (eso lo hace el orbe).
        /// </summary>
        private void Drag(Rigidbody rb, Vector3 desiredVelocity, float strength)
        {
            float speed = desiredVelocity.magnitude;
            if (speed < 1e-4f) return;
            Vector3 dir = desiredVelocity / speed;
            float missing = speed - Vector3.Dot(rb.linearVelocity, dir);
            if (missing <= 0f) return;
            float a = Mathf.Min(missing * coupling * strength * MassFactor(rb), MaxAcceleration);
            rb.AddForce(dir * a, ForceMode.Acceleration);
        }

        private void ApplyBurst()
        {
            float radius = frame.Scale * 3f;
            int n = Gather(frame.Origin, radius);
            for (int i = 0; i < n; i++)
            {
                var rb = bodies[i];
                Vector3 offset = rb.worldCenterOfMass - frame.Origin;
                // Pared del disco: la magia no va "debajo" del sello.
                if (Vector3.Dot(offset, frame.Normal) < -0.1f) continue;
                float falloff = 1f - Mathf.Clamp01(offset.magnitude / radius);
                Vector3 dir = offset.sqrMagnitude > 1e-4f ? offset.normalized : frame.Normal;
                float dv = Mathf.Min(4f * spell.force * push * Mathf.Sqrt(areaFactor), MaxBurstDeltaV) * falloff * MassFactor(rb);
                rb.AddForce(dir * dv, ForceMode.VelocityChange);
            }
        }

        private void ApplyJet(float envelope)
        {
            Vector3 dir = frame.Direction(plan.Aim);
            float length = frame.Scale * (4f + 10f * spell.range);
            float radius = frame.Scale * 0.9f / plan.Focus;
            int n = Gather(frame.Origin + dir * length * 0.5f, length * 0.5f + radius);
            for (int i = 0; i < n; i++)
            {
                var rb = bodies[i];
                Vector3 offset = rb.worldCenterOfMass - frame.Origin;
                float along = Vector3.Dot(offset, dir);
                if (along < 0f || along > length) continue;
                float off = (offset - dir * along).magnitude;
                if (off > radius) continue;
                float falloff = (1f - 0.5f * along / length) * (1f - off / radius);
                // La lente concentra el caudal: mas acople en un tubo mas angosto.
                Drag(rb, dir * flowSpeed * falloff, envelope * plan.Focus);
            }
        }

        private void ApplyFan(float envelope)
        {
            float radius = frame.Scale * (3f + 6f * spell.range);
            int n = Gather(frame.Origin, radius);
            for (int i = 0; i < n; i++)
            {
                var rb = bodies[i];
                Vector3 offset = rb.worldCenterOfMass - frame.Origin;
                float height = Vector3.Dot(offset, frame.Normal);
                if (height < -0.1f || height > frame.Scale * 1.5f) continue;
                Vector3 radial = offset - frame.Normal * height;
                if (radial.sqrMagnitude < 1e-4f) continue;
                float falloff = 1f - Mathf.Clamp01(radial.magnitude / radius);
                Drag(rb, radial.normalized * flowSpeed * falloff, envelope * Mathf.Min(plan.Dispersion, 2f));
            }
        }

        private void ApplyVortex(float envelope)
        {
            float radius = frame.Scale * 2.5f;
            float height = frame.Scale * 6f;
            int n = Gather(frame.Origin + frame.Normal * height * 0.5f, height * 0.5f + radius);
            float spin = Mathf.Sign(plan.Circulation);
            for (int i = 0; i < n; i++)
            {
                var rb = bodies[i];
                Vector3 offset = rb.worldCenterOfMass - frame.Origin;
                float along = Vector3.Dot(offset, frame.Normal);
                if (along < -0.1f || along > height) continue;
                Vector3 radial = offset - frame.Normal * along;
                float r = radial.magnitude;
                if (r > radius || r < 1e-3f) continue;
                Vector3 tangent = Vector3.Cross(frame.Normal, radial / r) * spin;
                float falloff = 1f - r / radius;
                Vector3 desired = (tangent * 0.7f - radial / r * 0.2f + frame.Normal * 0.35f) * flowSpeed * falloff;
                Drag(rb, desired, envelope * Mathf.Min(Mathf.Abs(plan.Circulation), 3f));
            }
        }

        private void ApplyHold(float envelope)
        {
            Vector3 point = frame.Point(plan.Hold.At) + frame.Normal * frame.Scale * 0.3f;
            float radius = frame.Scale * 1.2f;
            int n = Gather(point, radius);
            for (int i = 0; i < n; i++)
            {
                var rb = bodies[i];
                Vector3 toPoint = point - rb.worldCenterOfMass;
                // Resorte amortiguado que ademas sostiene el peso (el sello carga con la masa).
                Vector3 a = toPoint * (8f * plan.Hold.Grip) - rb.linearVelocity * 3f;
                if (rb.useGravity) a -= Physics.gravity;
                rb.AddForce(Vector3.ClampMagnitude(a * envelope, MaxAcceleration), ForceMode.Acceleration);
            }
        }

        private void ApplyIntake(float envelope)
        {
            float radius = frame.Scale * 4f;
            int n = Gather(frame.Origin, radius);
            for (int i = 0; i < n; i++)
            {
                var rb = bodies[i];
                Vector3 toSeal = frame.Origin - rb.worldCenterOfMass;
                float d = toSeal.magnitude;
                if (d < frame.Scale * 0.5f) continue;
                Vector3 inward = toSeal / d;
                Vector3 swirl = Vector3.Cross(frame.Normal, -inward) * Mathf.Clamp(plan.Intake.Swirl, -1f, 1f);
                float falloff = 1f - Mathf.Clamp01(d / radius);
                Vector3 desired = (inward * Mathf.Clamp(plan.Intake.Draw, 0.3f, 1.5f) + swirl * 0.5f) * flowSpeed * 0.5f * falloff;
                Drag(rb, desired, envelope);
            }
        }
    }
}
