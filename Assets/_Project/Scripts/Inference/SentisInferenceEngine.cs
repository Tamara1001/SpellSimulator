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

        public void ProcessVectorSession(DrawingSession session)
        {
            if (worker == null || session.IsEmpty) return;

            // 1. Detectar Anillo y Agrupar Trazos
            RingInfo ring = RingDetector.Detect(session);
            GroupedSession grouped = StrokeGrouper.Group(session, ring);

            SpellAST ast = new SpellAST();
            ast.HasRing = ring.Found;

            if (ring.Found)
            {
                ast.Ring = new RingNode();
                
                // Procesar Sigilo interior
                if (!grouped.Sigil.IsEmpty)
                {
                    var res = RunInference(grouped.Sigil, "Sigilo");
                    ast.Ring.Sigil = new SigilNode { ClassIndex = res.ClassIndex, Confidence = res.Confidence };
                }

                // Procesar cada Signo exterior
                foreach (var signSession in grouped.Signs)
                {
                    if (!signSession.IsEmpty)
                    {
                        // 1. Encontrar el centro del signo
                        Vector2 signCenter = GeometryUtils.GetSessionCenter(signSession);
                        
                        // 2. Calcular angulo respecto al centro del anillo
                        float angleToSign = GeometryUtils.GetAngleDegrees(ring.Center, signCenter);
                        
                        // 3. Rotar el signo para que apunte al Norte (90 grados en Unity UV Y-Up)
                        float rotationNeeded = 90f - angleToSign;
                        var rotatedSession = GeometryUtils.RotateSession(signSession, signCenter, rotationNeeded);

                        // 4. Inferir usando la version rotada (el modelo la vera perfectamente "derecha")
                        var res = RunInference(rotatedSession, "Signo");
                        ast.Ring.Signs.Add(new SignNode { ClassIndex = res.ClassIndex, Confidence = res.Confidence });
                    }
                }
            }
            else
            {
                // Si no hay anillo, lo tomamos como un sigilo directo
                ast.Ring = new RingNode();
                var res = RunInference(grouped.Sigil, "Sigilo Unico");
                ast.Ring.Sigil = new SigilNode { ClassIndex = res.ClassIndex, Confidence = res.Confidence };
            }

            // 2. Compilar el AST y emitir el efecto
            if (spellCompiler != null)
            {
                Compiler.SpellIR spell = spellCompiler.CompileAST(ast, true);
                if (spell.valid)
                {
                    outputController?.SpawnSpellEffect(spell, this.transform.position);
                }
            }
        }

        private (int ClassIndex, float Confidence) RunInference(DrawingSession session, string debugPrefix)
        {
            float[] tensorData = TensorRasterizer.Rasterize(session, 96, debugStrokeRadius, 0.1f);

            // Solo debuggeamos la imagen del primer sigilo para no saturar la UI
            if (saveDebugPng && (debugPrefix == "Sigilo" || debugPrefix == "Sigilo Unico"))
            {
                Texture2D debugTex = TensorRasterizer.DebugTensorToTexture(tensorData, 96);
                SpellSimulator.DebugTools.DiagnosticBus.OnTensorImageUpdated?.Invoke(debugTex);
            }

            using (var inputTensor = new Unity.InferenceEngine.Tensor<float>(
                new Unity.InferenceEngine.TensorShape(1, 1, 96, 96), tensorData))
            {
                worker.Schedule(inputTensor);

                using (var outputTensor = worker.PeekOutput() as Unity.InferenceEngine.Tensor<float>)
                {
                    float[] probs = outputTensor.DownloadToArray();
                    int bestIdx = GetHighestProbabilityIndex(probs);
                    
                    Debug.Log($"[{debugPrefix}] Prediccion: Idx {bestIdx} con {probs[bestIdx]:F2}");
                    
                    // El Sigilo (con o sin anillo) actualiza el panel principal
                    if (debugPrefix == "Sigilo Unico" || debugPrefix == "Sigilo") 
                        LogTopPredictions(probs);

                    return (bestIdx, probs[bestIdx]);
                }
            }
        }

        // Metodos heredados de debug
        private void LogTopPredictions(float[] probs)
        {
            int[] indices = new int[probs.Length];
            for (int i = 0; i < probs.Length; i++) indices[i] = i;
            System.Array.Sort(indices, (a, b) => probs[b].CompareTo(probs[a]));

            string log = "<b>IA Top 3 Predicciones:</b>\n";
            for (int i = 0; i < Mathf.Min(3, probs.Length); i++)
                log += $"- Idx {indices[i]} ({probs[indices[i]]:F2})\n";
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
