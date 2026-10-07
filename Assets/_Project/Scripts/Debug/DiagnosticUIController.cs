using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SpellSimulator.Player;

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

        private PlayerInputReader input;
        private bool visible;

        private void Awake()
        {
            // Arranca oculto. Los textos siguen llegando y quedan guardados en sus componentes para mostrarlos al activar.
            SetVisible(false);
        }

        private void Start()
        {
            input = FindFirstObjectByType<PlayerInputReader>();
        }

        private void LateUpdate()
        {
            // LateUpdate: asi se lee IsTogglingDebug ya actualizado por PlayerInputReader, sin depender del orden de scripts.
            if (input != null && input.IsTogglingDebug)
                SetVisible(!visible);
        }

        private void SetVisible(bool value)
        {
            visible = value;
            // Se apaga el render de cada elemento y no el GameObject: el controlador sigue recibiendo eventos.
            if (tensorViewer != null) tensorViewer.enabled = value;
            if (predictionsText != null) predictionsText.enabled = value;
            if (spellCompilerText != null) spellCompilerText.enabled = value;
        }

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
