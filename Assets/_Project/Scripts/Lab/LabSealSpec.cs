using UnityEngine;
using SpellSimulator.Casting;
using SpellSimulator.Learning;

namespace SpellSimulator.Lab
{
    /// <summary>Tipo de tinta del banco de sellos.</summary>
    public enum LabInkKind { Normal = 0, Powerful = 1, Resistant = 2 }

    /// <summary>
    /// Especificacion pura de un sello del Laboratorio v2 (tabla «Variables del banco» de Docs/LABORATORIO_V2_PLAN.md).
    /// Las distancias van en unidades de anillo (radio = 1). Los defaults reutilizan las constantes del canon
    /// (<see cref="StampPattern"/>, <see cref="SpellStrokeBuilder"/>); no se duplican valores.
    /// </summary>
    public struct LabSealSpec
    {
        // Rangos y defaults. Origen: SigilOfRadius/SignOfRadius/RingGapDeg en Casting/StampPattern.cs; SignDistance en Learning/SpellStrokeBuilder.cs.
        public static readonly float[] PaperSizes = { 0.35f, 0.45f, 0.55f, 1f, 2f };
        public const float DefaultPaperSize = 0.45f;
        public const float MinRingFill = 0.3f, MaxRingFill = 0.95f;
        /// <summary>Llenado del anillo por defecto; pendiente de verificar con HarnessSeals.</summary>
        public const float DefaultRingFill = 0.8f;
        public const float MinSigilScale = 0.6f, MaxSigilScale = 1.3f;
        public const int MaxSignCount = 4;
        public const float BaseAngleStepDeg = 45f, MaxBaseAngleDeg = 315f;
        /// <summary>Giro «hacia afuera» (180°); «hacia adentro» es 0 (TurnDeg de SpellSign).</summary>
        public const float TurnOutDeg = 180f;
        public const float MinSignLength = 0.5f, MaxSignLength = 1.5f;
        public const float MinAsymmetry = 0.6f, MaxAsymmetry = 1.6f;
        public const float MinSignDistance = 0.6f, MaxSignDistance = 0.85f;
        /// <summary>Hueco maximo del anillo (grados); StampPattern.RingGapDeg (34°) ya lo deja «preparado».</summary>
        public const float MaxRingGapDeg = 40f;
        public const float MaxTremor = 0.05f;
        /// <summary>Inclinacion del papel del campo (Laboratorio v4): 0 = de pie, 90 = mira arriba, −90 = mira abajo.</summary>
        public const float MaxPaperTiltDeg = 90f, MinPaperTiltDeg = -90f;
        /// <summary>
        /// Papel del campo (Laboratorio v4): X a lo ancho de la pista (m, 0 = centro) y distancia desde el inicio de la pista (m).
        /// Reflejan MapStructures.LaboratoryTrackWidth (10 m, con 0,5 m de margen) y LaboratoryTrackLength (12 m); MapLab avisa si no coinciden.
        /// </summary>
        public const float MaxPaperX = 4.5f, MinPaperDistance = 0.5f, MaxPaperDistance = 11.5f;
        /// <summary>Altura del centro del papel sobre el piso (m); LabSealBenchMath.PaperCenterHeight evita que se entierre.</summary>
        public const float MaxPaperHeight = 1.5f;
        public const float DefaultPaperDistance = 3f, DefaultPaperHeight = 1.2f;
        public const int PenWidthCount = 4;
        /// <summary>Atajos de posicion del papel (0..8, ver LabSealBenchMath.PaperPresets); 8 = personalizada (no carga nada).</summary>
        public const int PaperPoseCount = 9;
        public const int DefaultPaperPose = 0;
        public const int FreePaperPose = 8;

        public string recipeId;
        public string sigilId;
        public float paperSize;
        public float ringFill;
        public float sigilScale;
        public string signId;
        public int signCount;
        public float baseAngleDeg;
        /// <summary>0 = hacia adentro, 180 = hacia afuera.</summary>
        public float turnDeg;
        public float signLength;
        /// <summary>Multiplicador del largo del primer signo (un signo mas largo).</summary>
        public float asymmetry;
        public float signDistance;
        public float ringGapDeg;
        public float tremor;
        public LabInkKind ink;
        /// <summary>+1 arriba, -1 abajo.</summary>
        public int paperFace;
        public float paperTiltDeg;
        /// <summary>Indice de grosor de pluma (0..3).</summary>
        public int penWidthIndex;
        /// <summary>Ultimo atajo de posicion cargado (0..7) u 8 = personalizada (se movio algun control de posicion).</summary>
        public int paperPose;
        /// <summary>Papel del campo (v4): X lateral (m), distancia desde el inicio de la pista (m), giro (°, 0 = sale hacia el fondo, 90 = derecha, 180 = hacia el banco) y altura del centro sobre el piso (m).</summary>
        public float paperX;
        public float paperDistance;
        public float paperYawDeg;
        public float paperHeight;
        public int seed;

