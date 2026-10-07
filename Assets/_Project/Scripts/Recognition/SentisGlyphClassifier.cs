using System.Collections.Generic;
using UnityEngine;
using Unity.InferenceEngine;
using SpellSimulator.Core;
using SpellSimulator.DebugTools;

namespace SpellSimulator.Recognition
{
    /// <summary>
    /// Componente de escena que envuelve GlyphClassifier (Unity Inference Engine / Sentis).
    /// Modelo: GlyphStudent (CNN destilada, 96x96) de wha-spell-simulator.
    /// </summary>
    public class SentisGlyphClassifier : MonoBehaviour, IGlyphClassifier
    {
        [SerializeField] private ModelAsset modelAsset;
        [SerializeField, Tooltip("GPUCompute en PC y Quest. CPU como fallback si el dispositivo no soporta compute.")]
        private BackendType backend = BackendType.GPUCompute;
        [SerializeField, Tooltip("Promediar 8 rotaciones (como la referencia). Mas robusto, 8x el costo.")]
        private bool useRotationTta = true;
        [SerializeField, Tooltip("Publica en el DiagnosticBus la imagen que vio la red para el primer candidato.")]
        private bool publishDebugImage = true;

        private GlyphClassifier classifier;

        public bool IsReady => classifier != null;

        private void Awake()
        {
            if (modelAsset == null)
            {
                Debug.LogError("[Reconocimiento] Falta asignar el ModelAsset.", this);
                return;
            }

            classifier = new GlyphClassifier(modelAsset, backend, useRotationTta);
            // Calentar: la primera ejecucion compila kernels y causaria un tiron en el primer cast.
            classifier.Classify(new[] { new DrawingSession() });
        }

        private void OnDestroy()
        {
            classifier?.Dispose();
            classifier = null;
        }

        public GlyphPrediction[] Classify(IReadOnlyList<DrawingSession> candidates)
        {
            if (classifier == null) return new GlyphPrediction[0];
            var predictions = classifier.Classify(candidates);
            if (publishDebugImage && candidates.Count > 0 && !candidates[0].IsEmpty)
                DiagnosticBus.OnTensorImageUpdated?.Invoke(TensorRasterizer.DebugTensorToTexture(classifier.LastInput));
            return predictions;
        }
    }
}
