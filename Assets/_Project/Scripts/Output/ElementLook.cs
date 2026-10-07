using UnityEngine;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Look y fisica visual de un elemento (dato, no codigo). La forma la da el plan; esto da color, peso y caracter.
    /// </summary>
    [CreateAssetMenu(menuName = "SpellSimulator/Element Look", fileName = "ElementLook")]
    public class ElementLook : ScriptableObject
    {
        [Header("Render")]
        public Material material;
        [Tooltip("Color a lo largo de la vida de la particula (nace a la izquierda).")]
        public Gradient color = new Gradient();
        [Tooltip("Tamanio base de la particula en metros (se escala con el sello).")]
        public float size = 0.12f;
        [Tooltip("Estirar segun la velocidad (viento, chorros de agua).")]
        public float stretch;

        [Header("Movimiento")]
        [Tooltip("Gravedad sobre las particulas: + cae (agua, tierra), - sube (fuego).")]
        public float gravity;
        [Tooltip("Multiplicador de velocidad respecto del plan.")]
        public float speed = 1f;
        [Tooltip("Vida de cada particula en segundos.")]
        public float lifetime = 0.9f;
        [Tooltip("Turbulencia (ruido) de las particulas.")]
        public float turbulence = 0.5f;
        [Tooltip("Multiplicador de cantidad de particulas.")]
        public float density = 1f;

        [Header("Luz")]
        public bool emitsLight;
        public Color lightColor = Color.white;
        public float lightIntensity = 2f;

        [Header("Fisica (MagicField)")]
        [Tooltip("Cuanto empuja este elemento (agua y viento empujan mas que la luz).")]
        public float push = 1f;
    }
}
