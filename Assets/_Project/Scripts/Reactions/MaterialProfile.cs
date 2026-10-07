using UnityEngine;

namespace SpellSimulator.Reactions
{
    /// <summary>Material de un objeto reactivo (madera, papel, piedra, tela...). Ver SYSTEMS_DESIGN 5.</summary>
    [CreateAssetMenu(menuName = "SpellSimulator/Material Profile", fileName = "Material")]
    public class MaterialProfile : ScriptableObject
    {
        public string displayName = "Madera";
        public MaterialParams parameters = MaterialParams.Wood;

        [Header("Aspecto")]
        [Tooltip("Color al carbonizarse por completo.")]
        public Color charColor = new Color(0.08f, 0.06f, 0.05f);
        [Tooltip("Cuanto oscurece el agua (0-1).")]
        [Range(0f, 1f)] public float wetDarkening = 0.35f;

        [Header("Fractura")]
        [Tooltip("Fragmentos por eje al romperse (2 = 8 pedazos).")]
        [Range(1, 4)] public int fragmentsPerAxis = 2;
    }
}
