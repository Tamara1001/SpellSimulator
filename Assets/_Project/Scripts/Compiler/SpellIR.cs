using UnityEngine;

namespace SpellSimulator.Compiler
{
    /// <summary>Elemento primario que define el sigilo (SUPPORTED_ELEMENTS de la referencia).</summary>
    public enum ElementType { None, Fire, Water, Wind, Earth, Light }

    /// <summary>Ids de manifestacion (campo semantic.manifestation del diccionario).</summary>
    public static class Manifestations
    {
        public const string None = "none";
        public const string Aura = "aura";
        public const string Column = "column";
        public const string Dispersion = "dispersion";
        public const string Levitation = "levitation";
        public const string Pull = "pull";
        public const string Crush = "crush";
        public const string Directed = "directed";
        public const string Convergence = "convergence";
        public const string Collection = "collection";
        public const string Billowing = "billowing";
        public const string Repetition = "repetition";
        public const string Weave = "weave";
    }

    /// <summary>Una familia de manifestacion con su fuerza agregada. Point/Radius/Rigidity solo en convergence.</summary>
    public struct ManifestationStrength
    {
        public string Id;
        public float Strength;
        public float Influence;
        public Vector2 Point;
        public float Radius;
        public float Rigidity;
    }

    /// <summary>
    /// Representacion intermedia compilada del hechizo (SpellIR de la referencia, docs/spell-ir.md).
    /// Es lo unico que consume el modulo de salida. Valido + activo = se lanza; valido + preparado = anillo abierto.
    /// </summary>
    public struct SpellIR
    {
        // Estado
        public bool valid;
        public bool active;
        public bool prepared;
        /// <summary>Texto corto para la UI ("Hechizo activo", "Sigilo ambiguo"...).</summary>
        public string status;
        public string[] warnings;

        // Elemento
        public ElementType element;
        public string sigilId;
        public float elementConfidence;

        // Escala y fuerza
        public float primarySizeNorm;
        /// <summary>Tamanio dibujado / tamanio regular del sigilo (1 = regular).</summary>
        public float sizeRatio;
        public float strength;
        public float effectScale;

        // Manifestacion
        public string primaryManifestation;
        public ManifestationStrength[] manifestations;

        // Parametros de comportamiento (0..1 salvo duration)
        public float force;
        public float spread;
        public float focus;
        public float range;
        /// <summary>1 = caida normal, 0 = levitacion total.</summary>
        public float gravity;
        public float duration;
        public float stability;
        public float quality;
        public float neatness;

        /// <summary>Direccion en espacio-papel: x/y sobre la superficie, z hacia afuera del papel.</summary>
        public Vector3 direction;
        public float xTiltDeg;
        public float yTiltDeg;
        public float directionCoherence;

        /// <summary>Time.time al activarse, -1 si no esta activo.</summary>
        public float activatedAt;

        /// <summary>Que hace el hechizo en el espacio (chorro, abanico, vortice, orbe, succion). Ver PlanResolver.</summary>
        public SpellPlan plan;

        /// <summary>Radio del anillo en metros (lo completa SpellCaster). El caudal escala con su area (ley extensiva).</summary>
        public float sealRadius;

        public static SpellIR Invalid(string status, string[] warnings, float neatness = 0f)
        {
            return new SpellIR
            {
                valid = false,
                active = false,
                prepared = false,
                status = status,
                warnings = warnings ?? new string[0],
                element = ElementType.None,
                sigilId = null,
                effectScale = 1f,
                primaryManifestation = Manifestations.None,
                manifestations = new ManifestationStrength[0],
                gravity = 1f,
                neatness = neatness,
                direction = Vector3.forward,
                activatedAt = -1f,
                plan = SpellPlan.Inert()
            };
        }

        public static ElementType ParseElement(string element)
        {
            switch (element)
            {
                case "fire": return ElementType.Fire;
                case "water": return ElementType.Water;
                case "wind": return ElementType.Wind;
                case "earth": return ElementType.Earth;
                case "light": return ElementType.Light;
                default: return ElementType.None;
            }
        }
    }
}