        /// <summary>Sello por defecto: sin signos, anillo cerrado, tinta normal.</summary>
        public static LabSealSpec Default()
        {
            return new LabSealSpec
            {
                recipeId = "gust",
                sigilId = null,
                paperSize = DefaultPaperSize,
                ringFill = DefaultRingFill,
                sigilScale = 1f,
                signId = null,
                signCount = 0,
                baseAngleDeg = 0f,
                turnDeg = 0f,
                signLength = 1f,
                asymmetry = 1f,
                signDistance = SpellStrokeBuilder.SignDistance,
                ringGapDeg = 0f,
                tremor = 0f,
                ink = LabInkKind.Normal,
                paperFace = 1,
                paperTiltDeg = 0f,
                penWidthIndex = 0,
                paperPose = DefaultPaperPose,
                paperX = 0f,
                paperDistance = DefaultPaperDistance,
                paperYawDeg = 0f,
                paperHeight = DefaultPaperHeight,
                seed = 1
            };
        }

        /// <summary>Devuelve una copia con todos los campos dentro de rango (angulo a pasos de 45°, giro a 0/180, papel al tamaño permitido mas cercano).</summary>
        public static LabSealSpec Clamp(LabSealSpec s)
        {
            s.paperSize = NearestPaper(s.paperSize);
            s.ringFill = Mathf.Clamp(s.ringFill, MinRingFill, MaxRingFill);
            s.sigilScale = Mathf.Clamp(s.sigilScale, MinSigilScale, MaxSigilScale);
            s.signCount = Mathf.Clamp(s.signCount, 0, MaxSignCount);
            float a = Mathf.Clamp(s.baseAngleDeg, 0f, MaxBaseAngleDeg);
            s.baseAngleDeg = Mathf.Round(a / BaseAngleStepDeg) * BaseAngleStepDeg;
            s.turnDeg = s.turnDeg >= TurnOutDeg * 0.5f ? TurnOutDeg : 0f;
            s.signLength = Mathf.Clamp(s.signLength, MinSignLength, MaxSignLength);
            s.asymmetry = Mathf.Clamp(s.asymmetry, MinAsymmetry, MaxAsymmetry);
            s.signDistance = Mathf.Clamp(s.signDistance, MinSignDistance, MaxSignDistance);
            s.ringGapDeg = Mathf.Clamp(s.ringGapDeg, 0f, MaxRingGapDeg);
            s.tremor = Mathf.Clamp(s.tremor, 0f, MaxTremor);
            s.ink = (LabInkKind)Mathf.Clamp((int)s.ink, 0, 2);
            s.paperFace = s.paperFace < 0 ? -1 : 1;
            s.paperTiltDeg = Mathf.Clamp(s.paperTiltDeg, MinPaperTiltDeg, MaxPaperTiltDeg);
            s.penWidthIndex = Mathf.Clamp(s.penWidthIndex, 0, PenWidthCount - 1);
            s.paperPose = Mathf.Clamp(s.paperPose, 0, PaperPoseCount - 1);
            s.paperX = Mathf.Clamp(s.paperX, -MaxPaperX, MaxPaperX);
            s.paperDistance = Mathf.Clamp(s.paperDistance, MinPaperDistance, MaxPaperDistance);
            s.paperYawDeg = Mathf.Clamp(s.paperYawDeg, -180f, 180f);
            s.paperHeight = Mathf.Clamp(s.paperHeight, 0f, MaxPaperHeight);
            return s;
        }

        private static float NearestPaper(float v)
        {
            float best = PaperSizes[0], bestD = Mathf.Abs(v - best);
            for (int i = 1; i < PaperSizes.Length; i++)
            {
                float d = Mathf.Abs(v - PaperSizes[i]);
                if (d < bestD) { bestD = d; best = PaperSizes[i]; }
            }
            return best;
        }
    }
}
