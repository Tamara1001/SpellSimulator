using System;
using System.Collections.Generic;
using Unity.InferenceEngine;
using SpellSimulator.Core;

namespace SpellSimulator.Recognition
{
    /// <summary>
    /// Reconocedor sin MonoBehaviour: rasteriza todos los candidatos x rotaciones TTA en un batch, infiere con
    /// Unity Inference Engine, aplica softmax y promedia. Lee tambien la cabeza de pose (angulo) y, si se calibro,
    /// devuelve la rotacion de cada glifo respecto de su plantilla. Lo usan SentisGlyphClassifier y los tests.
    /// </summary>
    public sealed class GlyphClassifier : IGlyphClassifier, IDisposable
    {
        private const string LogitsOutput = "class_logits";
        private const string AngleOutput = "angle";

        private readonly bool useRotationTta;
        private Worker worker;
        private float[] inputBuffer = new float[0];

        // Calibracion de pose: direccion de equivarianza del modelo y angulo de cada clase en su pose de plantilla.
        private int poseSign;
        private float[] canonicalAngles;

        /// <summary>Entrada del ultimo batch (para la imagen de debug). El primer tensor es el primer candidato a 0 grados.</summary>
        public float[] LastInput => inputBuffer;

        public bool IsPoseCalibrated => canonicalAngles != null;

        public GlyphClassifier(ModelAsset model, BackendType backend, bool useRotationTta)
        {
            this.useRotationTta = useRotationTta;
            worker = new Worker(ModelLoader.Load(model), backend);
        }

        public GlyphPrediction[] Classify(IReadOnlyList<DrawingSession> candidates)
        {
            var predictions = Run(candidates, out float[][] poseOutputs, out float[] rotations);
            if (canonicalAngles == null || rotations.Length < 2) return predictions;

            for (int c = 0; c < predictions.Length; c++)
            {
                float canonical = canonicalAngles[predictions[c].BestIndex];
                if (float.IsNaN(canonical)) continue;
                float pose = PoseMath.Aggregate(poseOutputs[c], rotations, poseSign);
                // Con la equivarianza calibrada: salida = canonica + sign * rotacion real.
                predictions[c].RotationDeg = PoseMath.Normalize(poseSign * PoseMath.SignedDelta(canonical, pose));
            }
            return predictions;
        }

        /// <summary>
        /// Calibra la pose una vez: infiere la plantilla de cada clase en su pose canonica (con TTA), vota la direccion
        /// de equivarianza del modelo y guarda el angulo canonico por clase (ensureCanonicalAngles de la referencia).
        /// </summary>
        /// <param name="templates">Indice de clase -> trazos de plantilla (null si la clase no tiene).</param>
        public void CalibratePose(IReadOnlyList<DrawingSession> templates)
        {
            var indices = new List<int>();
            var sessions = new List<DrawingSession>();
            for (int i = 0; i < templates.Count; i++)
            {
                if (templates[i] == null || templates[i].IsEmpty) continue;
                indices.Add(i);
                sessions.Add(templates[i]);
            }
            if (sessions.Count == 0 || !useRotationTta) return;

            Run(sessions, out float[][] outputs, out float[] rotations);

            float plus = 0f, minus = 0f;
            foreach (var o in outputs)
            {
                plus += PoseMath.Concentration(PoseMath.DeRotate(o, rotations, 1));
                minus += PoseMath.Concentration(PoseMath.DeRotate(o, rotations, -1));
            }
            poseSign = plus >= minus ? 1 : -1;

            canonicalAngles = new float[templates.Count];
            for (int i = 0; i < canonicalAngles.Length; i++) canonicalAngles[i] = float.NaN;
            for (int k = 0; k < indices.Count; k++)
                canonicalAngles[indices[k]] = PoseMath.Aggregate(outputs[k], rotations, poseSign);
        }

        private GlyphPrediction[] Run(IReadOnlyList<DrawingSession> candidates, out float[][] poseOutputs, out float[] rotations)
        {
            rotations = useRotationTta ? ClassificationMath.TtaRotationsDeg : new[] { 0f };
            poseOutputs = new float[candidates.Count][];
            if (worker == null || candidates.Count == 0) return new GlyphPrediction[0];

            int batch = candidates.Count * rotations.Length;
            int needed = batch * TensorRasterizer.PixelCount;
            // Tensor<float>(shape, array) copia el array entero: el buffer tiene que medir exactamente el batch.
            if (inputBuffer.Length != needed) inputBuffer = new float[needed];

            for (int c = 0; c < candidates.Count; c++)
                for (int r = 0; r < rotations.Length; r++)
                    TensorRasterizer.Rasterize(candidates[c], rotations[r], inputBuffer,
                        (c * rotations.Length + r) * TensorRasterizer.PixelCount);

            float[] logits, angles;
            var shape = new TensorShape(batch, 1, TensorRasterizer.Size, TensorRasterizer.Size);
            using (var input = new Tensor<float>(shape, inputBuffer))
            {
                worker.Schedule(input);
                logits = (worker.PeekOutput(LogitsOutput) as Tensor<float>).DownloadToArray();
                angles = (worker.PeekOutput(AngleOutput) as Tensor<float>).DownloadToArray();
            }

            // angle: [batch, 2] = (sin, cos) por render.
            for (int c = 0; c < candidates.Count; c++)
            {
                poseOutputs[c] = new float[rotations.Length];
                for (int r = 0; r < rotations.Length; r++)
                {
                    int row = (c * rotations.Length + r) * 2;
                    poseOutputs[c][r] = PoseMath.AngleFromSinCos(angles[row], angles[row + 1]);
                }
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
