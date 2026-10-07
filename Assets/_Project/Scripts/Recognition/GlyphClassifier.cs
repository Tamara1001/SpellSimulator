using System;
using System.Collections.Generic;
using Unity.InferenceEngine;
using SpellSimulator.Core;

namespace SpellSimulator.Recognition
{
    /// <summary>
    /// Reconocedor sin MonoBehaviour: rasteriza todos los candidatos x rotaciones TTA en un batch, infiere con
    /// Unity Inference Engine, aplica softmax y promedia. Lo usan SentisGlyphClassifier y los tests.
    /// </summary>
    public sealed class GlyphClassifier : IGlyphClassifier, IDisposable
    {
        private const string LogitsOutput = "class_logits";

        private readonly bool useRotationTta;
        private Worker worker;
        private float[] inputBuffer = new float[0];

        /// <summary>Entrada del ultimo batch (para la imagen de debug). El primer tensor es el primer candidato a 0 grados.</summary>
        public float[] LastInput => inputBuffer;

        public GlyphClassifier(ModelAsset model, BackendType backend, bool useRotationTta)
        {
            this.useRotationTta = useRotationTta;
            worker = new Worker(ModelLoader.Load(model), backend);
        }

        public GlyphPrediction[] Classify(IReadOnlyList<DrawingSession> candidates)
        {
            if (worker == null || candidates.Count == 0) return new GlyphPrediction[0];

            float[] rotations = useRotationTta ? ClassificationMath.TtaRotationsDeg : new[] { 0f };
            int batch = candidates.Count * rotations.Length;
            int needed = batch * TensorRasterizer.PixelCount;
            // Tensor<float>(shape, array) copia el array entero: el buffer tiene que medir exactamente el batch.
            if (inputBuffer.Length != needed) inputBuffer = new float[needed];

            for (int c = 0; c < candidates.Count; c++)
                for (int r = 0; r < rotations.Length; r++)
                    TensorRasterizer.Rasterize(candidates[c], rotations[r], inputBuffer,
                        (c * rotations.Length + r) * TensorRasterizer.PixelCount);

            float[] logits;
            var shape = new TensorShape(batch, 1, TensorRasterizer.Size, TensorRasterizer.Size);
            using (var input = new Tensor<float>(shape, inputBuffer))
            {
                worker.Schedule(input);
                logits = (worker.PeekOutput(LogitsOutput) as Tensor<float>).DownloadToArray();
            }

            return ClassificationMath.AggregateTta(logits, candidates.Count, rotations.Length, logits.Length / batch);
        }

        public void Dispose()
        {
            worker?.Dispose();
            worker = null;
        }
    }
}
