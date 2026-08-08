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
        [SerializeField, Tooltip("Referencia al interprete logico para compilar el hechizo.")]
        private Compiler.SpellCompiler spellCompiler;

        [Header("Output Integration")]
        [SerializeField, Tooltip("Motor de salida para instanciar efectos visuales.")]
        private Output.SpellOutputController outputController;

        [Header("Preprocessing")]
        [SerializeField, Tooltip("Material con el shader Custom/Binarize para invertir colores.")]
        private Material binarizeMaterial;

        [Header("Debug")]
        [SerializeField, Tooltip("Guarda la textura final que ve el modelo como PNG.")]
        private bool saveDebugPng = true;

        private Unity.InferenceEngine.Model runtimeModel;
        private Unity.InferenceEngine.Worker worker;

        // Tamano del escaner intermedio para el pixel scan (mayor = mas preciso, mas lento)
        private const int ScanSize = 96;
        // Padding en pixeles que se agrega alrededor del glifo detectado
        private const int CropPaddingPx = 8;
        // Umbral de luminancia para considerar un pixel como tinta
        private const float InkThreshold = 0.25f;

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

        /// <summary>
        /// Pipeline de preprocesamiento en 3 pasos:
        /// 1. Binariza el canvas completo a ScanSize x ScanSize en GPU.
        /// 2. Escanea los pixeles en CPU para encontrar el bounding box real de la tinta.
        /// 3. Recorta esa region y la re-escala a 96x96 para el modelo.
        ///
        /// Esto replica renderCandidateTensor del proyecto de referencia y evita
        /// todo problema de espacio de coordenadas UV.
        /// </summary>
        public void ProcessCanvas(RenderTexture canvasTexture)
        {
            if (worker == null || canvasTexture == null) return;

            // ---------------------------------------------------------------
            // PASO 1: Binarizar el canvas completo a ScanSize x ScanSize (GPU)
            // ---------------------------------------------------------------
            RenderTexture binarizedFull = RenderTexture.GetTemporary(ScanSize, ScanSize, 0, RenderTextureFormat.ARGB32);
            binarizedFull.filterMode = FilterMode.Bilinear;

            if (binarizeMaterial != null)
                Graphics.Blit(canvasTexture, binarizedFull, binarizeMaterial);
            else
                Graphics.Blit(canvasTexture, binarizedFull);

            // ---------------------------------------------------------------
            // PASO 2: Leer pixeles a CPU y detectar el bounding box de la tinta
            // 96x96 = 9216 pixeles: < 0.5ms en CPU, completamente aceptable.
            // ---------------------------------------------------------------
            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = binarizedFull;

            Texture2D scanTex = new Texture2D(ScanSize, ScanSize, TextureFormat.RGB24, false);
            scanTex.ReadPixels(new Rect(0, 0, ScanSize, ScanSize), 0, 0);
            scanTex.Apply();

            RenderTexture.active = prevActive;

            int minX = ScanSize, maxX = 0, minY = ScanSize, maxY = 0;
            bool foundInk = false;
            Color[] pixels = scanTex.GetPixels();

            for (int y = 0; y < ScanSize; y++)
            {
                for (int x = 0; x < ScanSize; x++)
                {
                    Color c = pixels[y * ScanSize + x];
                    // Los pixeles de tinta son blancos (luminancia alta) tras la binarizacion
                    float lum = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
                    if (lum > InkThreshold)
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

            if (!foundInk)
            {
                RenderTexture.ReleaseTemporary(binarizedFull);
                Debug.LogWarning("[Inferencia] No se detecto tinta en el canvas. Ignorando.");
                return;
            }

            // Agregar padding sin salir de los limites
            minX = Mathf.Max(0, minX - CropPaddingPx);
            maxX = Mathf.Min(ScanSize - 1, maxX + CropPaddingPx);
            minY = Mathf.Max(0, minY - CropPaddingPx);
            maxY = Mathf.Min(ScanSize - 1, maxY + CropPaddingPx);

            int inkW = maxX - minX + 1;
            int inkH = maxY - minY + 1;
            Debug.Log($"[Inferencia] Tinta detectada en pixeles: X[{minX}-{maxX}] Y[{minY}-{maxY}] -> {inkW}x{inkH}px");

            // ---------------------------------------------------------------
            // PASO 3: Recortar la region de tinta y escalarla a 96x96 (GPU)
            // Convertir bounds de pixel a UV [0,1] para Graphics.Blit
            // IMPORTANTE: Graphics.Blit con scale/offset trabaja en UV donde
            // (0,0) = bottom-left de la textura. ReadPixels tiene (0,0) = bottom-left
            // tambien, por lo que los bounds son directamente compatibles.
            // ---------------------------------------------------------------
            float uvMinX = (float)minX / ScanSize;
            float uvMinY = (float)minY / ScanSize;
            float uvW    = (float)inkW  / ScanSize;
            float uvH    = (float)inkH  / ScanSize;

            RenderTexture croppedRT = RenderTexture.GetTemporary(96, 96, 0, RenderTextureFormat.ARGB32);
            croppedRT.filterMode = FilterMode.Bilinear;

            // CRITICO: El modelo fue entrenado con imagenes de canvas JavaScript
            // que tienen Y=0 en la parte SUPERIOR (convencion de pantalla).
            // Unity almacena las RenderTextures con Y=0 en la parte INFERIOR.
            // Para enviar la imagen en la orientacion correcta al modelo, invertimos el eje Y 
            // mediante scale/offset en el Blit (scale.y negativo y offset ajustado).
            Vector2 scale = new Vector2(uvW, -uvH);
            Vector2 offset = new Vector2(uvMinX, uvMinY + uvH);
            
            // Blit con scale/offset: sampledUV = destUV * scale + offset
            Graphics.Blit(binarizedFull, croppedRT, scale, offset);
            RenderTexture.ReleaseTemporary(binarizedFull);

            if (saveDebugPng)
                SaveRenderTextureToPNG(croppedRT);

            // ---------------------------------------------------------------
            // PASO 4: Convertir a Tensor y correr inferencia
            // ---------------------------------------------------------------
            var texTransform = new Unity.InferenceEngine.TextureTransform();

            using (var inputTensor = new Unity.InferenceEngine.Tensor<float>(
                new Unity.InferenceEngine.TensorShape(1, 1, 96, 96)))
            {
                Unity.InferenceEngine.TextureConverter.ToTensor(croppedRT, inputTensor, texTransform);

                // Diagnostico: verificar rango de valores del tensor
                if (saveDebugPng)
                    DiagnoseTensor(inputTensor);

                worker.Schedule(inputTensor);

                using (var outputTensor = worker.PeekOutput() as Unity.InferenceEngine.Tensor<float>)
                {
                    float[] probs = outputTensor.DownloadToArray();
                    LogTopPredictions(probs);

                    int bestIdx      = GetHighestProbabilityIndex(probs);
                    float confidence = probs[bestIdx];

                    if (spellCompiler != null)
                    {
                        Compiler.SpellIR spell = spellCompiler.Compile(bestIdx, confidence, true);
                        outputController?.SpawnSpellEffect(spell, this.transform.position);
                    }
                }
            }

            RenderTexture.ReleaseTemporary(croppedRT);
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
            string path = System.IO.Path.Combine(Application.dataPath, "_Project/DebugCanvasCapture.png");
            File.WriteAllBytes(path, bytes);
            Destroy(tex);

            Debug.Log($"[Debug] Imagen final guardada en: {path}");
        }

        /// <summary>
        /// Imprime min/max/mean del tensor para verificar que los valores esten en [0,1].
        /// Si el max es cercano a 0, el glifo no esta llegando al tensor.
        /// Si el max es > 1, la normalizacion esta mal.
        /// </summary>
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
                if (v > 0.1f) nonZero++;
            }
            float mean = tSum / data.Length;
            float inkPercent = (float)nonZero / data.Length * 100f;
            Debug.Log(
                $"[Tensor Diag] min={tMin:F3} max={tMax:F3} mean={mean:F3} " +
                $"pixels_con_tinta={nonZero}/{data.Length} ({inkPercent:F1}%) " +
                $"| Esperado: max~1.0, ink~5-30%"
            );
        }

        private void LogTopPredictions(float[] probs)
        {
            int[] indices = new int[probs.Length];
            for (int i = 0; i < probs.Length; i++) indices[i] = i;
            System.Array.Sort(indices, (a, b) => probs[b].CompareTo(probs[a]));

            string log = "[Debug IA] Top 3: ";
            for (int i = 0; i < Mathf.Min(3, probs.Length); i++)
                log += $"Idx {indices[i]} ({probs[indices[i]]:F2}) | ";
            Debug.Log(log);
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
