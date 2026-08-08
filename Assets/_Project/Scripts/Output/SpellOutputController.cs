using UnityEngine;
using SpellSimulator.Compiler;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Modulo 5: Motor de Salida (VFX Output).
    /// Recibe un SpellIR compilado y configura los efectos visuales en consecuencia.
    /// Todos los parametros del SpellIR se mapean a propiedades exposed del VFX Graph.
    /// </summary>
    public class SpellOutputController : MonoBehaviour
    {
        [Header("VFX Prefabs por Elemento")]
        [SerializeField] private GameObject fireVFXPrefab;
        [SerializeField] private GameObject waterVFXPrefab;
        [SerializeField] private GameObject windVFXPrefab;
        [SerializeField] private GameObject earthVFXPrefab;
        [SerializeField] private GameObject lightVFXPrefab;

        /// <summary>
        /// Instancia y configura el efecto visual para el hechizo compilado.
        /// </summary>
        public void SpawnSpellEffect(SpellIR spell, Vector3 spawnPosition)
        {
            if (!spell.valid)
            {
                Debug.Log("[VFX Output] SpellIR invalido. No se instancia efecto.");
                return;
            }

            if (spell.prepared && !spell.active)
            {
                Debug.Log("[VFX Output] Hechizo preparado (anillo abierto). Esperando sello.");
                return;
            }

            GameObject prefab = SelectPrefab(spell.element);
            if (prefab == null)
            {
                Debug.LogWarning($"[VFX Output] Sin prefab para elemento {spell.element}.");
                return;
            }

            // Orientar el efecto segun la direccion compilada
            // direction.z apunta fuera del papel (hacia la camara por defecto)
            Quaternion rotation = spell.direction != Vector3.zero
                ? Quaternion.LookRotation(spell.direction)
                : Quaternion.identity;

            GameObject instance = Instantiate(prefab, spawnPosition, rotation);

            // Escala global derivada del tamano del sigilo dibujado
            instance.transform.localScale = Vector3.one * spell.effectScale;

            // Pasar todos los parametros al VFX Graph si esta presente
            var vfx = instance.GetComponent<UnityEngine.VFX.VisualEffect>();
            if (vfx != null)
            {
                SetVFXFloat(vfx,   "Force",     spell.force);
                SetVFXFloat(vfx,   "Spread",    spell.spread);
                SetVFXFloat(vfx,   "Focus",     spell.focus);
                SetVFXFloat(vfx,   "Range",     spell.range);
                SetVFXFloat(vfx,   "Gravity",   spell.gravity);
                SetVFXFloat(vfx,   "Duration",  spell.duration);
                SetVFXFloat(vfx,   "Stability", spell.stability);
                SetVFXFloat(vfx,   "Quality",   spell.quality);
                SetVFXFloat(vfx,   "Strength",  spell.strength);
                SetVFXVector3(vfx, "Direction", spell.direction);
            }

            // Auto-destruccion al terminar la duracion + margen de fade
            Destroy(instance, spell.duration + 0.5f);

            Debug.Log(
                $"[VFX Output] {spell.element} | " +
                $"Escala: {spell.effectScale:F2} | Dur: {spell.duration:F1}s | " +
                $"Force: {spell.force:F2} | Focus: {spell.focus:F2} | " +
                $"Gravity: {spell.gravity:F2} | {spell.primaryManifestation}"
            );
        }

        private GameObject SelectPrefab(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire:            return fireVFXPrefab;
                case ElementType.Water:           return waterVFXPrefab;
                case ElementType.Wind:
                case ElementType.WindDirectsAir:
                case ElementType.WindUnderfoot:   return windVFXPrefab;
                case ElementType.Earth:           return earthVFXPrefab;
                case ElementType.Light:           return lightVFXPrefab;
                default:                          return null;
            }
        }

        private void SetVFXFloat(UnityEngine.VFX.VisualEffect vfx, string name, float value)
        {
            if (vfx.HasFloat(name)) vfx.SetFloat(name, value);
        }

        private void SetVFXVector3(UnityEngine.VFX.VisualEffect vfx, string name, Vector3 value)
        {
            if (vfx.HasVector3(name)) vfx.SetVector3(name, value);
        }
    }
}
