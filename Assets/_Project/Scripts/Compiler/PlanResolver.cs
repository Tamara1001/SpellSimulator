using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Core;

namespace SpellSimulator.Compiler
{
    /// <summary>
    /// Lectura del sello y resolucion del plan. Port de compiler/reading/ (orientacion, confianza) y compiler/plan/
    /// (columns, hold, intake, focus, spinUp, resolvePlan) de wha-spell-simulator. Logica pura.
    /// Diferencias: la orientacion sale de la pose del modelo o, si no hay, se asume hacia adentro; region, weave y
    /// orb todavia no se modelan (aportan potencia y una nota).
    /// </summary>
    public static class PlanResolver
    {
        private const float MaxRadiusNorm = 1.2f;
        private const float DeadBandDeg = 12f;
        private const float Negligible = 1e-6f;

        // reading/trust.ts
        public const float FacingTrustFloor = 0.2f;
        private const float PoseTrust = 0.92f;
        private const float CanonicalTrust = 0.3f;

        // plan/hold.ts
        private const float HoldRestHeightPerLength = 0.9f;
        private const float HoldLateralSlide = 0.35f;
        private const float HoldFloor = 0.05f;

        // plan/focus.ts
        private const float LensPerLength = 0.6f;

        // plan/spinUp.ts
        private const float SpinMinClash = 0.3f, SpinMinSwirl = 0.55f, SpinMinTwistShare = 0.35f;
        private const float SpinClashGain = 6f, SpinDrawGain = 1.5f;

        private enum Family { Column, Levitation, Pull, Convergence, Power }

        private static Family FamilyOf(string manifestation)
        {
            switch (manifestation)
            {
                case Manifestations.Column:
                case Manifestations.Dispersion: return Family.Column;
                case Manifestations.Levitation: return Family.Levitation;
                case Manifestations.Pull: return Family.Pull;
                case Manifestations.Convergence: return Family.Convergence;
                default: return Family.Power; // crush, directed (region), weave, collection, billowing, repetition
            }
        }

        // ---------------------------------------------------------------- lectura

        public static List<SignReading> ReadSigns(GlyphAST ast)
        {
            var readings = new List<SignReading>();
            foreach (var sign in ast.Signs)
            {
                string manifestation = sign.Entry.Semantic.manifestation;
                if (string.IsNullOrEmpty(manifestation)) continue;

                var c = sign.Candidate;
                float bearing = c.AngleDeg;
                float inward = bearing + 180f;
                bool fromPose = !float.IsNaN(sign.RotationDeg);
                // Plantillas de signo: abajo del anillo (270) apuntando arriba (90). Rotacion r => apunta a 90 + r.
                float facingDeg = fromPose ? 90f + sign.RotationDeg : inward;
                // Zona muerta: el ruido de pose no debe crear direcciones o giros fantasma. Se ajusta a la clase
                // canonica mas cercana (adentro, afuera, tangencial) si esta a menos de DeadBandDeg.
                float twist = PoseMathDelta(inward, facingDeg);
                float snapped = Mathf.Round(twist / 90f) * 90f;
                if (Mathf.Abs(twist - snapped) < DeadBandDeg) facingDeg = inward + snapped;

                float drawing = 0.5f + 0.5f * Mathf.Clamp01(sign.Confidence * c.Neatness);
                readings.Add(new SignReading
                {
                    Id = sign.Id,
                    Manifestation = manifestation,
                    At = GeometryUtils.VectorFromAngleDeg(bearing) * Mathf.Clamp(c.RadiusNorm, 0f, MaxRadiusNorm),
                    Length = c.LengthNorm * 2f * Mathf.PI,
                    Facing = GeometryUtils.VectorFromAngleDeg(facingDeg),
                    FacingTrust = Mathf.Clamp01((fromPose ? PoseTrust : CanonicalTrust) * drawing),
                    FacingFromPose = fromPose
                });
            }
            return readings;
        }

        private static float PoseMathDelta(float fromDeg, float toDeg)
        {
            float d = GeometryUtils.NormalizeAngleDeg(toDeg - fromDeg);
            return d > 180f ? d - 360f : d;
        }

        // ---------------------------------------------------------------- columnas

        public struct Aggregate
        {
            public float Budget;
            public Vector2 Lateral;
            public float Convergence;
            public float Circulation;
        }

        /// <summary>Ley vectorial de columnas (R-05): S, P, C y Gamma.</summary>
        public static Aggregate Fold(IEnumerable<SignReading> signs)
        {
            var a = new Aggregate();
            foreach (var s in signs)
            {
                a.Budget += s.Length;
                if (s.FacingTrust < FacingTrustFloor) continue; // R-06: potencia sin direccion

                float radius = s.At.magnitude;
                Vector2 radial = radius > 1e-9f ? s.At / radius : Vector2.zero;
                // Tangente antihoraria con Y arriba (la referencia usa Y abajo).
                Vector2 tangential = new Vector2(-radial.y, radial.x);
                float weight = Mathf.Min(radius, 1f);

                a.Lateral += s.Length * s.Facing;
                a.Convergence -= s.Length * weight * Vector2.Dot(s.Facing, radial);
                a.Circulation += s.Length * weight * Vector2.Dot(s.Facing, tangential);
            }
            return a;
        }

        // ---------------------------------------------------------------- plan

