using UnityEngine;
using UnityEngine.VFX;
using SpellSimulator.Compiler;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Modulo 5: salida visual. Instancia el prefab del elemento sobre el lienzo, orientado segun la
    /// direccion compilada (espacio-papel -> mundo) y pasa los parametros al VFX Graph.
    /// </summary>
    public class SpellOutputController : MonoBehaviour
    {
        [Header("VFX Prefabs por Elemento")]
        [SerializeField] private GameObject fireVFXPrefab;
        [SerializeField] private GameObject waterVFXPrefab;
        [SerializeField] private GameObject windVFXPrefab;
        [SerializeField] private GameObject earthVFXPrefab;
        [SerializeField] private GameObject lightVFXPrefab;

        [Header("Colocacion")]
        [SerializeField, Tooltip("Distancia (m) delante del lienzo donde nace el efecto.")]
        private float surfaceOffset = 0.05f;
        [SerializeField, Tooltip("Segundos extra antes de destruir el efecto, para el fade.")]
        private float fadeMargin = 0.5f;

        // Nombres de propiedades exposed del VFX Graph.
        private static readonly int ForceId = Shader.PropertyToID("Force");
        private static readonly int SpreadId = Shader.PropertyToID("Spread");
        private static readonly int FocusId = Shader.PropertyToID("Focus");
        private static readonly int RangeId = Shader.PropertyToID("Range");
        private static readonly int GravityId = Shader.PropertyToID("Gravity");
        private static readonly int DurationId = Shader.PropertyToID("Duration");
        private static readonly int StabilityId = Shader.PropertyToID("Stability");
        private static readonly int QualityId = Shader.PropertyToID("Quality");
        private static readonly int StrengthId = Shader.PropertyToID("Strength");
        private static readonly int DirectionId = Shader.PropertyToID("Direction");

        /// <param name="anchor">Transform del lienzo: right = +x del papel, up = +y.</param>
        /// <param name="surfaceNormalLocal">Normal "hacia afuera del papel" en espacio local del lienzo.</param>
        public GameObject SpawnSpellEffect(SpellIR spell, Transform anchor, Vector3 surfaceNormalLocal)
        {
            if (!spell.valid || !spell.active) return null;

            GameObject prefab = SelectPrefab(spell.element);
            if (prefab == null)
            {
                Debug.LogWarning($"[VFX Output] Sin prefab para el elemento {spell.element}.");
                return null;
            }

            Vector3 normal = anchor.TransformDirection(surfaceNormalLocal).normalized;
            Vector3 worldDirection = PaperToWorld(spell.direction, anchor, normal);
            Vector3 position = anchor.position + normal * surfaceOffset;
            Quaternion rotation = Quaternion.LookRotation(worldDirection, Mathf.Abs(Vector3.Dot(worldDirection, anchor.up)) > 0.99f ? anchor.forward : anchor.up);

            GameObject instance = Instantiate(prefab, position, rotation);
            instance.transform.localScale = prefab.transform.localScale * spell.effectScale;

            if (instance.TryGetComponent(out VisualEffect vfx))
            {
                SetFloat(vfx, ForceId, spell.force);
                SetFloat(vfx, SpreadId, spell.spread);
                SetFloat(vfx, FocusId, spell.focus);
                SetFloat(vfx, RangeId, spell.range);
                SetFloat(vfx, GravityId, spell.gravity);
                SetFloat(vfx, DurationId, spell.duration);
                SetFloat(vfx, StabilityId, spell.stability);
                SetFloat(vfx, QualityId, spell.quality);
                SetFloat(vfx, StrengthId, spell.strength);
                if (vfx.HasVector3(DirectionId)) vfx.SetVector3(DirectionId, worldDirection);
            }

            Destroy(instance, spell.duration + fadeMargin);
            Debug.Log($"[VFX Output] {spell.element} ({spell.sigilId}) | {spell.primaryManifestation} | " +
                      $"escala {spell.effectScale:F2} | {spell.duration:F1}s");
            return instance;
        }

        /// <summary>Direccion espacio-papel (x derecha, y arriba, z afuera) -> mundo.</summary>
        public static Vector3 PaperToWorld(Vector3 paper, Transform anchor, Vector3 worldNormal)
        {
            Vector3 world = anchor.right * paper.x + anchor.up * paper.y + worldNormal * paper.z;
            return world.sqrMagnitude > 1e-8f ? world.normalized : worldNormal;
        }

        private GameObject SelectPrefab(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return fireVFXPrefab;
                case ElementType.Water: return waterVFXPrefab;
                case ElementType.Wind: return windVFXPrefab;
                case ElementType.Earth: return earthVFXPrefab;
                case ElementType.Light: return lightVFXPrefab;
                default: return null;
            }
        }

        private static void SetFloat(VisualEffect vfx, int id, float value)
        {
            if (vfx.HasFloat(id)) vfx.SetFloat(id, value);
        }
    }
}
