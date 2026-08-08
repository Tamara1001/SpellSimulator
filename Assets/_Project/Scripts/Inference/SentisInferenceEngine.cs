using UnityEngine;

namespace SpellSimulator.Inference
{
    /// <summary>
    /// Módulo 3: Motor de Inferencia (MOCK).
    /// Versión temporal para eludir errores de compilación de Unity Sentis y habilitar el desarrollo del Módulo 4.
    /// Simula la recepción de la textura y devuelve un índice de clase hardcodeado.
    /// </summary>
    public class SentisInferenceEngine : MonoBehaviour
    {
        [Header("Compiler Integration")]
        [SerializeField, Tooltip("Referencia al intérprete lógico para compilar el hechizo.")]
        private Compiler.SpellCompiler spellCompiler;

        [Header("Output Integration")]
        [SerializeField, Tooltip("Motor de salida para instanciar efectos visuales.")]
        private Output.SpellOutputController outputController;

        /// <summary>
        /// Procesa el lienzo dibujado. (MOCK)
        /// </summary>
        /// <param name="canvasTexture">La RenderTexture donde se dibujó el trazo.</param>
        public void ProcessCanvas(RenderTexture canvasTexture)
        {
            if (canvasTexture == null)
            {
                Debug.LogWarning("SentisInferenceEngine (Mock): RenderTexture nula recibida.");
                return;
            }

            // Simular preprocesamiento comprobando la resolución recibida
            Debug.Log($"[Inferencia Sentis Mock] Textura recibida para procesar. Resolución: {canvasTexture.width}x{canvasTexture.height}");

            // Simular inferencia hardcodeando un índice (ej. 0 = Fuego)
            int mockClassIndex = 0; 
            float mockConfidence = 0.99f;

            Debug.Log($"[Inferencia Sentis Mock] Trazo clasificado. Clase ganadora (Index): {mockClassIndex} (Confianza: {mockConfidence:F2})");
            
            if (spellCompiler != null)
            {
                Compiler.SpellDescriptor descriptor = spellCompiler.Compile(mockClassIndex, true);
                
                if (outputController != null)
                {
                    outputController.SpawnSpellEffect(descriptor, transform.position);
                }
                else
                {
                    Debug.LogWarning("SentisInferenceEngine (Mock): SpellOutputController no asignado en el Inspector.");
                }
            }
            else
            {
                Debug.LogWarning("SentisInferenceEngine (Mock): SpellCompiler no asignado en el Inspector.");
            }
        }
    }
}