        public static SpellPlan Resolve(List<SignReading> signs, string sigilId, string element, float quality)
        {
            var columns = new List<SignReading>();
            var levitation = new List<SignReading>();
            var pulls = new List<SignReading>();
            float lensBudget = 0f, power = 0f;
            bool leak = false;
            var unmodeled = new SortedSet<string>();

            foreach (var s in signs)
            {
                switch (FamilyOf(s.Manifestation))
                {
                    case Family.Column:
                        columns.Add(s);
                        leak |= s.Manifestation == Manifestations.Dispersion;
                        break;
                    case Family.Levitation: levitation.Add(s); break;
                    case Family.Pull: pulls.Add(s); break;
                    case Family.Convergence: lensBudget += s.Length; break;
                    default:
                        power += s.Length;
                        unmodeled.Add(s.Manifestation);
                        break;
                }
            }

            var agg = Fold(columns);
            var plan = new SpellPlan
            {
                Mode = ModeOf(sigilId, element),
                Aim = new Vector3(agg.Lateral.x, agg.Lateral.y, Mathf.Max(agg.Convergence, 0f)),
                Dispersion = Mathf.Max(-agg.Convergence, 0f),
                Circulation = agg.Circulation,
                Budget = agg.Budget + power,
                Hold = ResolveHold(levitation),
                Intake = ResolveIntake(pulls),
                Focus = 1f + LensPerLength * lensBudget,
                Leak = leak,
                Quality = quality
            };

            if (SpinUp(plan)) plan.Notes.Add("spun-column");
            if (levitation.Count > 0 && plan.Hold == null)
                plan.Notes.Add(Fold(levitation).Convergence < -HoldFloor ? "levitation-inverted" : "levitation-inert");
            foreach (var s in signs)
                if (s.FacingTrust < FacingTrustFloor) { plan.Notes.Add("facing-untrusted"); break; }
            if (leak) plan.Notes.Add("dispersion-leak");
            foreach (string m in unmodeled) plan.Notes.Add("unmodeled-" + m);

            plan.Primitives = Primitives(plan);
            return plan;
        }

        private static PlanMode ModeOf(string sigilId, string element)
        {
            if (sigilId == "crystal" || sigilId == "aeroform") return PlanMode.Create;
            return element == "wind" || element == "earth" ? PlanMode.Manipulate : PlanMode.Create;
        }

        private static HoldSpec ResolveHold(List<SignReading> levitation)
        {
            if (levitation.Count == 0) return null;
            var a = Fold(levitation);
            float grip = Mathf.Max(a.Convergence, 0f);
            if (grip <= HoldFloor && Mathf.Abs(a.Circulation) <= HoldFloor) return null;
            float meanLength = a.Budget / levitation.Count;
            return new HoldSpec
            {
                At = new Vector3(-a.Lateral.x * HoldLateralSlide, -a.Lateral.y * HoldLateralSlide, meanLength * HoldRestHeightPerLength),
                Grip = grip,
                Spin = a.Circulation,
                Budget = a.Budget
            };
        }

        private static IntakeSpec ResolveIntake(List<SignReading> pulls)
        {
            if (pulls.Count == 0) return null;
            var a = Fold(pulls);
            return new IntakeSpec { Budget = a.Budget, Draw = a.Convergence, Swirl = a.Circulation, Lateral = a.Lateral };
        }

        /// <summary>R-21: una succion helicoidal que alimenta un chorro de choque se resuelve como un unico vortice.</summary>
        private static bool SpinUp(SpellPlan plan)
        {
            var intake = plan.Intake;
            if (intake == null || plan.Aim.z < SpinMinClash || intake.Draw < 0f) return false;
            float slant = Mathf.Sqrt(intake.Draw * intake.Draw + intake.Swirl * intake.Swirl);
            float twistShare = slant > Negligible ? Mathf.Abs(intake.Swirl) / slant : 0f;
            if (Mathf.Abs(intake.Swirl) < SpinMinSwirl || twistShare < SpinMinTwistShare) return false;
            if (Mathf.Abs(plan.Circulation) > Negligible && plan.Circulation * intake.Swirl < 0f) return false;

            float transfer = Mathf.Sign(intake.Swirl) *
                             (Mathf.Abs(intake.Swirl) + SpinClashGain * plan.Aim.z + SpinDrawGain * intake.Draw);
            plan.Aim = new Vector3(plan.Aim.x, plan.Aim.y, 0f);
            plan.Circulation += transfer;
            plan.Intake = null;
            return true;
        }

        /// <summary>
        /// Una primitiva se ejecuta si su magnitud es al menos esta fraccion del presupuesto de columnas. La referencia
        /// usa un umbral casi nulo porque su render escala con la magnitud; aca cada pista tiene costo fijo.
        /// </summary>
        public const float MinPrimitiveShare = 0.2f;

        private static PlanPrimitive Primitives(SpellPlan plan)
        {
            // Ley de la referencia: una semilla solo-pull no emite rafaga propia (manipula lo que hay).
            bool intakeOnly = plan.Intake != null && plan.Budget <= Negligible && plan.Hold == null;
            var p = intakeOnly ? PlanPrimitive.None : PlanPrimitive.Burst;
            float floor = Mathf.Max(plan.Budget * MinPrimitiveShare, Negligible);
            if (plan.Aim.magnitude > floor) p |= PlanPrimitive.Jet;
            if (plan.Dispersion > floor) p |= PlanPrimitive.Fan;
            if (Mathf.Abs(plan.Circulation) > floor) p |= PlanPrimitive.Vortex;
            if (plan.Hold != null) p |= PlanPrimitive.Hold;
            if (plan.Intake != null) p |= PlanPrimitive.Intake;
            if (intakeOnly) plan.Notes.Add("intake-only");
            return p;
        }
    }
}
