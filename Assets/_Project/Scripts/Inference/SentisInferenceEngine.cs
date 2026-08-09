using UnityEngine;
using System.IO;

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
        [Header("Preprocessing")]
        [SerializeField] private Material binarizeMaterial;
        [Header("Debug")]
        [SerializeField] private bool saveDebugPng = true;

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

        public void ProcessCanvas(RenderTexture canvasTexture)
        {
            if (worker == null || canvasTexture == null) return;

            // PASO 1: Binarizar a baja resolucion para encontrar el bounding box rapidamente en CPU
            int scanSize = 128;
            RenderTexture binarizedLowRes = RenderTexture.GetTemporary(scanSize, scanSize, 0, RenderTextureFormat.ARGB32);
            if (binarizeMaterial != null) Graphics.Blit(canvasTexture, binarizedLowRes, binarizeMaterial);
            else Graphics.Blit(canvasTexture, binarizedLowRes);

            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = binarizedLowRes;
            Texture2D scanTex = new Texture2D(scanSize, scanSize, TextureFormat.RGB24, false);
            scanTex.ReadPixels(new Rect(0, 0, scanSize, scanSize), 0, 0);
            scanTex.Apply();
            RenderTexture.active = prevActive;

            int minX = scanSize, maxX = 0, minY = scanSize, maxY = 0;
            bool foundInk = false;
            Color[] pixels = scanTex.GetPixels();

            for (int y = 0; y < scanSize; y++)
            {
                for (int x = 0; x < scanSize; x++)
                {
                    if (pixels[y * scanSize + x].r > 0.5f)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                        foundInk = true;
                    }
                }
            }
            Destroy(scanTex);
            RenderTexture.ReleaseTemporary(binarizedLowRes);

            if (!foundInk)
            {
                Debug.LogWarning("[Inferencia] No se detecto tinta.");
                return;
            }

            // PASO 2: Calcular bounding box CUADRADO para preservar relacion de aspecto
            int inkW = maxX - minX;
            int inkH = maxY - minY;
            int maxDim = Mathf.Max(inkW, inkH);
            
            // Agregar 10% de margen al dimension maxima (el modelo fue entrenado con un margen)
            float margin = maxDim * 0.1f;
            float paddedDim = maxDim + (margin * 2);
            
            // Centro del bounding box original
            float cx = minX + inkW / 2f;
            float cy = minY + inkH / 2f;

            // Coordenadas del cuadrado en espacio UV [0,1]
            float uvMinX = (cx - paddedDim / 2f) / scanSize;
            float uvMinY = (cy - paddedDim / 2f) / scanSize;
            float uvDim  = paddedDim / scanSize;

            // PASO 3: Recortar desde la textura original usando el cuadrado UV
            RenderTexture croppedRT = RenderTexture.GetTemporary(96, 96, 0, RenderTextureFormat.ARGB32);
            croppedRT.filterMode = FilterMode.Bilinear;

            Vector2 scale = new Vector2(uvDim, uvDim);
            Vector2 offset = new Vector2(uvMinX, uvMinY);
            
            if (binarizeMaterial != null)
            {
                binarizeMaterial.mainTextureScale = scale;
                binarizeMaterial.mainTextureOffset = offset;
                Graphics.Blit(canvasTexture, croppedRT, binarizeMaterial);
                
                // Resetear scale/offset para futuros usos
                binarizeMaterial.mainTextureScale = Vector2.one;
                binarizeMaterial.mainTextureOffset = Vector2.zero;
            }
            else
            {
                Graphics.Blit(canvasTexture, croppedRT, scale, offset);
            }

            if (saveDebugPng) SaveRenderTextureToPNG(croppedRT);

            // PASO 4: Leer pixels finales y crear tensor manual
            // El modelo espera el glifo en un tensor [-1, 1], donde -1 es fondo negro y 1 es tinta blanca.
            // Ademas, el eje Y debe estar invertido (Y=0 en la parte superior del glifo).
            prevActive = RenderTexture.active;
            RenderTexture.active = croppedRT;
            Texture2D finalTex = new Texture2D(96, 96, TextureFormat.RGB24, false);
            finalTex.ReadPixels(new Rect(0, 0, 96, 96), 0, 0);
            finalTex.Apply();
            RenderTexture.active = prevActive;

            Color[] finalPixels = finalTex.GetPixels();
            float[] tensorData = new float[96 * 96];

            for (int y = 0; y < 96; y++)
            {
                for (int x = 0; x < 96; x++)
                {
                    // Invertir eje Y: el tensor espera Y=0 arriba, Unity tiene Y=0 abajo.
                    int unityY = 95 - y;
                    float val = finalPixels[unityY * 96 + x].r;
                    // Normalizar de [0, 1] a [-1, 1]
                    tensorData[y * 96 + x] = (val - 0.5f) * 2.0f;
                }
            }
            
            // Pasar la textura al sistema de debug, o destruirla si nadie escucha
            if (SpellSimulator.DebugTools.DiagnosticBus.OnTensorImageUpdated != null)
                SpellSimulator.DebugTools.DiagnosticBus.OnTensorImageUpdated(finalTex);
            else
                Destroy(finalTex);

            RenderTexture.ReleaseTemporary(croppedRT);

            using (var inputTensor = new Unity.InferenceEngine.Tensor<float>(
                new Unity.InferenceEngine.TensorShape(1, 1, 96, 96), tensorData))
            {
                if (saveDebugPng) DiagnoseTensor(inputTensor);

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

        private void SaveRenderTextureToPNG(RenderTexture rt)
        {
            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;

            byte[] bytes = tex.EncodeToPNG();
            string path = Path.Combine(Application.dataPath, "_Project/DebugCanvasCapture.png");
            File.WriteAllBytes(path, bytes);
            Destroy(tex);
        }

        private void DiagnoseTensor(Unity.InferenceEngine.Tensor<float> tensor)
        {
            float[] data = tensor.DownloadToArray();
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
