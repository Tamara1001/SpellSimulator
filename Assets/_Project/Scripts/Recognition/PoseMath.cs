using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.Recognition
{
    /// <summary>
    /// Estadistica circular para la cabeza de pose del modelo (parser/ml/predictions.ts de la referencia).
    /// La pose sigue la rotacion de la entrada: deshaciendo la rotacion de cada render TTA se obtienen varias
    /// estimaciones del mismo angulo, que se combinan con una mediana circular.
    /// </summary>
    public static class PoseMath
    {
        public static float Normalize(float deg)
        {
            float d = deg % 360f;
            return d < 0f ? d + 360f : d;
        }

        /// <summary>Diferencia con signo en (-180, 180].</summary>
        public static float SignedDelta(float fromDeg, float toDeg)
        {
            float d = Normalize(toDeg - fromDeg);
            return d > 180f ? d - 360f : d;
        }

        /// <summary>Angulo (grados) de la salida [sin, cos].</summary>
        public static float AngleFromSinCos(float sin, float cos) => Mathf.Atan2(sin, cos) * Mathf.Rad2Deg;

        /// <summary>1 si todos los angulos coinciden, 0 si estan repartidos.</summary>
        public static float Concentration(IReadOnlyList<float> anglesDeg)
        {
            if (anglesDeg.Count == 0) return 0f;
            float x = 0f, y = 0f;
            foreach (float a in anglesDeg)
            {
                x += Mathf.Cos(a * Mathf.Deg2Rad);
                y += Mathf.Sin(a * Mathf.Deg2Rad);
            }
            return Mathf.Sqrt(x * x + y * y) / anglesDeg.Count;
        }

        /// <summary>Angulo del conjunto que minimiza la distancia circular total. Robusto a un outlier.</summary>
        public static float CircularMedian(IReadOnlyList<float> anglesDeg)
        {
            float best = anglesDeg.Count > 0 ? anglesDeg[0] : 0f;
            float bestCost = float.MaxValue;
            foreach (float candidate in anglesDeg)
            {
                float cost = 0f;
                foreach (float other in anglesDeg) cost += Mathf.Abs(SignedDelta(candidate, other));
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = candidate;
                }
            }
            return Normalize(best);
        }

        /// <summary>Deshace la rotacion aplicada a cada render. sign = direccion de equivarianza del modelo.</summary>
        public static float[] DeRotate(IReadOnlyList<float> outputsDeg, IReadOnlyList<float> rotationsDeg, int sign)
        {
            var result = new float[outputsDeg.Count];
            for (int i = 0; i < result.Length; i++) result[i] = outputsDeg[i] - sign * rotationsDeg[i];
            return result;
        }

        /// <summary>Pose combinada de un candidato. Sin signo calibrado, elige el que mas concentra las muestras.</summary>
        public static float Aggregate(IReadOnlyList<float> outputsDeg, IReadOnlyList<float> rotationsDeg, int sign)
        {
            if (sign == 0)
            {
                var plus = DeRotate(outputsDeg, rotationsDeg, 1);
                var minus = DeRotate(outputsDeg, rotationsDeg, -1);
                return CircularMedian(Concentration(plus) >= Concentration(minus) ? plus : minus);
            }
            return CircularMedian(DeRotate(outputsDeg, rotationsDeg, sign));
        }
    }
}
