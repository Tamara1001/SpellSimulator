using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SpellSimulator.DebugTools
{
    /// <summary>
    /// Controlador de UI para el sistema de diagnostico.
    /// Actualiza la visualizacion del tensor y los textos de compilacion en tiempo real.
    /// </summary>
    public class DiagnosticUIController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField, Tooltip("Referencia al RawImage que mostrara el tensor (96x96)")]
        private RawImage tensorViewer;

        [SerializeField, Tooltip("Referencia al texto que mostrara las predicciones")]
        private TextMeshProUGUI predictionsText;

        [SerializeField, Tooltip("Referencia al texto que mostrara los resultados de compilacion")]
        private TextMeshProUGUI spellCompilerText;

        private void OnEnable()
        {
            DiagnosticBus.OnTensorImageUpdated += HandleTensorImage;
            DiagnosticBus.OnPredictionUpdated  += HandlePrediction;
            DiagnosticBus.OnDiagnosticsUpdated += HandleDiagnostics;
        }

        private void OnDisable()
        {
            DiagnosticBus.OnTensorImageUpdated -= HandleTensorImage;
            DiagnosticBus.OnPredictionUpdated  -= HandlePrediction;
            DiagnosticBus.OnDiagnosticsUpdated -= HandleDiagnostics;
        }

        private void HandleTensorImage(Texture2D tex)
        {
            if (tensorViewer != null)
            {
                // Si habia una textura anterior (y no es un asset), destruirla para evitar leaks
                if (tensorViewer.texture != null && tensorViewer.texture is Texture2D t2d)
                {
                    Destroy(t2d);
                }
                tensorViewer.texture = tex;
            }
            else
            {
                // Si no hay UI, destruimos la textura para que no quede huerfana
                Destroy(tex);
            }
        }

        private void HandlePrediction(string predictionLog)
        {
            if (predictionsText != null)
                predictionsText.text = predictionLog;
        }

        private void HandleDiagnostics(string diagnosticsLog)
        {
            if (spellCompilerText != null)
                spellCompilerText.text = diagnosticsLog;
        }
    }
}
