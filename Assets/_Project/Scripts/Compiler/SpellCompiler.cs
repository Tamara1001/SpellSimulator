using UnityEngine;

namespace SpellSimulator.Compiler
{
    /// <summary>
    /// Módulo 4: Intérprete Lógico (Compilador).
    /// Convierte las predicciones brutas del motor de inferencia en directivas
    /// estructuradas (SpellDescriptor) para el motor de físicas y partículas.
    /// </summary>
    public class SpellCompiler : MonoBehaviour
    {
        /// <summary>
        /// Compila las reglas lógicas basándose en el output de la IA y el estado geométrico.
        /// </summary>
        /// <param name="recognizedClassIndex">Índice de clase predicho por el modelo ONNX.</param>
        /// <param name="isRingClosed">True si el trazado dibujó un círculo sellado.</param>
        /// <returns>Struct empaquetado y listo para ejecución.</returns>
        public SpellDescriptor Compile(int recognizedClassIndex, bool isRingClosed)
        {
            ElementType element = ElementType.None;
            float scale = 1.0f;

            // Mapeo lógico temporal para el MVP:
            // 0 = Fuego
            // 1 = Aire
            // 2 = Modificador de Escala
            // 3 = Círculo (Cierre/Sello)
            
            switch (recognizedClassIndex)
            {
                case 0:
                    element = ElementType.Fire;
                    break;
                case 1:
                    element = ElementType.Air;
                    break;
                case 2:
                    scale = 2.0f; // Modificador simple de demostración
                    break;
                case 3:
                    // Es un círculo explícito, aunque la variable 'isRingClosed' 
                    // también evalúa la geometría algorítmicamente.
                    break;
                default:
                    Debug.LogWarning($"[Compilador] Clase desconocida recibida: {recognizedClassIndex}");
                    break;
            }

            // Empaquetar en el struct
            SpellDescriptor descriptor = new SpellDescriptor(element, scale, isRingClosed);

            // Resumen en consola para validación
            Debug.Log($"[Compilador] Hechizo Formado -> Elemento: {descriptor.primaryElement} | Modificador Escala: {descriptor.scaleMultiplier}x | Activo (Sellado): {descriptor.isSignedAndActive}");

            return descriptor;
        }
    }
}
