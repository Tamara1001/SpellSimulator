using UnityEngine;
using SpellSimulator.Compiler;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Módulo 5: Motor Físico de Salida (VFX Output).
    /// Instancia y dirige los sistemas de partículas y físicas en base al SpellDescriptor.
    /// </summary>
    public class SpellOutputController : MonoBehaviour
    {
        [Header("VFX References")]
        [SerializeField, Tooltip("Gestor de efectos visuales o contenedor del VFX Graph de Fuego.")]
        private GameObject fireVFXPrefab;
        
        [SerializeField, Tooltip("Gestor de efectos visuales o contenedor del VFX Graph de Aire.")]
        private GameObject airVFXPrefab;

        /// <summary>
        /// Recibe el descriptor compilado y la posición para instanciar el efecto.
        /// Diseñado para evitar asignaciones dinámicas (GC Alloc) en su evaluación lógica.
        /// </summary>
        /// <param name="descriptor">Struct inmutable con las directivas del hechizo.</param>
        /// <param name="spawnPosition">Coordenada mundial 3D donde estallará la magia.</param>
        public void SpawnSpellEffect(SpellDescriptor descriptor, Vector3 spawnPosition)
        {
            if (!descriptor.isSignedAndActive)
            {
                Debug.Log("[VFX Output] El hechizo no está sellado (Anillo Abierto). Ignorando.");
                return;
            }

            switch (descriptor.primaryElement)
            {
                case ElementType.Fire:
                    // Nota: En fases futuras se recomienda Object Pooling para evitar el GC de Instantiate
                    if (fireVFXPrefab != null)
                    {
                        GameObject fireInstance = Instantiate(fireVFXPrefab, spawnPosition, Quaternion.identity);
                        fireInstance.transform.localScale = Vector3.one * descriptor.scaleMultiplier;
                    }
                    Debug.Log($"[VFX Output] Instanciando partículas de Fuego con escala: {descriptor.scaleMultiplier}");
                    break;

                case ElementType.Air:
                    if (airVFXPrefab != null)
                    {
                        GameObject airInstance = Instantiate(airVFXPrefab, spawnPosition, Quaternion.identity);
                        airInstance.transform.localScale = Vector3.one * descriptor.scaleMultiplier;
                    }
                    Debug.Log($"[VFX Output] Instanciando partículas de Aire con escala: {descriptor.scaleMultiplier}");
                    break;

                case ElementType.None:
                    Debug.LogWarning("[VFX Output] Se recibió un hechizo sellado pero sin Elemento Primario.");
                    break;
            }
        }
    }
}
