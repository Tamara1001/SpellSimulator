using UnityEngine;

namespace SpellSimulator.Compiler
{
    // -------------------------------------------------------------------------
    // Sigils: definen el elemento primario del hechizo
    // -------------------------------------------------------------------------
    public enum ElementType
    {
        None,
        Fire,
        Water,
        Wind,
        Earth,
        Light,
        // Variantes de viento segun el diccionario
        WindDirectsAir,
        WindUnderfoot
    }

    // -------------------------------------------------------------------------
    // Signs: modifican como se manifiesta el elemento
    // Se mapean directamente al diccionario glyph-class-to-idx.json
    // -------------------------------------------------------------------------
    public enum SignType
    {
        None,
        Column,      // Columna/haz que nace del sello
        Levitation,  // Reduce gravedad; orbe si dos signos se oponen
        Pull,        // Atraccion radial (puede torcer = vortice)
        Dispersion,  // Repulsion radial
        Collection,  // Recoleccion hacia un punto
        Convergence, // Comprime lateralmente el efecto
        Region,      // Chorro local; define dominio de spawn
        Billowing,   // Modificador de forma expansiva
        Float,       // Sustentacion ligera (gravity parcial)
        Crush,       // Fuerza inward intensa
        Repetition,  // Replica el efecto
        Weave        // Entrelazo / trayectoria ondulada
    }

    // -------------------------------------------------------------------------
    // Manifestacion: resultado combinado de todos los signos sobre el sigilo
    // -------------------------------------------------------------------------
    public enum ManifestationType
    {
        Aura,        // Sin signos: efecto centrado en el sello
        Column,      // Haz dirigido
        Levitation,  // Suspension / orbe
        Convergence, // Efecto comprimido lateralmente
        None         // Hechizo invalido
    }

    // -------------------------------------------------------------------------
    // SpellIR: Representacion Intermedia Compilada del Hechizo
    // Equivalente directo al SpellIR del proyecto wha-spell-simulator.
    // Implementado como struct para 0 GC Alloc al pasar entre modulos.
    //
    // Contrato: SpellIR solo contiene comportamiento compilado.
    // El estado de parseo (strokes, ring geometry) va en GlyphAST (futuro).
    // -------------------------------------------------------------------------
    public struct SpellIR
    {
        // --- Estado ---
        public bool valid;
        public bool active;       // Anillo cerrado + hechizo valido -> se ejecuta
        public bool prepared;     // Anillo abierto + hechizo valido -> listo pero inactivo

        // --- Elemento ---
        public ElementType element;
        public float elementConfidence;  // Confianza del modelo (0..1)

        // --- Escala y Fuerza ---
        public float sizeRatio;          // Tamano dibujado / tamano de referencia (1 = normal)
        public float strength;           // Potencia derivada del tamano (0..1)
        public float effectScale;        // Multiplicador de escala visual para VFX

        // --- Manifestacion ---
        public ManifestationType primaryManifestation;

        // --- Parametros de Comportamiento (alimentan directamente al VFX Graph) ---
        public float force;       // Velocidad / presion de las particulas (0..1)
        public float spread;      // Dispersion lateral (0..1)
        public float focus;       // Concentracion / estrechez del haz (0..1)
        public float range;       // Alcance / distancia maxima (0..1)
        public float gravity;     // 1.0 = caida normal, 0.0 = levitacion total
        public float duration;    // Tiempo de vida activo en segundos
        public float stability;   // Resistencia al ruido/flickering (0..1)
        public float quality;     // Calidad global del trazo (0..1)

        // --- Direccion 3D en espacio-papel ---
        // z apunta fuera del papel, x/y son el plano de la superficie
        public Vector3 direction;
        public float directionCoherence; // Coherencia vectorial entre todos los signos (0..1)

        // --- Tiempo ---
        public float activatedAt;  // Time.time al momento de activacion, -1 si inactivo

        // Constructor para un hechizo invalido (evita valores undefined)
        public static SpellIR Invalid(string reason = "")
        {
            return new SpellIR
            {
                valid = false,
                active = false,
                prepared = false,
                element = ElementType.None,
                elementConfidence = 0f,
                sizeRatio = 0f,
                strength = 0f,
                effectScale = 1f,
                primaryManifestation = ManifestationType.None,
                force = 0f,
                spread = 0f,
                focus = 0f,
                range = 0f,
                gravity = 1f,
                duration = 0f,
                stability = 0f,
                quality = 0f,
                direction = Vector3.forward,
                directionCoherence = 0f,
                activatedAt = -1f
            };
        }
    }
}
