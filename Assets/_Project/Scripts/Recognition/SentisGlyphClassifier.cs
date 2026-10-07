using System.Collections.Generic;
using UnityEngine;
using Unity.InferenceEngine;
using SpellSimulator.Core;
using SpellSimulator.DebugTools;

namespace SpellSimulator.Recognition
{
    /// <summary>
    /// Reconocedor de glifos con Unity Inference Engine (Sentis). Corre todos los candidatos y sus
    /// rotaciones TTA en un solo batch, aplica softmax y promedia.
    /// Modelo: GlyphStudent (CNN destilada, 96x96) de wha-spell-simulator.
    /// </summary>
    public class SentisGlyphClassifier : MonoBehaviour, IGlyphClassifier
    {
        private const string LogitsOutput = "class_logits";

        [SerializeField] private ModelAsset modelAsset;
        [SerializeField, Tooltip("GPUCompute en PC y Quest. CPU como fallback si el dispositivo no soporta compute.")]
        private BackendType backend = BackendType.GPUCompute;
        [SerializeField, Tooltip("Promediar 8 rotaciones (como la referencia). Mas robusto, 8x el costo.")]
        private bool useRotationTta = true;
        [SerializeField, Tooltip("Publica en el DiagnosticBus la imagen que vio la red para el primer candidato.")]
        private bool publishDebugImage = true;

        private Worker worker;
        private float[] inputBuffer = new float[0];

        public bool IsReady => worker != null;

        private void Awake()
        {
            if (modelAsset == null)
            {
                Debug.LogError("[Reconocimiento] Falta asignar el ModelAsset.", this);
                return;
            }

            worker = new Worker(ModelLoader.Load(modelAsset), backend);
            // Calentar: la primera ejecucion compila kernels y causaria un tiron en el primer cast.
            Classify(new[] { new DrawingSession() });
        }

        private void OnDestroy()
        {
            worker?.Dispose();
            worker = null;
        }

        public GlyphPrediction[] Classify(IReadOnlyList<DrawingSession> candidates)
        {
            if (worker == null || candidates.Count == 0) return new GlyphPrediction[0];

            float[] rotations = useRotationTta ? ClassificationMath.TtaRotationsDeg : new[] { 0f };
            int batch = candidates.Count * rotations.Length;
            int needed = batch * TensorRasterizer.PixelCount;
            if (inputBuffer.Length < needed) inputBuffer = new float[needed];

            for (int c = 0; c < candidates.Count; c++)
                for (int r = 0; r < rotations.Length; r++)
                    TensorRasterizer.Rasterize(candidates[c], rotations[r], inputBuffer,
                        (c * rotations.Length + r) * TensorRasterizer.PixelCount);

            if (publishDebugImage && !candidates[0].IsEmpty)
                DiagnosticBus.OnTensorImageUpdated?.Invoke(TensorRasterizer.DebugTensorToTexture(inputBuffer));

            float[] logits;
            var shape = new TensorShape(batch, 1, TensorRasterizer.Size, TensorRasterizer.Size);
            using (var input = new Tensor<float>(shape, inputBuffer))
            {
                worker.Schedule(input);
                var output = worker.PeekOutput(LogitsOutput) as Tensor<float>;
                logits = output.DownloadToArray();
            }

            int classCount = logits.Length / batch;
            return ClassificationMath.AggregateTta(logits, candidates.Count, rotations.Length, classCount);
        }
    }
}
