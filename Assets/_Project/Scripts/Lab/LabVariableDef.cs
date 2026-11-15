namespace SpellSimulator.Lab
{
    /// <summary>Variables que la mesa del laboratorio deja ajustar. El valor entero indexa <see cref="LabCatalog.Defs"/>.</summary>
    public enum LabVariableId
    {
        Mass = 0,
        WindYaw = 1,
        WindSpeed = 2,
        FireSeedRadius = 3,
        FireDecay = 4,
        FireMaxGenerations = 5
    }

    /// <summary>
    /// Definicion inmutable de una variable del laboratorio: rotulo, unidad, rango, paso, valor por defecto y formato de lectura.
    /// Si <see cref="Steps"/> no es null, la variable se mueve por esa tabla de valores discretos (p. ej. la masa, en escala
    /// logaritmica) y <see cref="Min"/>/<see cref="Max"/> son su primer y ultimo valor.
    /// </summary>
    public readonly struct LabVariableDef
    {
        /// <summary>Que variable describe.</summary>
        public readonly LabVariableId Id;
        /// <summary>Rotulo en español para la perilla y el registro.</summary>
        public readonly string Label;
        /// <summary>Unidad de la lectura ("kg", "°", "m/s", "m", "x" o vacio).</summary>
        public readonly string Unit;
        /// <summary>Valor minimo.</summary>
        public readonly float Min;
        /// <summary>Valor maximo.</summary>
        public readonly float Max;
        /// <summary>Paso continuo (se ignora si hay <see cref="Steps"/>).</summary>
        public readonly float Step;
        /// <summary>Valor por defecto (= constante actual del codigo, para no cambiar el balance).</summary>
        public readonly float Default;
        /// <summary>Formato numerico .NET de la lectura (p. ej. "0", "0.0", "0.00").</summary>
        public readonly string Format;
        /// <summary>Tabla opcional de valores discretos, ordenada de menor a mayor; null si la variable es continua.</summary>
        public readonly float[] Steps;

        public LabVariableDef(LabVariableId id, string label, string unit, float min, float max, float step,
            float defaultValue, string format, float[] steps = null)
        {
            Id = id;
            Label = label;
            Unit = unit;
            Min = min;
            Max = max;
            Step = step;
            Default = defaultValue;
            Format = format;
            Steps = steps;
        }

        /// <summary>True si la variable se mueve por la tabla discreta <see cref="Steps"/>.</summary>
        public bool HasSteps => Steps != null && Steps.Length > 0;
    }

    /// <summary>
    /// Catalogo de variables del laboratorio (tabla de la seccion 2 de Docs/LABORATORIO_PLAN.md). Los defaults son los valores
    /// actuales del juego (masa 10 kg; fuego: GrassFire.cs, viento: WindCone) para que el laboratorio no cambie el balance.
    /// </summary>
    public static class LabCatalog
    {
        /// <summary>Pasos discretos de la masa de la caja (kg), escala logaritmica. 600 = Movable.maxMass.</summary>
        public static readonly float[] MassSteps = { 1f, 2f, 5f, 10f, 25f, 50f, 100f, 200f, 400f, 600f };

        /// <summary>Definiciones, indexables por <c>(int)LabVariableId</c>.</summary>
        public static readonly LabVariableDef[] Defs =
        {
            new LabVariableDef(LabVariableId.Mass, "Masa de la caja", "kg", 1f, 600f, 0f, 10f, "0", MassSteps),
            new LabVariableDef(LabVariableId.WindYaw, "Viento: dirección", "°", 0f, 315f, 45f, 0f, "0"),
            new LabVariableDef(LabVariableId.WindSpeed, "Viento: velocidad", "m/s", 0f, 20f, 1f, 6f, "0"),
            new LabVariableDef(LabVariableId.FireSeedRadius, "Fuego: radio de semilla", "m", 0.5f, 4f, 0.5f, 1f, "0.0"),
            new LabVariableDef(LabVariableId.FireDecay, "Fuego: debilitamiento", "", 0.5f, 0.95f, 0.05f, 0.78f, "0.00"),
            new LabVariableDef(LabVariableId.FireMaxGenerations, "Fuego: generaciones máx.", "", 0f, 12f, 1f, 6f, "0")
        };

        /// <summary>Cantidad de variables del catalogo.</summary>
        public static int Count => Defs.Length;

        /// <summary>Definicion de una variable (sin allocations: devuelve una copia del struct).</summary>
        public static LabVariableDef Get(LabVariableId id) => Defs[(int)id];
    }

    /// <summary>Variables del banco de sellos del Laboratorio v2 (tabla «Variables del banco» de Docs/LABORATORIO_V2_PLAN.md). Indexa <see cref="LabSealCatalog.Defs"/>.</summary>
    public enum LabSealVarId
    {
        BaseRecipe = 0,
        Sigil = 1,
        PaperSize = 2,
        RingFill = 3,
        SigilScale = 4,
        SignType = 5,
        SignCount = 6,
        BaseAngle = 7,
        TurnDeg = 8,
        SignLength = 9,
        Asymmetry = 10,
        SignDistance = 11,
        RingGap = 12,
        Tremor = 13,
        Ink = 14,
        PaperFace = 15,
        PaperTilt = 16,
        PenWidth = 17
    }

    /// <summary>Como se mueve una variable del banco: deslizador continuo, muescas numericas o lista de opciones.</summary>
    public enum LabSealVarKind { Continuous, Discrete, List }

    /// <summary>Definicion inmutable de una variable del banco de sellos.</summary>
    public readonly struct LabSealVarDef
    {
        public readonly LabSealVarId Id;
        /// <summary>Rotulo en español.</summary>
        public readonly string Label;
        /// <summary>Unidad de la lectura ("m", "°", "x", "R" o vacio).</summary>
        public readonly string Unit;
        /// <summary>En List, Min/Max son indices (Max 0 = el banco lo ajusta al tamaño real de la lista).</summary>
        public readonly float Min;
        public readonly float Max;
        public readonly float Default;
        public readonly float Step;
        public readonly LabSealVarKind Kind;

        public LabSealVarDef(LabSealVarId id, string label, string unit, float min, float max, float defaultValue, float step, LabSealVarKind kind)
        {
            Id = id; Label = label; Unit = unit; Min = min; Max = max; Default = defaultValue; Step = step; Kind = kind;
        }
    }

    /// <summary>Catalogo de variables del banco de sellos. Los defaults vienen de <see cref="LabSealSpec"/> (sin duplicar valores).</summary>
    public static class LabSealCatalog
    {
        /// <summary>Definiciones, indexables por <c>(int)LabSealVarId</c>. Receta, sigilo, tipo de signo, tinta y pluma son listas (indices).</summary>
        public static readonly LabSealVarDef[] Defs =
        {
            new LabSealVarDef(LabSealVarId.BaseRecipe, "Receta base", "", 0f, 0f, 0f, 1f, LabSealVarKind.List),
            new LabSealVarDef(LabSealVarId.Sigil, "Sigilo", "", 0f, 8f, 0f, 1f, LabSealVarKind.List),
            new LabSealVarDef(LabSealVarId.PaperSize, "Tamaño del papel", "m", 0f, 4f, LabSealSpec.DefaultPaperSize, 1f, LabSealVarKind.List),
            new LabSealVarDef(LabSealVarId.RingFill, "Llenado del anillo", "", LabSealSpec.MinRingFill, LabSealSpec.MaxRingFill, LabSealSpec.DefaultRingFill, 0.05f, LabSealVarKind.Continuous),
            new LabSealVarDef(LabSealVarId.SigilScale, "Tamaño del sigilo", "x", LabSealSpec.MinSigilScale, LabSealSpec.MaxSigilScale, 1f, 0.05f, LabSealVarKind.Continuous),
            new LabSealVarDef(LabSealVarId.SignType, "Tipo de signo", "", 0f, 11f, 0f, 1f, LabSealVarKind.List),
            new LabSealVarDef(LabSealVarId.SignCount, "Cantidad de signos", "", 0f, LabSealSpec.MaxSignCount, 0f, 1f, LabSealVarKind.Discrete),
            new LabSealVarDef(LabSealVarId.BaseAngle, "Ángulo base", "°", 0f, LabSealSpec.MaxBaseAngleDeg, 0f, LabSealSpec.BaseAngleStepDeg, LabSealVarKind.Discrete),
            new LabSealVarDef(LabSealVarId.TurnDeg, "Hacia adentro / afuera", "°", 0f, LabSealSpec.TurnOutDeg, 0f, LabSealSpec.TurnOutDeg, LabSealVarKind.Discrete),
            new LabSealVarDef(LabSealVarId.SignLength, "Largo del signo", "x", LabSealSpec.MinSignLength, LabSealSpec.MaxSignLength, 1f, 0.05f, LabSealVarKind.Continuous),
            new LabSealVarDef(LabSealVarId.Asymmetry, "Asimetría", "x", LabSealSpec.MinAsymmetry, LabSealSpec.MaxAsymmetry, 1f, 0.05f, LabSealVarKind.Continuous),
            new LabSealVarDef(LabSealVarId.SignDistance, "Distancia del signo", "R", LabSealSpec.MinSignDistance, LabSealSpec.MaxSignDistance, SpellSimulator.Learning.SpellStrokeBuilder.SignDistance, 0.01f, LabSealVarKind.Continuous),
            new LabSealVarDef(LabSealVarId.RingGap, "Hueco del anillo", "°", 0f, LabSealSpec.MaxRingGapDeg, 0f, 2f, LabSealVarKind.Continuous),
            new LabSealVarDef(LabSealVarId.Tremor, "Prolijidad (temblor)", "R", 0f, LabSealSpec.MaxTremor, 0f, 0.005f, LabSealVarKind.Continuous),
            new LabSealVarDef(LabSealVarId.Ink, "Tinta", "", 0f, 2f, 0f, 1f, LabSealVarKind.List),
            new LabSealVarDef(LabSealVarId.PaperFace, "Cara del papel", "", -1f, 1f, 1f, 2f, LabSealVarKind.Discrete),
            new LabSealVarDef(LabSealVarId.PaperTilt, "Inclinación", "°", 0f, LabSealSpec.MaxPaperTiltDeg, 0f, 5f, LabSealVarKind.Continuous),
            new LabSealVarDef(LabSealVarId.PenWidth, "Grosor de pluma", "", 0f, LabSealSpec.PenWidthCount - 1, 0f, 1f, LabSealVarKind.List)
        };

        public static int Count => Defs.Length;

        public static LabSealVarDef Get(LabSealVarId id) => Defs[(int)id];
    }
}
