using UnityEngine;
using SpellSimulator.Core;

namespace SpellSimulator.Inference
{
    public class SentisInferenceEngine : MonoBehaviour
    {
        [Header("Model Configuration")]
        [SerializeField] private Unity.InferenceEngine.ModelAsset modelAsset;
        [Header("Compiler Integration")]
        [SerializeField] private Compiler.SpellCompiler spellCompiler;
        [Header("Output Integration")]
        [SerializeField] private Output.SpellOutputController outputController;
        [Header("Debug")]
        [SerializeField] private bool saveDebugPng = true;
        [SerializeField] private float debugStrokeRadius = 3f;

        private Unity.InferenceEngine.Model runtimeModel;
        private Unity.InferenceEngine.Worker worker;

        private void Awake()
        {
            if (modelAsset != null)
            {
                runtimeModel = Unity.InferenceEngine.ModelLoader.Load(modelAsset);
                worker = new Unity.InferenceEngine.Worker(runtimeModel, Unity.InferenceEngine.BackendType.GPUCompute);
            }
        }

        private void OnDestroy()
        {
            worker?.Dispose();
        }

        /// <summary>
        /// Procesa una sesion vectorial directamente sin usar la GPU ni blitting.
        /// </summary>
        public void ProcessVectorSession(DrawingSession session)
        {
            if (worker == null) return;
            if (session.IsEmpty) return;

            // 1. Rasterizar los vectores puros a 96x96
            float[] tensorData = TensorRasterizer.Rasterize(session, 96, debugStrokeRadius, 0.1f);

            // 2. Opcional: Mostrar diagnosticos
            if (saveDebugPng)
            {
                Texture2D debugTex = TensorRasterizer.DebugTensorToTexture(tensorData, 96);
                SpellSimulator.DebugTools.DiagnosticBus.OnTensorImageUpdated?.Invoke(debugTex);
                DiagnoseTensorData(tensorData);
            }

            // 3. Crear tensor en CPU a partir del array (evita bugs de TextureConverter)
            using (var inputTensor = new Unity.InferenceEngine.Tensor<float>(
                new Unity.InferenceEngine.TensorShape(1, 1, 96, 96), tensorData))
            {
                worker.Schedule(inputTensor);

                using (var outputTensor = worker.PeekOutput() as Unity.InferenceEngine.Tensor<float>)
                {
                    float[] probs = outputTensor.DownloadToArray();
                    LogTopPredictions(probs);

                    int bestIdx = GetHighestProbabilityIndex(probs);
                    float confidence = probs[bestIdx];

                    if (spellCompiler != null)
                    {
                        Compiler.SpellIR spell = spellCompiler.Compile(bestIdx, confidence, true);
                        outputController?.SpawnSpellEffect(spell, this.transform.position);
                    }
                }
            }
        }

        // Metodos heredados de la implementacion de pixels
        public void ProcessCanvas(RenderTexture rt) {
            Debug.LogWarning("ProcessCanvas esta deprecado. Usar ProcessVectorSession.");
        }

        private void DiagnoseTensorData(float[] data)
        {
            float tMin = float.MaxValue, tMax = float.MinValue, tSum = 0f;
            int nonZero = 0;
            foreach (float v in data)
            {
                if (v < tMin) tMin = v;
                if (v > tMax) tMax = v;
                tSum += v;
                if (v > 0.0f) nonZero++;
            }
            float mean = tSum / data.Length;
            float inkPercent = (float)nonZero / data.Length * 100f;
            Debug.Log($"[Tensor Diag] min={tMin:F3} max={tMax:F3} mean={mean:F3} pixels_con_tinta={nonZero}/{data.Length} ({inkPercent:F1}%)");
        }

        private void LogTopPredictions(float[] probs)
        {
            int[] indices = new int[probs.Length];
            for (int i = 0; i < probs.Length; i++) indices[i] = i;
            System.Array.Sort(indices, (a, b) => probs[b].CompareTo(probs[a]));

            string log = "<b>IA Top 3 Predicciones:</b>\n";
            for (int i = 0; i < Mathf.Min(3, probs.Length); i++)
                log += $"- Idx {indices[i]} ({probs[indices[i]]:F2})\n";
            Debug.Log(log);
            SpellSimulator.DebugTools.DiagnosticBus.OnPredictionUpdated?.Invoke(log);
        }

        private int GetHighestProbabilityIndex(float[] probs)
        {
            int best = 0;
            for (int i = 1; i < probs.Length; i++)
                if (probs[i] > probs[best]) best = i;
            return best;
        }
    }
}
