using UnityEngine;
using System.IO;

namespace SpellSimulator.Inference
{
    public class SentisInferenceEngine : MonoBehaviour
    {
        [Header("Model Configuration")]
        [SerializeField, Tooltip("El archivo .onnx exportado (ej. glyph-recognizer.onnx)")]
        private Unity.InferenceEngine.ModelAsset modelAsset;

        [Header("Compiler Integration")]
        [SerializeField, Tooltip("Referencia al intérprete lógico para compilar el hechizo.")]
        private Compiler.SpellCompiler spellCompiler;

        [Header("Output Integration")]
        [SerializeField, Tooltip("Motor de salida para instanciar efectos visuales.")]
        private Output.SpellOutputController outputController;

        [Header("Preprocessing")]
        [SerializeField, Tooltip("Material con el shader Hidden/Binarize para aislar la tinta oscura sobre fondo claro e invertirla.")]
        private Material binarizeMaterial;

        [Header("Debug")]
        [SerializeField, Tooltip("Si está activo, guarda una copia del canvas procesado como PNG en la raíz del proyecto.")]
        private bool saveDebugPng = true;

        private Unity.InferenceEngine.Model runtimeModel;
        private Unity.InferenceEngine.Worker worker;

        private void Awake()
        {
            if (modelAsset == null)
            {
                Debug.LogError("SentisInferenceEngine: Falta asignar el ModelAsset.");
                return;
            }

            runtimeModel = Unity.InferenceEngine.ModelLoader.Load(modelAsset);
            worker = new Unity.InferenceEngine.Worker(runtimeModel, Unity.InferenceEngine.BackendType.GPUCompute);
        }

        private void OnDestroy()
        {
            worker?.Dispose();
        }

        public void ProcessCanvas(RenderTexture canvasTexture)
        {
            if (worker == null || canvasTexture == null) return;

            // 0. Preprocesamiento (Escalar a 96x96 y aplicar shader de binarización)
            // Se usa GetTemporary para aislar este render y no generar GC basura.
            RenderTexture processedRT = RenderTexture.GetTemporary(96, 96, 0, RenderTextureFormat.ARGB32);
            processedRT.filterMode = FilterMode.Bilinear;

            if (binarizeMaterial != null)
            {
                Graphics.Blit(canvasTexture, processedRT, binarizeMaterial);
            }
            else
            {
                Debug.LogWarning("SentisInferenceEngine: Falta Material de Binarización. Fallback a blit crudo.");
                Graphics.Blit(canvasTexture, processedRT);
            }

            // Exportamos el resultado FINAL procesado al PNG para auditarlo
            if (saveDebugPng)
            {
                SaveRenderTextureToPNG(processedRT);
            }

            // 1. Convertir la textura procesada (ya en B&N 96x96) a Tensor de 1 canal
            using (Unity.InferenceEngine.Tensor<float> inputTensor = new Unity.InferenceEngine.Tensor<float>(new Unity.InferenceEngine.TensorShape(1, 1, 96, 96)))
            {
                Unity.InferenceEngine.TextureConverter.ToTensor(processedRT, inputTensor, new Unity.InferenceEngine.TextureTransform());
                // 2. Ejecutar inferencia en la GPU
                worker.Schedule(inputTensor);

                // 3. Obtener la salida y descargarla de forma segura a CPU
                using (Unity.InferenceEngine.Tensor<float> outputTensor = worker.PeekOutput() as Unity.InferenceEngine.Tensor<float>)
                {
                    float[] probabilities = outputTensor.DownloadToArray();
                    
                    LogTopPredictions(probabilities);

                    int bestClassIndex = GetHighestProbabilityIndex(probabilities);
                    float confidence = probabilities[bestClassIndex];

                    // 4. Pasar la clasificación al Compilador Lógico
                    if (spellCompiler != null)
                    {
                        Compiler.SpellDescriptor descriptor = spellCompiler.Compile(bestClassIndex, true);
                        
                        if (outputController != null)
                        {
                            outputController.SpawnSpellEffect(descriptor, transform.position);
                        }
                    }
                }
            }

            // Liberar memoria gráfica del buffer temporal
            RenderTexture.ReleaseTemporary(processedRT);
        }

        private void SaveRenderTextureToPNG(RenderTexture rt)
        {
            RenderTexture currentActiveRT = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();

            RenderTexture.active = currentActiveRT;

            byte[] bytes = tex.EncodeToPNG();
            string path = Path.Combine(Application.dataPath, "_Project/DebugCanvasCapture.png");
            File.WriteAllBytes(path, bytes);
            Destroy(tex);

            Debug.Log($"[Debug] Canvas PROCESADO capturado y guardado en: {path}");
        }

        private void LogTopPredictions(float[] probs)
        {
            int[] indices = new int[probs.Length];
            for (int i = 0; i < probs.Length; i++) indices[i] = i;
            System.Array.Sort(indices, (a, b) => probs[b].CompareTo(probs[a]));

            string topDebug = "[Debug IA] Top 3 predicciones: ";
            for (int i = 0; i < Mathf.Min(3, probs.Length); i++)
            {
                topDebug += $"Índice {indices[i]} ({probs[indices[i]]:F2}) | ";
            }
            Debug.Log(topDebug);
        }

        private int GetHighestProbabilityIndex(float[] probs)
        {
            int bestIndex = 0;
            float maxProb = float.MinValue;

            for (int i = 0; i < probs.Length; i++)
            {
                if (probs[i] > maxProb)
                {
                    maxProb = probs[i];
                    bestIndex = i;
                }
            }
            return bestIndex;
        }
    }
}
