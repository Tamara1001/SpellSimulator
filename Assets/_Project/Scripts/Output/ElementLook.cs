using UnityEngine;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Look y fisica visual de un elemento (dato, no codigo). La forma la da el plan; esto da color, peso y caracter.
    /// Un look puede sumar capas (espuma, polvo, hojas, estelas) e impactos (spray, polvo y fragmentos al chocar):
    /// son otros ElementLook. Los campos de "Como capa / impacto" solo se leen cuando el look se usa asi.
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
        [Tooltip("Si hay malla, las particulas son mallas 3D con giro libre (esquirlas de tierra). El material debe aceptar color por vertice.")]
        public Mesh mesh;
        [Tooltip("Giro de cada particula en grados/s (hojas, esquirlas). 0 = sin giro.")]
        public float spin;
        [Tooltip("Tamanio a lo largo de la vida (multiplicador). Vacio = crece de 0.6 a 1.4.")]
        public AnimationCurve sizeOverLife = new AnimationCurve();
        [Tooltip("Tope de particulas por pista (presupuesto Quest).")]
        public int maxParticles = 1500;
        [Tooltip("Estelas curvas (Trails) de ancho size: pocas particulas, barato.")]
        public bool trails;
        [Tooltip("Vida de la estela relativa a la de la particula.")]
        public float trailLifetime = 0.4f;

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

        [Header("Capas e impactos")]
        [Tooltip("Capas que acompanian cada pista del cuerpo (espuma, polvo, hojas, estelas).")]
        public ElementLook[] layers;
        [Tooltip("Sub-emisores al chocar con el mundo (spray, polvo, fragmentos). Activa la colision de baja calidad.")]
        public ElementLook[] impacts;
        [Tooltip("Las particulas tambien se desmoronan al morir en el aire (los impactos salen con la mitad de chance).")]
        public bool crumble;

        [Header("Como capa / impacto")]
        [Tooltip("Como capa: fraccion de la tasa de la pista que acompania.")]
        public float layerRatio = 0.2f;
        [Tooltip("Como capa: multiplicador de velocidad respecto de la pista.")]
        public float layerSpeed = 1f;
        [Tooltip("Como impacto: particulas por choque.")]
        public int impactCount = 4;
        [Tooltip("Como impacto: probabilidad de emitir en cada choque (0..1).")]
        public float impactChance = 0.5f;

        [Header("Luz")]
        public bool emitsLight;
        public Color lightColor = Color.white;
        public float lightIntensity = 2f;

        [Header("Fisica (MagicField)")]
        [Tooltip("Cuanto empuja este elemento (agua y viento empujan mas que la luz).")]
        public float push = 1f;
    }
}
