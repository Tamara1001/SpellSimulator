using UnityEngine;

namespace SpellSimulator.World
{
    /// <summary>
    /// Desgaste visible de una runa (bloque 4 del plan): grietas progresivas segun <see cref="Rune.WearFraction"/>.
    /// Todo se pre-arma en el builder (MapContraptions): tres hijos de grietas que se encienden por tramos (finas a
    /// partir de ~40 %, profundas desde ~75 %, rota al agotarse) y una variante oscurecida de la malla de la losa que se
    /// intercambia al agotarse. Sin Update ni materiales nuevos: solo SetActive y cambio de sharedMesh al cambiar el
    /// desgaste (evento <see cref="Rune.WearChanged"/>), barato y compatible con WebGL/Quest. La de oro apaga su brillo.
    /// </summary>
    [RequireComponent(typeof(Rune))]
    public class RuneVisuals : MonoBehaviour
    {
        /// <summary>Desde esta fraccion de desgaste aparecen una o dos grietas finas.</summary>
        private const float FineCracksFrom = 0.4f;
        /// <summary>Desde esta fraccion de desgaste las grietas son profundas y bajan por los costados.</summary>
        private const float DeepCracksFrom = 0.75f;
        /// <summary>Brillo restante de la de oro justo antes de agotarse (se apaga del todo al romperse).</summary>
        private const float MinGlowFactor = 0.45f;

        [SerializeField, Tooltip("Malla de la losa (se intercambia por la gastada al agotarse).")]
        private MeshFilter slab;
        [SerializeField] private Mesh freshMesh;
        [SerializeField, Tooltip("Variante oscurecida y apagada de la losa.")] private Mesh wornMesh;
        [SerializeField, Tooltip("Grietas por tramo: 0 finas, 1 profundas, 2 rota. Se encienden acumuladas.")]
        private GameObject[] crackStages = new GameObject[0];
        [SerializeField, Tooltip("Adornos emisivos (solo oro).")] private GameObject glow;
        [SerializeField, Tooltip("Luz puntual tenue (solo oro).")] private Light glowLight;
        [SerializeField, Min(0f)] private float glowLightIntensity = 0.6f;

        private Rune rune;
        private int shownStage = -1;

        /// <summary>Lo llama el builder al generar el mapa.</summary>
        public void Configure(MeshFilter slabFilter, Mesh fresh, Mesh worn, GameObject[] cracks, GameObject glowObject, Light light)
        {
            slab = slabFilter;
            freshMesh = fresh;
            wornMesh = worn;
            crackStages = cracks ?? new GameObject[0];
            glow = glowObject;
            glowLight = light;
            if (light != null) glowLightIntensity = light.intensity;
            shownStage = -1;
            rune = GetComponent<Rune>();
            Refresh();
        }

        private void Awake()
        {
            rune = GetComponent<Rune>();
        }

        private void OnEnable()
        {
            Rune.WearChanged += OnWearChanged;
            shownStage = -1;
            Refresh();
        }

        private void OnDisable()
        {
            Rune.WearChanged -= OnWearChanged;
        }

        private void OnWearChanged(Rune changed)
        {
            if (changed == rune) Refresh();
        }

        private void Refresh()
        {
            if (rune == null) return;
            bool exhausted = rune.IsExhausted;
            float wear = rune.WearFraction;
            int stage = exhausted ? 3 : wear >= DeepCracksFrom ? 2 : wear >= FineCracksFrom ? 1 : 0;

            if (stage != shownStage)
            {
                for (int i = 0; i < crackStages.Length; i++)
                    if (crackStages[i] != null) crackStages[i].SetActive(i < stage);
                if (slab != null)
                {
                    Mesh target = exhausted && wornMesh != null ? wornMesh : freshMesh;
                    if (target != null && slab.sharedMesh != target) slab.sharedMesh = target;
                }
                shownStage = stage;
            }

            // Brillo propio (oro): baja con el desgaste y se apaga al romperse.
            bool glowing = !exhausted && rune.GlowIntensity > 0f;
            if (glow != null && glow.activeSelf != glowing) glow.SetActive(glowing);
            if (glowLight != null)
            {
                glowLight.enabled = glowing;
                glowLight.intensity = glowLightIntensity * rune.GlowIntensity * Mathf.Lerp(1f, MinGlowFactor, wear);
            }
        }
    }
}
