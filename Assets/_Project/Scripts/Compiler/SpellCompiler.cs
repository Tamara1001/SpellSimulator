using UnityEngine;
using System.Collections.Generic;

namespace SpellSimulator.Compiler
{
    /// <summary>
    /// Modulo 4: Interprete Logico (Compilador).
    /// Traduce el indice de clase del modelo ONNX + la confianza a un SpellIR completo.
    ///
    /// Arquitectura basada en wha-spell-simulator/src/lib/compiler/spellBuilder.ts
    /// Las constantes de tuning son las mismas del proyecto de referencia.
    /// </summary>
    public class SpellCompiler : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField, Tooltip("Archivo JSON con el diccionario (ej. glyph-class-to-idx.json)")]
        private TextAsset dictionaryJson;

        // -----------------------------------------------------------------------
        // Constantes de tuning: copiadas de spellBuilder.ts y semanticRules.ts
        // -----------------------------------------------------------------------

        // Parametros base del hechizo (de SPELL_PARAMETER_TUNING en spellBuilder.ts)
        private const float FocusBase              = 0.46f;
        private const float FocusQuality           = 0.20f;
        private const float SpreadBase             = 0.32f;
        private const float SpreadInverseFocus     = 0.28f;
        private const float ForceBase              = 0.34f;
        private const float ForceQuality           = 0.18f;
        private const float RangeBase              = 0.42f;
        private const float DurationMinSeconds     = 0.65f;
        private const float DurationMaxSeconds     = 8.5f;
        private const float DurationSecondsScale   = 6.4f;
        private const float DurationQualityWeight  = 0.35f;
        private const float DurationNeatnessWeight = 0.65f;
        private const float DurationCurve         = 1.45f;

        // Escala del efecto (de config.renderer.effectSize en spellBuilder.ts)
        private const float EffectNeutralScale     = 1.91f;
        private const float EffectSizeRatioInfluence = 0.65f;
        private const float EffectMinScale         = 1.0f;
        private const float EffectMaxScale         = 2.35f;
        private const float StrengthFullSizeRatio  = 1.5f;

        // Fisica de levitacion (de PHYSICS_TUNING en spellBuilder.ts)
        private const float LevitationGravityScale = 0.42f;

        // Umbral de confianza minima para aceptar el sigio primario
        private const float MinPrimaryConfidence   = 0.45f;

        // Clasificacion de glifos por rol
        // Sigilos: definen el elemento
        private static readonly HashSet<string> SigilIds = new HashSet<string>
        {
            "fire", "water", "earth", "aeroform", "light",
            "wind-directs-air", "wind-underfoot", "crystal"
        };

        // Signos: modifican el comportamiento
        private static readonly HashSet<string> SignIds = new HashSet<string>
        {
            "column", "levitation", "pull", "dispersion", "collection",
            "convergence", "region", "billowing", "float", "crush",
            "repetition", "weave"
        };

        // Deltas semanticos por signo (force, focus, spread, range, lifetimeBias)
        // Extraidos de los archivos .json del diccionario del proyecto de referencia
        private static readonly Dictionary<string, float[]> SignSemantics =
            new Dictionary<string, float[]>
        {
            // {force, focus, spread, range, lifetimeBias}
            { "column",      new float[] {  0.12f,  0.18f, -0.08f,  0.18f,  0.10f } },
            { "levitation",  new float[] { -0.06f,  0.06f,  0.04f, -0.04f,  0.05f } },
            { "pull",        new float[] {  0.08f,  0.10f, -0.05f, -0.08f,  0.00f } },
            { "dispersion",  new float[] {  0.05f, -0.10f,  0.18f,  0.12f, -0.05f } },
            { "collection",  new float[] {  0.04f,  0.08f, -0.06f, -0.05f,  0.00f } },
            { "convergence", new float[] {  0.00f,  0.12f, -0.18f,  0.00f,  0.00f } },
            { "region",      new float[] { -0.04f, -0.04f,  0.10f, -0.10f,  0.00f } },
            { "billowing",   new float[] {  0.06f, -0.08f,  0.20f,  0.10f, -0.05f } },
            { "float",       new float[] { -0.08f,  0.04f,  0.06f, -0.06f,  0.05f } },
            { "crush",       new float[] {  0.18f,  0.14f, -0.12f, -0.08f,  0.00f } },
            { "repetition",  new float[] {  0.00f,  0.00f,  0.00f,  0.00f,  0.12f } },
            { "weave",       new float[] {  0.04f, -0.06f,  0.12f,  0.08f,  0.02f } },
        };

        // Mapa de memoria (Indice -> Nombre del Glifo)
        private Dictionary<int, string> indexToClassName = new Dictionary<int, string>();

        private void Awake()
        {
            LoadDictionary();
        }

        private void LoadDictionary()
        {
            if (dictionaryJson == null)
            {
                Debug.LogWarning("SpellCompiler: No se asigno el diccionario JSON en el inspector.");
                return;
            }

            string json = dictionaryJson.text;
            json = json.Replace("{", "").Replace("}", "").Replace("\"", "")
                       .Replace(" ", "").Replace("\r", "").Replace("\n", "");

            string[] pairs = json.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (string p in pairs)
            {
                string[] kv = p.Split(':');
                if (kv.Length == 2 && int.TryParse(kv[1], out int index))
                    indexToClassName[index] = kv[0];
            }
            Debug.Log($"[Compilador] Diccionario cargado: {indexToClassName.Count} glifos.");
        }

        /// <summary>
        /// Compila el indice de clase de la IA en un SpellIR completo.
        /// </summary>
        /// <param name="classIndex">Indice de clase predicho por el modelo ONNX.</param>
        /// <param name="confidence">Confianza del modelo (0..1).</param>
        /// <param name="isRingClosed">True si el anillo esta geometricamente cerrado.</param>
        public SpellIR Compile(int classIndex, float confidence, bool isRingClosed)
        {
            // 1. Validacion de confianza minima
            if (confidence < MinPrimaryConfidence)
            {
                Debug.Log($"[Compilador] Confianza insuficiente ({confidence:F2} < {MinPrimaryConfidence}). Hechizo invalido.");
                return SpellIR.Invalid("confidence_too_low");
            }

            // 2. Resolver el nombre del glifo
            if (!indexToClassName.TryGetValue(classIndex, out string glyphName))
            {
                Debug.LogWarning($"[Compilador] Indice {classIndex} no existe en el diccionario.");
                return SpellIR.Invalid("unknown_class");
            }

            // 3. Clasificar como sigilo o signo
            bool isSigil = SigilIds.Contains(glyphName);
            bool isSign  = SignIds.Contains(glyphName);

            if (!isSigil && !isSign)
            {
                Debug.LogWarning($"[Compilador] Glifo '{glyphName}' no clasificado como sigilo ni signo.");
                return SpellIR.Invalid("unclassified_glyph");
            }

            // 4. Por ahora (MVP sin ring detector ni stroke grouper):
            //    Si es un sigilo, compilamos un hechizo completo.
            //    Si es un signo, lo reportamos pero devolvemos invalido hasta que
            //    tengamos el ring detector y el agrupamiento de candidatos.
            if (!isSigil)
            {
                Debug.Log($"[Compilador] Glifo '{glyphName}' es un Signo. Requiere Sigilo primario. Hechizo preparado.");
                return SpellIR.Invalid("sign_without_sigil");
            }

            // 5. Mapear sigilo a elemento
            ElementType element = GlyphToElement(glyphName);
            if (element == ElementType.None)
            {
                Debug.LogWarning($"[Compilador] Sigilo '{glyphName}' sin elemento mapeado.");
                return SpellIR.Invalid("unsupported_element");
            }

            // 6. Calcular quality aproximada desde la confianza del modelo
            //    (En la Fase Media esto vendra del ring neatness + symbol neatness reales)
            float quality  = Mathf.Clamp01(confidence);
            float neatness = quality; // sin ring detector, usamos confidence como proxy

            // 7. Calcular sizeRatio y strength
            //    Sin ring detector no tenemos el tamano normalizado real,
            //    usamos 1.0 (tamano regular) como valor neutro.
            float sizeRatio   = 1.0f;
            float strength    = Mathf.Clamp01(sizeRatio / StrengthFullSizeRatio);
            float effectScale = Mathf.Clamp(
                EffectNeutralScale + (sizeRatio - 1f) * EffectSizeRatioInfluence,
                EffectMinScale,
                EffectMaxScale
            );

            // 8. Sin signos reconocidos, los deltas son cero
            //    (En la Fase Media los signos llegaran del StrokeGrouper)
            float deltaForce  = 0f;
            float deltaFocus  = 0f;
            float deltaSpread = 0f;
            float deltaRange  = 0f;
            float deltaLifetime = 0f;

            // 9. Calcular los parametros de comportamiento (formulas de spellBuilder.ts)
            float focus = Mathf.Clamp01(
                FocusBase + deltaFocus + quality * FocusQuality
            );
            float spread = Mathf.Clamp01(
                SpreadBase + deltaSpread + (1f - focus) * SpreadInverseFocus
            );
            float force = Mathf.Clamp01(
                ForceBase + deltaForce + quality * ForceQuality
            );
            float range = Mathf.Clamp01(
                RangeBase + deltaRange
            );

            // 10. Calcular duracion (curva de calidad de spellBuilder.ts)
            float durationScore = Mathf.Clamp01(
                quality  * DurationQualityWeight +
                neatness * DurationNeatnessWeight +
                deltaLifetime
            );
            float duration = Mathf.Clamp(
                DurationMinSeconds + Mathf.Pow(durationScore, DurationCurve) * DurationSecondsScale,
                DurationMinSeconds,
                DurationMaxSeconds
            );

            // 11. Stability y Gravity (sin signos ni ring: valores neutros)
            float stability = Mathf.Clamp01(quality * 0.8f + 0.2f);
            float gravity   = 1.0f; // Sin signos de levitacion

            // 12. Direccion: sin signos, direccion neutra = z (fuera del papel)
            Vector3 direction       = Vector3.forward;
            float   directionCoherence = 0f;

            // 13. Manifestacion primaria: sin signos = Aura
            ManifestationType manifestation = ManifestationType.Aura;

            // 14. Estado del hechizo
            bool active   = isRingClosed;
            bool prepared = !isRingClosed;

            SpellIR result = new SpellIR
            {
                valid              = true,
                active             = active,
                prepared           = prepared,
                element            = element,
                elementConfidence  = confidence,
                sizeRatio          = sizeRatio,
                strength           = strength,
                effectScale        = effectScale,
                primaryManifestation = manifestation,
                force              = force,
                spread             = spread,
                focus              = focus,
                range              = range,
                gravity            = gravity,
                duration           = duration,
                stability          = stability,
                quality            = quality,
                direction          = direction,
                directionCoherence = directionCoherence,
                activatedAt        = active ? Time.time : -1f
            };

            Debug.Log(
                $"[Compilador] Hechizo compilado -> " +
                $"Glifo: '{glyphName}' | Elemento: {element} | " +
                $"Confianza: {confidence:F2} | Calidad: {quality:F2} | " +
                $"Duracion: {duration:F1}s | Escala: {effectScale:F2} | " +
                $"Force: {force:F2} | Focus: {focus:F2} | Spread: {spread:F2} | " +
                $"Activo: {active}"
            );

            return result;
        }

        private static ElementType GlyphToElement(string glyphName)
        {
            switch (glyphName)
            {
                case "fire":            return ElementType.Fire;
                case "water":           return ElementType.Water;
                case "earth":           return ElementType.Earth;
                case "aeroform":        return ElementType.Wind;
                case "light":           return ElementType.Light;
                case "wind-directs-air":return ElementType.WindDirectsAir;
                case "wind-underfoot":  return ElementType.WindUnderfoot;
                case "crystal":         return ElementType.Earth; // variante de tierra
                default:                return ElementType.None;
            }
        }
    }
}
