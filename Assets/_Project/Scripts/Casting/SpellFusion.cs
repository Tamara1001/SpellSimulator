using UnityEngine;
using SpellSimulator.Compiler;

namespace SpellSimulator.Casting
{
    /// <summary>
    /// Fusion de dos hechizos cuando se unen dos runas armadas (bloque 4). Logica pura: el hechizo grande manda
    /// (elemento, sigilo, tamano, direccion) y absorbe de la chica la potencia y las primitivas que no tenia.
    /// </summary>
    public static class SpellFusion
    {
        public static SpellIR Merge(SpellIR big, SpellIR small)
        {
            SpellIR merged = big;
            merged.strength = Mathf.Min(1f, Mathf.Sqrt(big.strength * big.strength + small.strength * small.strength));
            merged.inkPower = Mathf.Max(big.inkPower, small.inkPower);
            merged.force = Mathf.Max(big.force, small.force);
            merged.range = Mathf.Max(big.range, small.range);
            merged.duration = Mathf.Max(big.duration, small.duration);
            merged.waterproof = big.waterproof || small.waterproof;
            merged.manifestations = MergeManifestations(big.manifestations, small.manifestations);
            merged.plan = MergePlan(big.plan, small.plan);
            return merged;
        }

        private static ManifestationStrength[] MergeManifestations(ManifestationStrength[] big, ManifestationStrength[] small)
        {
            if (small == null || small.Length == 0) return big;
            int bigCount = big?.Length ?? 0;
            var list = new System.Collections.Generic.List<ManifestationStrength>(bigCount + small.Length);
            if (big != null) list.AddRange(big);
            foreach (var m in small)
            {
                bool present = false;
                for (int i = 0; i < bigCount; i++)
                    if (big[i].Id == m.Id) { present = true; break; }
                if (!present) list.Add(m);
            }
            return list.ToArray();
        }

        /// <summary>Plan nuevo (no muta los originales): el de la grande mas las primitivas que solo tiene la chica.</summary>
        public static SpellPlan MergePlan(SpellPlan big, SpellPlan small)
        {
            if (big == null) return small;
            var plan = new SpellPlan
            {
                Mode = big.Mode,
                Aim = big.Aim,
                Dispersion = big.Dispersion,
                Circulation = big.Circulation,
                Budget = big.Budget,
                Hold = big.Hold,
                Intake = big.Intake,
                Focus = big.Focus,
                Leak = big.Leak,
                Quality = big.Quality,
                Primitives = big.Primitives
            };
            plan.Notes.AddRange(big.Notes);
            plan.Signs.AddRange(big.Signs);
            if (small == null) return plan;

            plan.Budget = Mathf.Max(big.Budget, small.Budget);
            plan.Quality = Mathf.Max(big.Quality, small.Quality);
            if (!big.Has(PlanPrimitive.Jet) && small.Has(PlanPrimitive.Jet)) plan.Aim = small.Aim;
            if (!big.Has(PlanPrimitive.Fan) && small.Has(PlanPrimitive.Fan)) plan.Dispersion = small.Dispersion;
            if (!big.Has(PlanPrimitive.Vortex) && small.Has(PlanPrimitive.Vortex)) plan.Circulation = small.Circulation;
            if (!big.Has(PlanPrimitive.Hold) && small.Has(PlanPrimitive.Hold)) plan.Hold = small.Hold;
            if (!big.Has(PlanPrimitive.Intake) && small.Has(PlanPrimitive.Intake)) plan.Intake = small.Intake;
            plan.Primitives = big.Primitives | small.Primitives;
            foreach (var note in small.Notes) if (!plan.Notes.Contains(note)) plan.Notes.Add(note);
            plan.Signs.AddRange(small.Signs);
            return plan;
        }
    }
}
