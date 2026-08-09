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
        private Dictionary<int, string> classIndexToGlyphName;

        public SpellIR CompileAST(SpellSimulator.Core.SpellAST ast, bool active)
        {
            if (ast.Ring == null || ast.Ring.Sigil == null)
            {
                Debug.LogWarning("[Compilador] AST invalido o vacio.");
                return SpellIR.Invalid("ast_empty");
            }

            // Para MVP: Usar el sigilo central como base
            SpellIR result = Compile(ast.Ring.Sigil.ClassIndex, ast.Ring.Sigil.Confidence, active);

            // Acumular deltas de los signos usando las tablas del proyecto original
            float deltaForce = 0f;
            float deltaFocus = 0f;
            float deltaSpread = 0f;
            float deltaRange = 0f;
            float deltaLifetime = 0f;

            string signNames = "Ninguno";
            if (ast.Ring.Signs.Count > 0)
            {
                List<string> names = new List<string>();
                foreach (var signNode in ast.Ring.Signs)
                {
                    if (indexToClassName != null && indexToClassName.TryGetValue(signNode.ClassIndex, out string signName))
                    {
                        names.Add(signName);
                        // Acumular deltas matematicos
                        if (SignSemantics.TryGetValue(signName, out float[] deltas))
                        {
                            deltaForce += deltas[0];
                            deltaFocus += deltas[1];
                            deltaSpread += deltas[2];
                            deltaRange += deltas[3];
                            deltaLifetime += deltas[4];
                        }
                        
                        // Aplicar reglas de manifestacion/gravedad puras
                        SemanticRules.ApplySign(ref result, SemanticRules.ParseSignName(signName));
                    }
                    else
                    {
                        names.Add($"Idx {signNode.ClassIndex}");
                    }
                }
                signNames = string.Join(", ", names);
            }

            // Recalcular los valores finales del SpellIR usando las formulas puras + los deltas
            float quality = Mathf.Clamp01(ast.Ring.Sigil.Confidence);
            float neatness = quality;

            result.focus = Mathf.Clamp01(FocusBase + deltaFocus + quality * FocusQuality);
            result.spread = Mathf.Clamp01(SpreadBase + deltaSpread + (1f - result.focus) * SpreadInverseFocus);
            result.force = Mathf.Clamp01(ForceBase + deltaForce + quality * ForceQuality);
            result.range = Mathf.Clamp01(RangeBase + deltaRange);
            
            result.duration = Mathf.Clamp(
                (DurationMinSeconds + deltaLifetime) +
                Mathf.Pow(quality * DurationQualityWeight + neatness * DurationNeatnessWeight, DurationCurve) * DurationSecondsScale,
                DurationMinSeconds,
                DurationMaxSeconds
            );

            // Emitir diagnostico actualizado con el AST y la Telemetria completa
            string log = 
                $"<b>[AST] Hechizo Compilado</b>\n" +
                $"- Sigilo: {result.element} (Anillo: {ast.HasRing})\n" +
                $"- Signos ({ast.Ring.Signs.Count}): {signNames}\n" +
                $"\n<b>[Parametros VFX]</b>\n" +
                $"- Scale: {result.effectScale:F2}\n" +
                $"- Duration: {result.duration:F1}s\n" +
                $"- Force: {result.force:F2}\n" +
                $"- Spread: {result.spread:F2}\n" +
                $"- Gravity: {result.gravity:F2}\n" +
                $"- Focus: {result.focus:F2}\n" +
                $"- Range: {result.range:F2}\n" +
                $"- Forma: {result.primaryManifestation}";
                
            Debug.Log(log);
            SpellSimulator.DebugTools.DiagnosticBus.OnDiagnosticsUpdated?.Invoke(log);

            return result;
        }

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

            // 8. Calcular los parametros de comportamiento base (sin signos todavia)
            // Estos se re-calcularan de forma final en CompileAST con todos los deltas.
            float focus  = FocusBase + quality * FocusQuality;
            float spread = SpreadBase + (1f - focus) * SpreadInverseFocus;
            float force  = ForceBase + quality * ForceQuality;
            float range  = RangeBase;

            float durationScore = Mathf.Clamp01(quality * DurationQualityWeight + neatness * DurationNeatnessWeight);
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

            // El log individual fue eliminado para que lo maneje CompileAST directamente.

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
