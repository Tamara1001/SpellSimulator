using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Core;

namespace SpellSimulator.Recognition
{
    /// <summary>Probabilidades del modelo para un candidato (softmax, promediadas sobre las rotaciones TTA).</summary>
    public class GlyphPrediction
    {
        public readonly float[] Probabilities;
        public readonly int BestIndex;

        public GlyphPrediction(float[] probabilities)
        {
            Probabilities = probabilities;
            BestIndex = ClassificationMath.ArgMax(probabilities);
        }

        public float Confidence => Probabilities.Length > 0 ? Probabilities[BestIndex] : 0f;
    }

    /// <summary>Contrato del reconocedor. Permite tests con un clasificador falso.</summary>
    public interface IGlyphClassifier
    {
        /// <summary>Clasifica todos los candidatos en una sola pasada.</summary>
        GlyphPrediction[] Classify(IReadOnlyList<DrawingSession> candidates);
    }

    public static class ClassificationMath
    {
        /// <summary>
        /// Rotaciones de test-time augmentation (POSE_TTA_ROTATIONS_DEG de la referencia). La clase final es
        /// el promedio de softmax sobre estas rotaciones, lo que rescata glifos que el modelo rechaza en una sola.
        /// </summary>
        public static readonly float[] TtaRotationsDeg = { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };

        /// <summary>Softmax numericamente estable sobre logits[offset .. offset + count).</summary>
        public static void Softmax(float[] logits, int offset, int count, float[] output)
        {
            float max = float.NegativeInfinity;
            for (int i = 0; i < count; i++) max = Mathf.Max(max, logits[offset + i]);

            float sum = 0f;
            for (int i = 0; i < count; i++)
            {
                float e = Mathf.Exp(logits[offset + i] - max);
                output[i] = e;
                sum += e;
            }
            float inv = sum > 0f ? 1f / sum : 0f;
            for (int i = 0; i < count; i++) output[i] *= inv;
        }

        /// <summary>
        /// Convierte logits [candidatos * rotaciones, clases] (fila = candidato-major) en una prediccion por candidato.
        /// </summary>
        public static GlyphPrediction[] AggregateTta(float[] logits, int candidateCount, int rotationCount, int classCount)
        {
            var result = new GlyphPrediction[candidateCount];
            var row = new float[classCount];
            for (int c = 0; c < candidateCount; c++)
            {
                var mean = new float[classCount];
                for (int r = 0; r < rotationCount; r++)
                {
                    Softmax(logits, (c * rotationCount + r) * classCount, classCount, row);
                    for (int k = 0; k < classCount; k++) mean[k] += row[k] / rotationCount;
                }
                result[c] = new GlyphPrediction(mean);
            }
            return result;
        }

        public static int ArgMax(float[] values)
        {
            int best = 0;
            for (int i = 1; i < values.Length; i++)
                if (values[i] > values[best]) best = i;
            return best;
        }
    }
}
