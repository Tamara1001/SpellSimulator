using System;
using UnityEngine;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Matematica pura de las perillas del laboratorio (sin estado ni MonoBehaviour, testeable en EditMode): pasos, ajuste a la
    /// grilla, limites, valor normalizado para el angulo de la palanca y lectura numerica. Step/Snap/Clamp/IsDefault/Normalized no
    /// generan allocations; solo Format crea strings.
    /// </summary>
    public static class LabParameterMath
    {
        /// <summary>Decimales a los que se redondea un valor tras un paso (evita la deriva de floats: 0.5 + 6 * 0.05 = 0.8, no 0.8000001).</summary>
        public const int RoundDecimals = 4;
        /// <summary>Tolerancia relativa para comparar valores (paso, default, tabla discreta).</summary>
        public const float Epsilon = 1e-4f;

        /// <summary>
        /// Un paso de la perilla en el sentido de <paramref name="dir"/> (solo importa el signo; 0 = solo ajustar a la grilla).
        /// Con tabla discreta va al siguiente valor estrictamente mayor (o menor) de la tabla; continua, al siguiente multiplo del
        /// paso desde el minimo (desde un valor fuera de grilla, como el default 0.78, va al vecino de grilla: 0.80 o 0.75).
        /// Siempre limitado al rango.
        /// </summary>
        public static float Step(float value, LabVariableDef def, int dir)
        {
            if (dir == 0) return Snap(value, def);

            if (def.HasSteps)
            {
                float[] steps = def.Steps;
                float tol = Tolerance(value);
                if (dir > 0)
                {
                    for (int i = 0; i < steps.Length; i++)
                        if (steps[i] > value + tol) return steps[i];
                    return steps[steps.Length - 1];
                }
                for (int i = steps.Length - 1; i >= 0; i--)
                    if (steps[i] < value - tol) return steps[i];
                return steps[0];
            }

            if (def.Step <= 0f) return Clamp(value, def);
            double offset = ((double)value - def.Min) / def.Step;
            int maxIndex = MaxIndex(def);
            int k = dir > 0 ? (int)Math.Floor(offset + 1e-3) + 1 : (int)Math.Ceiling(offset - 1e-3) - 1;
            if (k < 0) k = 0;
            if (k > maxIndex) k = maxIndex;
            return FromIndex(def, k);
        }

        /// <summary>Ajusta un valor al valor permitido mas cercano (tabla discreta o multiplo del paso), dentro del rango.</summary>
        public static float Snap(float value, LabVariableDef def)
        {
            if (def.HasSteps) return def.Steps[NearestStepIndex(value, def.Steps)];
            if (def.Step <= 0f) return Clamp(value, def);
            double offset = ((double)value - def.Min) / def.Step;
            int k = (int)Math.Round(offset, MidpointRounding.AwayFromZero);
            int maxIndex = MaxIndex(def);
            if (k < 0) k = 0;
            if (k > maxIndex) k = maxIndex;
            return FromIndex(def, k);
        }

        /// <summary>Limita un valor a [Min, Max] de la definicion.</summary>
        public static float Clamp(float value, LabVariableDef def) => Mathf.Clamp(value, def.Min, def.Max);

        /// <summary>True si el valor coincide (con tolerancia) con el default de la definicion.</summary>
        public static bool IsDefault(float value, LabVariableDef def) => Mathf.Abs(value - def.Default) <= Tolerance(def.Default);

        /// <summary>
        /// Posicion del valor en 0..1 (para el angulo de la palanca). Con tabla discreta usa el indice normalizado (escala
        /// logaritmica de la masa: cada valor de la tabla es un tramo igual), interpolando en log entre valores vecinos.
        /// </summary>
        public static float Normalized(float value, LabVariableDef def)
        {
            if (def.HasSteps)
            {
                float[] steps = def.Steps;
                int last = steps.Length - 1;
                if (last <= 0 || value <= steps[0]) return 0f;
                if (value >= steps[last]) return 1f;
                for (int i = 0; i < last; i++)
                {
                    float a = steps[i], b = steps[i + 1];
                    if (value > b) continue;
                    float t = a > 0f && b > a ? Mathf.Log(value / a) / Mathf.Log(b / a) : Mathf.InverseLerp(a, b, value);
                    return Mathf.Clamp01((i + t) / last);
                }
                return 1f;
            }
            if (def.Max <= def.Min) return 0f;
            return Mathf.Clamp01((value - def.Min) / (def.Max - def.Min));
        }

        /// <summary>
        /// Lectura numerica para la perilla y el registro: coma decimal (<see cref="LabCsv.NumberFormat"/>), formato de la
        /// definicion y unidad ("10 kg", "45°", "0,78", "1,25x"). Crea un string: no llamar por frame.
        /// </summary>
        public static string Format(float value, LabVariableDef def)
        {
            string number = value.ToString(string.IsNullOrEmpty(def.Format) ? "0.##" : def.Format, LabCsv.NumberFormat);
            if (string.IsNullOrEmpty(def.Unit)) return number;
            bool glued = def.Unit == "°" || def.Unit == "x";
            return glued ? number + def.Unit : number + " " + def.Unit;
        }

        private static float Tolerance(float reference) => Epsilon * Mathf.Max(1f, Mathf.Abs(reference));

        private static int MaxIndex(LabVariableDef def) => (int)Math.Round(((double)def.Max - def.Min) / def.Step);

        private static float FromIndex(LabVariableDef def, int k)
        {
            float v = (float)Math.Round(def.Min + (double)k * def.Step, RoundDecimals);
            return Mathf.Clamp(v, def.Min, def.Max);
        }

        private static int NearestStepIndex(float value, float[] steps)
        {
            int best = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < steps.Length; i++)
            {
                float d = Mathf.Abs(steps[i] - value);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }
            return best;
        }
    }
}
