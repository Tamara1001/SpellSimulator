using UnityEngine;
using SpellSimulator.Compiler;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Variante elemental para la manifestación visual de modificadores.
    /// Distingue los 5 elementos base más la variante de Cristal (Tierra cristalina).
    /// </summary>
    public enum ElementVariant
    {
        Fire,
        Water,
        Wind,
        Earth,
        Crystal,
        Light
    }

    /// <summary>
    /// Parámetros visuales calculados para una pista de signo modificador en SpellManifestation.
    /// Define forma, tamaño, velocidad, vida, emisión, color y dinámicas orbitales.
    /// </summary>
    public struct SignVisualParams
    {
        public ParticleSystemShapeType shapeType;
        public float shapeRadiusFactor;
        public float shapeAngle;
        public float sizeFactor;
        public float speedFactor;
        public float lifetimeFactor;
        public float rateFactor;
        public int burstCount;
        public float orbitalSpeed;
        public float radialSpeed;
        public Color startColor;
        public Color endColor;
        public float lightIntensityBoost;
    }

    /// <summary>
    /// Lógica pura para cálculos y mapeos visuales de signos modificadores (Tanda 1 de MODIFICADORES_AUDITORIA.md).
    /// Clase estática sin MonoBehaviour para testeo exhaustivo en EditMode.
    /// </summary>
    public static class SpellVisualModifierMath
    {
        /// <summary>
        /// Resuelve la variante elemental distinguiendo Cristal de Tierra común.
        /// </summary>
        public static ElementVariant ResolveVariant(ElementType element, bool crystalline)
        {
            switch (element)
            {
                case ElementType.Fire: return ElementVariant.Fire;
                case ElementType.Water: return ElementVariant.Water;
                case ElementType.Wind: return ElementVariant.Wind;
                case ElementType.Earth: return crystalline ? ElementVariant.Crystal : ElementVariant.Earth;
                case ElementType.Light: return ElementVariant.Light;
                default: return ElementVariant.Fire;
            }
        }

        /// <summary>
        /// Calcula el ángulo del cono combinando spread, focus y convergencia del plan (sección 4.4 de auditoría).
        /// </summary>
        public static float CalculateConeAngle(float baseCone, float spread, float focus, float convergence)
        {
            float focusFactor = Mathf.Max(0.5f, convergence * (1f + focus * 0.4f));
            float angle = (baseCone * (1f + 1.8f * Mathf.Max(0f, spread))) / focusFactor;
            return Mathf.Clamp(angle, 2f, 50f);
        }

        /// <summary>
        /// Calcula el radio de apertura del cono según el spread y el foco.
        /// </summary>
        public static float CalculateConeRadius(float baseRadius, float spread, float convergence)
        {
            float focusFactor = Mathf.Max(0.5f, convergence);
            return (baseRadius / focusFactor) * (1f + 0.8f * Mathf.Max(0f, spread));
        }

        /// <summary>
        /// Calcula la gravedad efectiva de las partículas modulada por spell.gravity (sección 4.3 de auditoría).
        /// 1 = gravedad normal del look; 0 = levitación total sin caída.
        /// </summary>
        public static float CalculateEffectiveGravity(float baseLookGravity, float spellGravity)
        {
            return baseLookGravity * Mathf.Clamp(spellGravity, 0f, 3f);
        }

        /// <summary>
        /// Genera un gradiente bicolor para ParticleSystem.MinMaxGradient.
        /// </summary>
        public static Gradient CreateGradient(Color start, Color end)
        {
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(start.a, 0f), new GradientAlphaKey(end.a, 1f) }
            );
            return grad;
        }

        // ---------------------------------------------------------------- Parámetros por Signo × Elemento

        /// <summary>
        /// Parámetros para Aplastar (crush): fractura, ondas de choque y estallido destructivo.
        /// </summary>
        public static SignVisualParams GetCrushParams(ElementVariant variant, float strength, float baseScale)
        {
            float strNorm = Mathf.Clamp(strength, 0.5f, 3f);
            switch (variant)
            {
                case ElementVariant.Fire:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Cone,
                        shapeRadiusFactor = 0.4f,
                        shapeAngle = 70f,
                        sizeFactor = 1.4f,
                        speedFactor = 1.3f,
                        lifetimeFactor = 0.6f,
                        rateFactor = 0f,
                        burstCount = Mathf.RoundToInt(180f * strNorm),
                        orbitalSpeed = 0f,
                        radialSpeed = 0f,
                        startColor = new Color(1f, 0.22f, 0.05f, 1f),
                        endColor = new Color(0.25f, 0.05f, 0.02f, 0f),
                        lightIntensityBoost = 1.5f
                    };
                case ElementVariant.Water:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Circle,
                        shapeRadiusFactor = 0.5f,
                        shapeAngle = 0f,
                        sizeFactor = 0.8f,
                        speedFactor = 1.4f,
                        lifetimeFactor = 0.5f,
                        rateFactor = 0f,
                        burstCount = Mathf.RoundToInt(220f * strNorm),
                        orbitalSpeed = 0f,
                        radialSpeed = 0f,
                        startColor = new Color(0.9f, 0.96f, 1f, 1f),
                        endColor = new Color(0.08f, 0.35f, 0.8f, 0f),
                        lightIntensityBoost = 1.2f
                    };
                case ElementVariant.Wind:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Circle,
                        shapeRadiusFactor = 0.7f,
                        shapeAngle = 0f,
                        sizeFactor = 1.6f,
                        speedFactor = 1.8f,
                        lifetimeFactor = 0.4f,
                        rateFactor = 0f,
                        burstCount = Mathf.RoundToInt(150f * strNorm),
                        orbitalSpeed = 0f,
                        radialSpeed = 0f,
                        startColor = new Color(0.9f, 0.94f, 1f, 0.85f),
                        endColor = new Color(0.6f, 0.75f, 0.9f, 0f),
                        lightIntensityBoost = 1.1f
                    };
                case ElementVariant.Earth:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Hemisphere,
                        shapeRadiusFactor = 0.45f,
                        shapeAngle = 0f,
                        sizeFactor = 2.0f,
                        speedFactor = 1.1f,
                        lifetimeFactor = 0.8f,
                        rateFactor = 0f,
                        burstCount = Mathf.RoundToInt(130f * strNorm),
                        orbitalSpeed = 0f,
                        radialSpeed = 0f,
                        startColor = new Color(0.62f, 0.48f, 0.32f, 1f),
                        endColor = new Color(0.28f, 0.2f, 0.14f, 0f),
                        lightIntensityBoost = 1.0f
                    };
                case ElementVariant.Crystal:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Sphere,
                        shapeRadiusFactor = 0.4f,
                        shapeAngle = 0f,
                        sizeFactor = 1.1f,
                        speedFactor = 1.5f,
                        lifetimeFactor = 0.65f,
                        rateFactor = 0f,
                        burstCount = Mathf.RoundToInt(200f * strNorm),
                        orbitalSpeed = 0f,
                        radialSpeed = 0f,
                        startColor = new Color(0.6f, 1f, 1f, 1f),
                        endColor = new Color(0.85f, 0.6f, 0.95f, 0f),
                        lightIntensityBoost = 1.8f
                    };
                default: // Light
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Sphere,
                        shapeRadiusFactor = 0.35f,
                        shapeAngle = 0f,
                        sizeFactor = 1.5f,
                        speedFactor = 2.0f,
                        lifetimeFactor = 0.45f,
                        rateFactor = 0f,
                        burstCount = Mathf.RoundToInt(260f * strNorm),
                        orbitalSpeed = 0f,
                        radialSpeed = 0f,
                        startColor = new Color(1f, 1f, 0.95f, 1f),
                        endColor = new Color(1f, 0.85f, 0.4f, 0f),
                        lightIntensityBoost = 2.5f
                    };
            }
        }

        /// <summary>
        /// Parámetros para Orbe (orb): contenedor esférico o membrana que suspende la magia.
        /// </summary>
        public static SignVisualParams GetOrbParams(ElementVariant variant, float strength, float baseScale)
        {
            switch (variant)
            {
                case ElementVariant.Fire:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Sphere,
                        shapeRadiusFactor = 0.38f,
                        shapeAngle = 0f,
                        sizeFactor = 1.1f,
                        speedFactor = 0.2f,
                        lifetimeFactor = 1.0f,
                        rateFactor = 180f,
                        burstCount = 0,
                        orbitalSpeed = 3.0f,
                        radialSpeed = -0.1f,
                        startColor = new Color(1f, 0.65f, 0.1f, 0.9f),
                        endColor = new Color(0.8f, 0.12f, 0.05f, 0f),
                        lightIntensityBoost = 1.3f
                    };
                case ElementVariant.Water:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Sphere,
                        shapeRadiusFactor = 0.42f,
                        shapeAngle = 0f,
                        sizeFactor = 0.9f,
                        speedFactor = 0.15f,
                        lifetimeFactor = 1.1f,
                        rateFactor = 200f,
                        burstCount = 0,
                        orbitalSpeed = 1.2f,
                        radialSpeed = -0.05f,
                        startColor = new Color(0.3f, 0.72f, 1f, 0.75f),
                        endColor = new Color(0.08f, 0.32f, 0.85f, 0.2f),
                        lightIntensityBoost = 1.1f
                    };
                case ElementVariant.Wind:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Sphere,
                        shapeRadiusFactor = 0.45f,
                        shapeAngle = 0f,
                        sizeFactor = 1.0f,
                        speedFactor = 0.25f,
                        lifetimeFactor = 1.0f,
                        rateFactor = 160f,
                        burstCount = 0,
                        orbitalSpeed = 5.0f,
                        radialSpeed = -0.15f,
                        startColor = new Color(0.9f, 0.94f, 1f, 0.6f),
                        endColor = new Color(0.7f, 0.85f, 0.95f, 0f),
                        lightIntensityBoost = 1.0f
                    };
                case ElementVariant.Earth:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Sphere,
                        shapeRadiusFactor = 0.4f,
                        shapeAngle = 0f,
                        sizeFactor = 1.5f,
                        speedFactor = 0.18f,
                        lifetimeFactor = 1.2f,
                        rateFactor = 130f,
                        burstCount = 0,
                        orbitalSpeed = 1.4f,
                        radialSpeed = -0.08f,
                        startColor = new Color(0.82f, 0.7f, 0.52f, 0.9f),
                        endColor = new Color(0.55f, 0.38f, 0.24f, 0f),
                        lightIntensityBoost = 1.0f
                    };
                case ElementVariant.Crystal:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Sphere,
                        shapeRadiusFactor = 0.36f,
                        shapeAngle = 0f,
                        sizeFactor = 0.85f,
                        speedFactor = 0.2f,
                        lifetimeFactor = 1.1f,
                        rateFactor = 150f,
                        burstCount = 0,
                        orbitalSpeed = 2.0f,
                        radialSpeed = -0.1f,
                        startColor = new Color(0.72f, 0.96f, 1f, 0.9f),
                        endColor = new Color(0.9f, 0.65f, 1f, 0f),
                        lightIntensityBoost = 1.4f
                    };
                default: // Light
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Sphere,
                        shapeRadiusFactor = 0.35f,
                        shapeAngle = 0f,
                        sizeFactor = 1.2f,
                        speedFactor = 0.15f,
                        lifetimeFactor = 1.0f,
                        rateFactor = 240f,
                        burstCount = 0,
                        orbitalSpeed = 0.8f,
                        radialSpeed = -0.05f,
                        startColor = new Color(1f, 0.96f, 0.8f, 0.95f),
                        endColor = new Color(1f, 0.88f, 0.5f, 0.1f),
                        lightIntensityBoost = 2.0f
                    };
            }
        }

        /// <summary>
        /// Parámetros para Nube (billowing): volutas hinchadas, gran radio, baja velocidad y turbulencia.
        /// </summary>
        public static SignVisualParams GetBillowingParams(ElementVariant variant, float strength, float baseScale)
        {
            switch (variant)
            {
                case ElementVariant.Fire:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Cone,
                        shapeRadiusFactor = 0.45f,
                        shapeAngle = 40f,
                        sizeFactor = 3.5f,
                        speedFactor = 0.35f,
                        lifetimeFactor = 1.8f,
                        rateFactor = 140f,
                        burstCount = 0,
                        orbitalSpeed = 0.5f,
                        radialSpeed = 0.15f,
                        startColor = new Color(0.38f, 0.16f, 0.05f, 0.85f),
                        endColor = new Color(0.15f, 0.15f, 0.15f, 0f),
                        lightIntensityBoost = 0.9f
                    };
                case ElementVariant.Water:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Hemisphere,
                        shapeRadiusFactor = 0.5f,
                        shapeAngle = 0f,
                        sizeFactor = 3.2f,
                        speedFactor = 0.3f,
                        lifetimeFactor = 1.9f,
                        rateFactor = 160f,
                        burstCount = 0,
                        orbitalSpeed = 0.3f,
                        radialSpeed = 0.1f,
                        startColor = new Color(0.72f, 0.88f, 0.95f, 0.7f),
                        endColor = new Color(0.92f, 0.96f, 1f, 0f),
                        lightIntensityBoost = 0.8f
                    };
                case ElementVariant.Wind:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Sphere,
                        shapeRadiusFactor = 0.55f,
                        shapeAngle = 0f,
                        sizeFactor = 3.0f,
                        speedFactor = 0.4f,
                        lifetimeFactor = 1.7f,
                        rateFactor = 150f,
                        burstCount = 0,
                        orbitalSpeed = 0.8f,
                        radialSpeed = 0.2f,
                        startColor = new Color(0.94f, 0.96f, 0.98f, 0.65f),
                        endColor = new Color(0.82f, 0.9f, 0.96f, 0f),
                        lightIntensityBoost = 0.8f
                    };
                case ElementVariant.Earth:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Hemisphere,
                        shapeRadiusFactor = 0.6f,
                        shapeAngle = 0f,
                        sizeFactor = 3.4f,
                        speedFactor = 0.35f,
                        lifetimeFactor = 2.0f,
                        rateFactor = 140f,
                        burstCount = 0,
                        orbitalSpeed = 0.4f,
                        radialSpeed = 0.25f,
                        startColor = new Color(0.76f, 0.68f, 0.52f, 0.8f),
                        endColor = new Color(0.48f, 0.4f, 0.3f, 0f),
                        lightIntensityBoost = 0.7f
                    };
                case ElementVariant.Crystal:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Sphere,
                        shapeRadiusFactor = 0.45f,
                        shapeAngle = 0f,
                        sizeFactor = 2.2f,
                        speedFactor = 0.4f,
                        lifetimeFactor = 1.6f,
                        rateFactor = 140f,
                        burstCount = 0,
                        orbitalSpeed = 1.0f,
                        radialSpeed = 0.15f,
                        startColor = new Color(0.82f, 0.96f, 1f, 0.75f),
                        endColor = new Color(0.9f, 0.82f, 1f, 0f),
                        lightIntensityBoost = 1.2f
                    };
                default: // Light
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Sphere,
                        shapeRadiusFactor = 0.5f,
                        shapeAngle = 0f,
                        sizeFactor = 3.6f,
                        speedFactor = 0.35f,
                        lifetimeFactor = 1.8f,
                        rateFactor = 160f,
                        burstCount = 0,
                        orbitalSpeed = 0.6f,
                        radialSpeed = 0.2f,
                        startColor = new Color(1f, 0.94f, 0.75f, 0.85f),
                        endColor = new Color(1f, 0.98f, 0.92f, 0f),
                        lightIntensityBoost = 1.5f
                    };
            }
        }

        /// <summary>
        /// Parámetros para Enfriar (cool): vaho gélido, escarcha y tinte cromático frío.
        /// </summary>
        public static SignVisualParams GetCoolParams(ElementVariant variant, float strength, float baseScale)
        {
            switch (variant)
            {
                case ElementVariant.Fire:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Cone,
                        shapeRadiusFactor = 0.3f,
                        shapeAngle = 30f,
                        sizeFactor = 0.9f,
                        speedFactor = 0.8f,
                        lifetimeFactor = 0.9f,
                        rateFactor = 130f,
                        burstCount = 0,
                        orbitalSpeed = 0f,
                        radialSpeed = 0f,
                        startColor = new Color(0.25f, 0.65f, 1f, 0.9f),
                        endColor = new Color(0.06f, 0.12f, 0.45f, 0f),
                        lightIntensityBoost = 0.8f
                    };
                case ElementVariant.Water:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Hemisphere,
                        shapeRadiusFactor = 0.35f,
                        shapeAngle = 0f,
                        sizeFactor = 0.65f,
                        speedFactor = 0.6f,
                        lifetimeFactor = 1.1f,
                        rateFactor = 150f,
                        burstCount = 0,
                        orbitalSpeed = 0f,
                        radialSpeed = 0f,
                        startColor = new Color(0.75f, 0.92f, 1f, 0.85f),
                        endColor = new Color(0.92f, 0.98f, 1f, 0f),
                        lightIntensityBoost = 0.9f
                    };
                case ElementVariant.Wind:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Cone,
                        shapeRadiusFactor = 0.4f,
                        shapeAngle = 45f,
                        sizeFactor = 0.55f,
                        speedFactor = 1.4f,
                        lifetimeFactor = 0.7f,
                        rateFactor = 140f,
                        burstCount = 0,
                        orbitalSpeed = 1.5f,
                        radialSpeed = 0f,
                        startColor = new Color(0.92f, 0.96f, 1f, 0.9f),
                        endColor = new Color(0.75f, 0.86f, 1f, 0f),
                        lightIntensityBoost = 0.85f
                    };
                case ElementVariant.Earth:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Circle,
                        shapeRadiusFactor = 0.45f,
                        shapeAngle = 0f,
                        sizeFactor = 0.8f,
                        speedFactor = 0.5f,
                        lifetimeFactor = 1.2f,
                        rateFactor = 120f,
                        burstCount = 0,
                        orbitalSpeed = 0f,
                        radialSpeed = 0.2f,
                        startColor = new Color(0.72f, 0.82f, 0.92f, 0.8f),
                        endColor = new Color(0.42f, 0.46f, 0.54f, 0f),
                        lightIntensityBoost = 0.75f
                    };
                case ElementVariant.Crystal:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Sphere,
                        shapeRadiusFactor = 0.35f,
                        shapeAngle = 0f,
                        sizeFactor = 0.7f,
                        speedFactor = 0.7f,
                        lifetimeFactor = 1.0f,
                        rateFactor = 140f,
                        burstCount = 0,
                        orbitalSpeed = 1.2f,
                        radialSpeed = 0f,
                        startColor = new Color(0.6f, 0.88f, 1f, 0.9f),
                        endColor = new Color(0.95f, 1f, 1f, 0f),
                        lightIntensityBoost = 1.1f
                    };
                default: // Light
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Sphere,
                        shapeRadiusFactor = 0.4f,
                        shapeAngle = 0f,
                        sizeFactor = 1.3f,
                        speedFactor = 0.8f,
                        lifetimeFactor = 1.0f,
                        rateFactor = 150f,
                        burstCount = 0,
                        orbitalSpeed = 1.0f,
                        radialSpeed = 0f,
                        startColor = new Color(0.35f, 1f, 0.82f, 0.85f),
                        endColor = new Color(0.45f, 0.35f, 1f, 0f),
                        lightIntensityBoost = 1.3f
                    };
            }
        }

        /// <summary>
        /// Parámetros para Fortalecer (empower): sobretensión energética, chispas periféricas y brillo aumentado.
        /// </summary>
        public static SignVisualParams GetEmpowerParams(ElementVariant variant, float strength, float baseScale)
        {
            float strNorm = Mathf.Clamp(strength, 0.5f, 3f);
            switch (variant)
            {
                case ElementVariant.Fire:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Cone,
                        shapeRadiusFactor = 0.45f,
                        shapeAngle = 45f,
                        sizeFactor = 1.5f,
                        speedFactor = 1.5f,
                        lifetimeFactor = 0.8f,
                        rateFactor = 160f,
                        burstCount = Mathf.RoundToInt(160f * strNorm),
                        orbitalSpeed = 1.5f,
                        radialSpeed = 0.3f,
                        startColor = new Color(1f, 0.96f, 0.65f, 1f),
                        endColor = new Color(1f, 0.4f, 0f, 0f),
                        lightIntensityBoost = 2.5f
                    };
                case ElementVariant.Water:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Cone,
                        shapeRadiusFactor = 0.4f,
                        shapeAngle = 40f,
                        sizeFactor = 1.3f,
                        speedFactor = 1.6f,
                        lifetimeFactor = 0.75f,
                        rateFactor = 180f,
                        burstCount = Mathf.RoundToInt(180f * strNorm),
                        orbitalSpeed = 2.0f,
                        radialSpeed = 0.25f,
                        startColor = new Color(0.15f, 0.88f, 1f, 1f),
                        endColor = new Color(0.88f, 0.98f, 1f, 0f),
                        lightIntensityBoost = 2.0f
                    };
                case ElementVariant.Wind:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Sphere,
                        shapeRadiusFactor = 0.5f,
                        shapeAngle = 0f,
                        sizeFactor = 1.4f,
                        speedFactor = 1.8f,
                        lifetimeFactor = 0.7f,
                        rateFactor = 170f,
                        burstCount = Mathf.RoundToInt(170f * strNorm),
                        orbitalSpeed = 4.0f,
                        radialSpeed = 0.4f,
                        startColor = new Color(0.88f, 0.82f, 1f, 0.9f),
                        endColor = new Color(0.35f, 0.18f, 0.75f, 0f),
                        lightIntensityBoost = 2.2f
                    };
                case ElementVariant.Earth:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Hemisphere,
                        shapeRadiusFactor = 0.45f,
                        shapeAngle = 0f,
                        sizeFactor = 1.6f,
                        speedFactor = 1.3f,
                        lifetimeFactor = 0.85f,
                        rateFactor = 140f,
                        burstCount = Mathf.RoundToInt(150f * strNorm),
                        orbitalSpeed = 1.2f,
                        radialSpeed = 0.2f,
                        startColor = new Color(1f, 0.82f, 0.25f, 1f),
                        endColor = new Color(0.95f, 0.22f, 0.05f, 0f),
                        lightIntensityBoost = 2.0f
                    };
                case ElementVariant.Crystal:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Sphere,
                        shapeRadiusFactor = 0.4f,
                        shapeAngle = 0f,
                        sizeFactor = 1.2f,
                        speedFactor = 1.7f,
                        lifetimeFactor = 0.8f,
                        rateFactor = 170f,
                        burstCount = Mathf.RoundToInt(190f * strNorm),
                        orbitalSpeed = 2.5f,
                        radialSpeed = 0.3f,
                        startColor = new Color(1f, 0.45f, 0.92f, 1f),
                        endColor = new Color(0.75f, 1f, 1f, 0f),
                        lightIntensityBoost = 2.8f
                    };
                default: // Light
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Sphere,
                        shapeRadiusFactor = 0.4f,
                        shapeAngle = 0f,
                        sizeFactor = 1.8f,
                        speedFactor = 2.2f,
                        lifetimeFactor = 0.65f,
                        rateFactor = 220f,
                        burstCount = Mathf.RoundToInt(240f * strNorm),
                        orbitalSpeed = 1.8f,
                        radialSpeed = 0.5f,
                        startColor = new Color(1f, 1f, 1f, 1f),
                        endColor = new Color(1f, 0.95f, 0.65f, 0f),
                        lightIntensityBoost = 4.0f
                    };
            }
        }

        /// <summary>
        /// Parámetros para Enroscar (entwine): zarcillos y espirales constrictoras alrededor del eje.
        /// </summary>
        public static SignVisualParams GetEntwineParams(ElementVariant variant, float strength, float baseScale)
        {
            switch (variant)
            {
                case ElementVariant.Fire:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Circle,
                        shapeRadiusFactor = 0.5f,
                        shapeAngle = 0f,
                        sizeFactor = 0.9f,
                        speedFactor = 0.9f,
                        lifetimeFactor = 1.1f,
                        rateFactor = 160f,
                        burstCount = 0,
                        orbitalSpeed = 4.2f,
                        radialSpeed = -0.5f,
                        startColor = new Color(1f, 0.28f, 0.05f, 0.9f),
                        endColor = new Color(1f, 0.82f, 0.15f, 0f),
                        lightIntensityBoost = 1.1f
                    };
                case ElementVariant.Water:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Circle,
                        shapeRadiusFactor = 0.55f,
                        shapeAngle = 0f,
                        sizeFactor = 0.85f,
                        speedFactor = 0.85f,
                        lifetimeFactor = 1.2f,
                        rateFactor = 170f,
                        burstCount = 0,
                        orbitalSpeed = 3.5f,
                        radialSpeed = -0.6f,
                        startColor = new Color(0.2f, 0.78f, 0.92f, 0.85f),
                        endColor = new Color(0.85f, 0.95f, 1f, 0f),
                        lightIntensityBoost = 1.0f
                    };
                case ElementVariant.Wind:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Circle,
                        shapeRadiusFactor = 0.6f,
                        shapeAngle = 0f,
                        sizeFactor = 0.95f,
                        speedFactor = 1.1f,
                        lifetimeFactor = 1.0f,
                        rateFactor = 170f,
                        burstCount = 0,
                        orbitalSpeed = 5.8f,
                        radialSpeed = -0.8f,
                        startColor = new Color(0.86f, 0.92f, 1f, 0.8f),
                        endColor = new Color(0.65f, 0.78f, 0.92f, 0f),
                        lightIntensityBoost = 1.0f
                    };
                case ElementVariant.Earth:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Circle,
                        shapeRadiusFactor = 0.5f,
                        shapeAngle = 0f,
                        sizeFactor = 1.2f,
                        speedFactor = 0.75f,
                        lifetimeFactor = 1.3f,
                        rateFactor = 140f,
                        burstCount = 0,
                        orbitalSpeed = 2.2f,
                        radialSpeed = -0.4f,
                        startColor = new Color(0.52f, 0.4f, 0.28f, 0.9f),
                        endColor = new Color(0.38f, 0.44f, 0.32f, 0f),
                        lightIntensityBoost = 0.9f
                    };
                case ElementVariant.Crystal:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Circle,
                        shapeRadiusFactor = 0.45f,
                        shapeAngle = 0f,
                        sizeFactor = 0.8f,
                        speedFactor = 0.9f,
                        lifetimeFactor = 1.15f,
                        rateFactor = 150f,
                        burstCount = 0,
                        orbitalSpeed = 3.0f,
                        radialSpeed = -0.5f,
                        startColor = new Color(0.75f, 0.52f, 1f, 0.9f),
                        endColor = new Color(0.55f, 0.92f, 1f, 0f),
                        lightIntensityBoost = 1.2f
                    };
                default: // Light
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Circle,
                        shapeRadiusFactor = 0.45f,
                        shapeAngle = 0f,
                        sizeFactor = 0.9f,
                        speedFactor = 1.0f,
                        lifetimeFactor = 1.0f,
                        rateFactor = 180f,
                        burstCount = 0,
                        orbitalSpeed = 3.8f,
                        radialSpeed = -0.5f,
                        startColor = new Color(1f, 0.88f, 0.52f, 0.95f),
                        endColor = new Color(1f, 1f, 1f, 0f),
                        lightIntensityBoost = 1.5f
                    };
            }
        }

        /// <summary>
        /// Parámetros para Mira Fija (focus / sights set): haz guía concentrado hacia la dirección de apunte.
        /// </summary>
        public static SignVisualParams GetFocusAimParams(ElementVariant variant, float strength, float baseScale)
        {
            switch (variant)
            {
                case ElementVariant.Fire:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Cone,
                        shapeRadiusFactor = 0.06f,
                        shapeAngle = 1.5f,
                        sizeFactor = 0.3f,
                        speedFactor = 2.6f,
                        lifetimeFactor = 0.8f,
                        rateFactor = 150f,
                        burstCount = 0,
                        orbitalSpeed = 0f,
                        radialSpeed = 0f,
                        startColor = new Color(1f, 0.35f, 0.05f, 1f),
                        endColor = new Color(1f, 0.95f, 0.8f, 0f),
                        lightIntensityBoost = 1.3f
                    };
                case ElementVariant.Water:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Cone,
                        shapeRadiusFactor = 0.05f,
                        shapeAngle = 1.2f,
                        sizeFactor = 0.25f,
                        speedFactor = 2.4f,
                        lifetimeFactor = 0.85f,
                        rateFactor = 160f,
                        burstCount = 0,
                        orbitalSpeed = 0f,
                        radialSpeed = 0f,
                        startColor = new Color(0.12f, 0.88f, 1f, 1f),
                        endColor = new Color(0.85f, 0.95f, 1f, 0f),
                        lightIntensityBoost = 1.2f
                    };
                case ElementVariant.Wind:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Cone,
                        shapeRadiusFactor = 0.08f,
                        shapeAngle = 1.8f,
                        sizeFactor = 0.35f,
                        speedFactor = 3.0f,
                        lifetimeFactor = 0.7f,
                        rateFactor = 170f,
                        burstCount = 0,
                        orbitalSpeed = 0f,
                        radialSpeed = 0f,
                        startColor = new Color(0.92f, 0.96f, 1f, 0.8f),
                        endColor = new Color(1f, 1f, 1f, 0f),
                        lightIntensityBoost = 1.1f
                    };
                case ElementVariant.Earth:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Cone,
                        shapeRadiusFactor = 0.09f,
                        shapeAngle = 2.2f,
                        sizeFactor = 0.4f,
                        speedFactor = 2.0f,
                        lifetimeFactor = 0.9f,
                        rateFactor = 140f,
                        burstCount = 0,
                        orbitalSpeed = 0f,
                        radialSpeed = 0f,
                        startColor = new Color(0.86f, 0.65f, 0.35f, 0.9f),
                        endColor = new Color(0.42f, 0.36f, 0.3f, 0f),
                        lightIntensityBoost = 1.0f
                    };
                case ElementVariant.Crystal:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Cone,
                        shapeRadiusFactor = 0.04f,
                        shapeAngle = 0.8f,
                        sizeFactor = 0.2f,
                        speedFactor = 3.0f,
                        lifetimeFactor = 0.75f,
                        rateFactor = 180f,
                        burstCount = 0,
                        orbitalSpeed = 0f,
                        radialSpeed = 0f,
                        startColor = new Color(0.3f, 1f, 0.9f, 1f),
                        endColor = new Color(0.95f, 0.65f, 1f, 0f),
                        lightIntensityBoost = 1.6f
                    };
                default: // Light
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Cone,
                        shapeRadiusFactor = 0.04f,
                        shapeAngle = 1.0f,
                        sizeFactor = 0.28f,
                        speedFactor = 3.4f,
                        lifetimeFactor = 0.7f,
                        rateFactor = 190f,
                        burstCount = 0,
                        orbitalSpeed = 0f,
                        radialSpeed = 0f,
                        startColor = new Color(1f, 1f, 0.92f, 1f),
                        endColor = new Color(1f, 0.85f, 0.4f, 0f),
                        lightIntensityBoost = 2.2f
                    };
            }
        }

        /// <summary>
        /// Parámetros para Reunir / Recolección (gather / collection): succión centrípeta hacia el sello.
        /// </summary>
        public static SignVisualParams GetGatherParams(ElementVariant variant, float strength, float baseScale)
        {
            switch (variant)
            {
                case ElementVariant.Fire:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Sphere,
                        shapeRadiusFactor = 3.5f,
                        shapeAngle = 0f,
                        sizeFactor = 0.9f,
                        speedFactor = -2.5f,
                        lifetimeFactor = 0.9f,
                        rateFactor = 170f,
                        burstCount = 0,
                        orbitalSpeed = 2.2f,
                        radialSpeed = 0f,
                        startColor = new Color(0.38f, 0.12f, 0.05f, 0.85f),
                        endColor = new Color(1f, 0.78f, 0.2f, 0f),
                        lightIntensityBoost = 1.1f
                    };
                case ElementVariant.Water:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Sphere,
                        shapeRadiusFactor = 3.8f,
                        shapeAngle = 0f,
                        sizeFactor = 0.8f,
                        speedFactor = -2.2f,
                        lifetimeFactor = 1.0f,
                        rateFactor = 180f,
                        burstCount = 0,
                        orbitalSpeed = 3.2f,
                        radialSpeed = 0f,
                        startColor = new Color(0.55f, 0.82f, 1f, 0.85f),
                        endColor = new Color(0.06f, 0.28f, 0.72f, 0f),
                        lightIntensityBoost = 1.0f
                    };
                case ElementVariant.Wind:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Sphere,
                        shapeRadiusFactor = 4.2f,
                        shapeAngle = 0f,
                        sizeFactor = 0.9f,
                        speedFactor = -3.0f,
                        lifetimeFactor = 0.85f,
                        rateFactor = 190f,
                        burstCount = 0,
                        orbitalSpeed = 4.2f,
                        radialSpeed = 0f,
                        startColor = new Color(0.82f, 0.88f, 0.96f, 0.8f),
                        endColor = new Color(0.95f, 1f, 1f, 0f),
                        lightIntensityBoost = 1.0f
                    };
                case ElementVariant.Earth:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Circle,
                        shapeRadiusFactor = 3.2f,
                        shapeAngle = 0f,
                        sizeFactor = 1.3f,
                        speedFactor = -1.8f,
                        lifetimeFactor = 1.1f,
                        rateFactor = 150f,
                        burstCount = 0,
                        orbitalSpeed = 1.8f,
                        radialSpeed = 0f,
                        startColor = new Color(0.62f, 0.48f, 0.32f, 0.9f),
                        endColor = new Color(0.32f, 0.24f, 0.16f, 0f),
                        lightIntensityBoost = 0.9f
                    };
                case ElementVariant.Crystal:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Sphere,
                        shapeRadiusFactor = 3.4f,
                        shapeAngle = 0f,
                        sizeFactor = 0.75f,
                        speedFactor = -2.2f,
                        lifetimeFactor = 0.95f,
                        rateFactor = 160f,
                        burstCount = 0,
                        orbitalSpeed = 2.5f,
                        radialSpeed = 0f,
                        startColor = new Color(0.65f, 0.88f, 1f, 0.9f),
                        endColor = new Color(0.88f, 0.58f, 1f, 0f),
                        lightIntensityBoost = 1.2f
                    };
                default: // Light
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Sphere,
                        shapeRadiusFactor = 4.0f,
                        shapeAngle = 0f,
                        sizeFactor = 1.0f,
                        speedFactor = -3.5f,
                        lifetimeFactor = 0.8f,
                        rateFactor = 200f,
                        burstCount = 0,
                        orbitalSpeed = 2.0f,
                        radialSpeed = 0f,
                        startColor = new Color(0.72f, 0.62f, 0.95f, 0.85f),
                        endColor = new Color(1f, 1f, 0.95f, 0f),
                        lightIntensityBoost = 1.5f
                    };
            }
        }

        /// <summary>
        /// Parámetros para Repetición (repetition): rebobinado temporal, ondas inversas y destellos de eco.
        /// </summary>
        public static SignVisualParams GetRepetitionParams(ElementVariant variant, float strength, float baseScale)
        {
            switch (variant)
            {
                case ElementVariant.Fire:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Hemisphere,
                        shapeRadiusFactor = 0.45f,
                        shapeAngle = 0f,
                        sizeFactor = 1.2f,
                        speedFactor = -1.5f,
                        lifetimeFactor = 0.8f,
                        rateFactor = 130f,
                        burstCount = 70,
                        orbitalSpeed = -1.5f,
                        radialSpeed = -0.2f,
                        startColor = new Color(0.3f, 0.22f, 0.22f, 0.8f),
                        endColor = new Color(1f, 0.52f, 0.1f, 0f),
                        lightIntensityBoost = 1.1f
                    };
                case ElementVariant.Water:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Hemisphere,
                        shapeRadiusFactor = 0.5f,
                        shapeAngle = 0f,
                        sizeFactor = 0.8f,
                        speedFactor = -1.4f,
                        lifetimeFactor = 0.85f,
                        rateFactor = 140f,
                        burstCount = 80,
                        orbitalSpeed = -1.2f,
                        radialSpeed = -0.25f,
                        startColor = new Color(0.68f, 0.85f, 1f, 0.8f),
                        endColor = new Color(0.18f, 0.48f, 0.88f, 0f),
                        lightIntensityBoost = 1.0f
                    };
                case ElementVariant.Wind:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Sphere,
                        shapeRadiusFactor = 0.55f,
                        shapeAngle = 0f,
                        sizeFactor = 1.3f,
                        speedFactor = -1.6f,
                        lifetimeFactor = 0.75f,
                        rateFactor = 130f,
                        burstCount = 70,
                        orbitalSpeed = -4.0f,
                        radialSpeed = -0.3f,
                        startColor = new Color(0.85f, 0.92f, 1f, 0.85f),
                        endColor = new Color(0.62f, 0.8f, 0.95f, 0f),
                        lightIntensityBoost = 1.0f
                    };
                case ElementVariant.Earth:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Hemisphere,
                        shapeRadiusFactor = 0.45f,
                        shapeAngle = 0f,
                        sizeFactor = 1.4f,
                        speedFactor = -1.2f,
                        lifetimeFactor = 0.9f,
                        rateFactor = 110f,
                        burstCount = 60,
                        orbitalSpeed = -1.0f,
                        radialSpeed = -0.15f,
                        startColor = new Color(0.45f, 0.38f, 0.32f, 0.85f),
                        endColor = new Color(0.72f, 0.62f, 0.48f, 0f),
                        lightIntensityBoost = 0.9f
                    };
                case ElementVariant.Crystal:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Sphere,
                        shapeRadiusFactor = 0.4f,
                        shapeAngle = 0f,
                        sizeFactor = 1.0f,
                        speedFactor = -1.4f,
                        lifetimeFactor = 0.8f,
                        rateFactor = 120f,
                        burstCount = 75,
                        orbitalSpeed = -2.0f,
                        radialSpeed = -0.2f,
                        startColor = new Color(0.8f, 0.68f, 1f, 0.9f),
                        endColor = new Color(0.55f, 1f, 1f, 0f),
                        lightIntensityBoost = 1.3f
                    };
                default: // Light
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Sphere,
                        shapeRadiusFactor = 0.45f,
                        shapeAngle = 0f,
                        sizeFactor = 1.5f,
                        speedFactor = -2.0f,
                        lifetimeFactor = 0.7f,
                        rateFactor = 150f,
                        burstCount = 90,
                        orbitalSpeed = -1.8f,
                        radialSpeed = -0.3f,
                        startColor = new Color(0.92f, 0.75f, 1f, 0.95f),
                        endColor = new Color(1f, 1f, 0.85f, 0f),
                        lightIntensityBoost = 1.8f
                    };
            }
        }

        /// <summary>
        /// Parámetros para Tejido (weave): doble hélice trenzada con emisores gemelos desfasados 180°.
        /// </summary>
        public static SignVisualParams GetWeaveParams(ElementVariant variant, float strength, float baseScale, bool isSecondStrand)
        {
            float dir = isSecondStrand ? -1f : 1f;
            switch (variant)
            {
                case ElementVariant.Fire:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Circle,
                        shapeRadiusFactor = 0.3f,
                        shapeAngle = 0f,
                        sizeFactor = 0.85f,
                        speedFactor = 1.1f,
                        lifetimeFactor = 0.9f,
                        rateFactor = 150f,
                        burstCount = 0,
                        orbitalSpeed = 3.5f * dir,
                        radialSpeed = 0f,
                        startColor = isSecondStrand ? new Color(1f, 0.72f, 0.1f, 0.95f) : new Color(1f, 0.3f, 0.05f, 0.95f),
                        endColor = new Color(0.85f, 0.12f, 0.05f, 0f),
                        lightIntensityBoost = 1.2f
                    };
                case ElementVariant.Water:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Circle,
                        shapeRadiusFactor = 0.32f,
                        shapeAngle = 0f,
                        sizeFactor = 0.8f,
                        speedFactor = 1.05f,
                        lifetimeFactor = 0.95f,
                        rateFactor = 160f,
                        burstCount = 0,
                        orbitalSpeed = 3.2f * dir,
                        radialSpeed = 0f,
                        startColor = isSecondStrand ? new Color(0.88f, 0.96f, 1f, 0.9f) : new Color(0.18f, 0.65f, 0.95f, 0.9f),
                        endColor = new Color(0.08f, 0.35f, 0.85f, 0f),
                        lightIntensityBoost = 1.1f
                    };
                case ElementVariant.Wind:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Circle,
                        shapeRadiusFactor = 0.35f,
                        shapeAngle = 0f,
                        sizeFactor = 0.9f,
                        speedFactor = 1.2f,
                        lifetimeFactor = 0.85f,
                        rateFactor = 160f,
                        burstCount = 0,
                        orbitalSpeed = 4.5f * dir,
                        radialSpeed = 0f,
                        startColor = isSecondStrand ? new Color(0.6f, 0.85f, 0.92f, 0.85f) : new Color(0.9f, 0.95f, 1f, 0.85f),
                        endColor = new Color(0.7f, 0.85f, 0.95f, 0f),
                        lightIntensityBoost = 1.0f
                    };
                case ElementVariant.Earth:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Circle,
                        shapeRadiusFactor = 0.3f,
                        shapeAngle = 0f,
                        sizeFactor = 1.1f,
                        speedFactor = 0.95f,
                        lifetimeFactor = 1.0f,
                        rateFactor = 130f,
                        burstCount = 0,
                        orbitalSpeed = 2.4f * dir,
                        radialSpeed = 0f,
                        startColor = isSecondStrand ? new Color(0.68f, 0.42f, 0.25f, 0.9f) : new Color(0.85f, 0.72f, 0.48f, 0.9f),
                        endColor = new Color(0.38f, 0.28f, 0.18f, 0f),
                        lightIntensityBoost = 0.9f
                    };
                case ElementVariant.Crystal:
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Circle,
                        shapeRadiusFactor = 0.28f,
                        shapeAngle = 0f,
                        sizeFactor = 0.75f,
                        speedFactor = 1.15f,
                        lifetimeFactor = 0.9f,
                        rateFactor = 150f,
                        burstCount = 0,
                        orbitalSpeed = 3.0f * dir,
                        radialSpeed = 0f,
                        startColor = isSecondStrand ? new Color(0.95f, 0.55f, 0.9f, 0.95f) : new Color(0.45f, 0.88f, 1f, 0.95f),
                        endColor = new Color(0.65f, 0.75f, 1f, 0f),
                        lightIntensityBoost = 1.3f
                    };
                default: // Light
                    return new SignVisualParams
                    {
                        shapeType = ParticleSystemShapeType.Circle,
                        shapeRadiusFactor = 0.3f,
                        shapeAngle = 0f,
                        sizeFactor = 0.85f,
                        speedFactor = 1.25f,
                        lifetimeFactor = 0.85f,
                        rateFactor = 180f,
                        burstCount = 0,
                        orbitalSpeed = 3.6f * dir,
                        radialSpeed = 0f,
                        startColor = isSecondStrand ? new Color(0.92f, 0.95f, 1f, 0.95f) : new Color(1f, 0.88f, 0.48f, 0.95f),
                        endColor = new Color(1f, 1f, 0.85f, 0f),
                        lightIntensityBoost = 1.5f
                    };
            }
        }
    }
}
