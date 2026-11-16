using System.Collections.Generic;

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

    /// <summary>Variables del banco de sellos del Laboratorio v2 (tabla «Variables del banco» de Docs/LABORATORIO_V2_PLAN.md). Indexa <see cref="LabSealCatalog.Defs"/>. Los ids no se renumeran: los nuevos van al final.</summary>
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
        PenWidth = 17,
        /// <summary>Preset de posicion del papel (Laboratorio v3): atril, piso, pista, etc. Ver <see cref="LabSealBenchMath.PaperPose"/>.</summary>
        PaperPose = 18,
        /// <summary>Laboratorio v4: posicion libre del papel del campo (X, distancia por la pista, giro y altura).</summary>
        PaperX = 19,
        PaperDistance = 20,
        PaperYaw = 21,
        PaperHeight = 22
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
        /// <summary>Ayuda corta en español (hasta <see cref="LabSealCatalog.MaxHelpLength"/> caracteres): que hace y que pasa al subirla.</summary>
        public readonly string Help;
        /// <summary>Grupo: 1 Hechizo, 2 Signos, 3 Papel y tinta, 4 Afinado (consola del sello); 5 Papel de prueba (panel de objetos).</summary>
        public readonly int Group;

        public LabSealVarDef(LabSealVarId id, string label, string unit, float min, float max, float defaultValue, float step,
            LabSealVarKind kind, string help = "", int group = 1)
        {
            Id = id; Label = label; Unit = unit; Min = min; Max = max; Default = defaultValue; Step = step; Kind = kind;
            Help = help; Group = group;
        }
    }

    /// <summary>Catalogo de variables del banco de sellos. Los defaults vienen de <see cref="LabSealSpec"/> (sin duplicar valores).</summary>
    public static class LabSealCatalog
    {
        /// <summary>Largo maximo de <see cref="LabSealVarDef.Help"/> (cabe en la tira de ayuda de la consola).</summary>
        public const int MaxHelpLength = 60;
        /// <summary>Cantidad de grupos de consola (numerados desde 1).</summary>
        public const int GroupCount = 5;
        /// <summary>Grupos de la consola del sello (1..SealGroupCount); el 5 es el papel de prueba del panel de objetos.</summary>
        public const int SealGroupCount = 4;

        /// <summary>Titulos de los grupos, indexados por grupo - 1.</summary>
        public static readonly string[] GroupNames = { "1 Hechizo", "2 Signos", "3 Papel y tinta", "4 Afinado", "Papel de prueba" };

        /// <summary>Definiciones, indexables por <c>(int)LabSealVarId</c>. Receta, sigilo, tipo de signo, tinta, pluma y posicion del papel son listas (indices).</summary>
        public static readonly LabSealVarDef[] Defs =
        {
            new LabSealVarDef(LabSealVarId.BaseRecipe, "Receta base", "", 0f, 0f, 0f, 1f, LabSealVarKind.List,
                "Carga un hechizo del juego. «Libre» deja el sello vacío.", 1),
            new LabSealVarDef(LabSealVarId.Sigil, "Sigilo", "", 0f, 8f, 0f, 1f, LabSealVarKind.List,
                "Dibujo central del sello: define el elemento.", 1),
            new LabSealVarDef(LabSealVarId.PaperSize, "Tamaño del papel", "m", 0f, 4f, LabSealSpec.DefaultPaperSize, 1f, LabSealVarKind.List,
                "Lado del papel de prueba. Más grande, sello más grande.", 5),
            new LabSealVarDef(LabSealVarId.RingFill, "Llenado del anillo", "", LabSealSpec.MinRingFill, LabSealSpec.MaxRingFill, LabSealSpec.DefaultRingFill, 0.05f, LabSealVarKind.Continuous,
                "Cuánto del papel ocupa el anillo. Más, anillo más grande.", 4),
            new LabSealVarDef(LabSealVarId.SigilScale, "Tamaño del sigilo", "x", LabSealSpec.MinSigilScale, LabSealSpec.MaxSigilScale, 1f, 0.05f, LabSealVarKind.Continuous,
                "Tamaño del sigilo en el anillo. Más, sigilo más grande.", 4),
            new LabSealVarDef(LabSealVarId.SignType, "Tipo de signo", "", 0f, 11f, 0f, 1f, LabSealVarKind.List,
                "Clase de signo que se repite alrededor del anillo.", 2),
            new LabSealVarDef(LabSealVarId.SignCount, "Cantidad de signos", "", 0f, LabSealSpec.MaxSignCount, 0f, 1f, LabSealVarKind.Discrete,
                "Cuántos signos hay. Más signos, más modificaciones.", 2),
            new LabSealVarDef(LabSealVarId.BaseAngle, "Ángulo base", "°", 0f, LabSealSpec.MaxBaseAngleDeg, 0f, LabSealSpec.BaseAngleStepDeg, LabSealVarKind.Discrete,
                "Dónde cae el primer signo. Sube de a 45°.", 2),
            new LabSealVarDef(LabSealVarId.TurnDeg, "Hacia adentro / afuera", "°", 0f, LabSealSpec.TurnOutDeg, 0f, LabSealSpec.TurnOutDeg, LabSealVarKind.Discrete,
                "Signos hacia adentro o hacia afuera del anillo.", 2),
            new LabSealVarDef(LabSealVarId.SignLength, "Largo del signo", "x", LabSealSpec.MinSignLength, LabSealSpec.MaxSignLength, 1f, 0.05f, LabSealVarKind.Continuous,
                "Largo de cada signo. Más largo, efecto más marcado.", 4),
            new LabSealVarDef(LabSealVarId.Asymmetry, "Asimetría", "x", LabSealSpec.MinAsymmetry, LabSealSpec.MaxAsymmetry, 1f, 0.05f, LabSealVarKind.Continuous,
                "Largo extra del primer signo. Más, sello más desparejo.", 4),
            new LabSealVarDef(LabSealVarId.SignDistance, "Distancia del signo", "R", LabSealSpec.MinSignDistance, LabSealSpec.MaxSignDistance, SpellSimulator.Learning.SpellStrokeBuilder.SignDistance, 0.01f, LabSealVarKind.Continuous,
                "Distancia de los signos al centro. Más, más lejos.", 4),
            new LabSealVarDef(LabSealVarId.RingGap, "Hueco del anillo", "°", 0f, LabSealSpec.MaxRingGapDeg, 0f, 2f, LabSealVarKind.Continuous,
                "Hueco del anillo. Desde 34° queda «preparado».", 4),
            new LabSealVarDef(LabSealVarId.Tremor, "Prolijidad (temblor)", "R", 0f, LabSealSpec.MaxTremor, 0f, 0.005f, LabSealVarKind.Continuous,
                "Temblor del trazo. Más temblor, sello menos prolijo.", 4),
            new LabSealVarDef(LabSealVarId.Ink, "Tinta", "", 0f, 2f, 0f, 1f, LabSealVarKind.List,
                "Tinta normal, poderosa o resistente.", 3),
            new LabSealVarDef(LabSealVarId.PaperFace, "Sentido del disparo", "", -1f, 1f, 1f, 2f, LabSealVarKind.Discrete,
                "Invertido: el hechizo sale por el otro lado del papel.", 3),
            new LabSealVarDef(LabSealVarId.PaperTilt, "Inclinación", "°", LabSealSpec.MinPaperTiltDeg, LabSealSpec.MaxPaperTiltDeg, 0f, 15f, LabSealVarKind.Discrete,
                "0 de pie, 90 mira arriba, −90 mira abajo.", 5),
            new LabSealVarDef(LabSealVarId.PenWidth, "Grosor de pluma", "", 0f, LabSealSpec.PenWidthCount - 1, 0f, 1f, LabSealVarKind.List,
                "Grosor de la pluma. Más grueso, trazo más ancho.", 3),
            new LabSealVarDef(LabSealVarId.PaperPose, "Atajo de posición", "", 0f, LabSealSpec.PaperPoseCount - 1, LabSealSpec.DefaultPaperPose, 1f, LabSealVarKind.List,
                "Carga una posición típica: Frente, Piso, Izquierda…", 5),
            new LabSealVarDef(LabSealVarId.PaperX, "Papel: lado", "m", -LabSealSpec.MaxPaperX, LabSealSpec.MaxPaperX, 0f, 0.1f, LabSealVarKind.Continuous,
                "Mueve el papel a la izquierda o a la derecha de la pista.", 5),
            new LabSealVarDef(LabSealVarId.PaperDistance, "Papel: distancia", "m", LabSealSpec.MinPaperDistance, LabSealSpec.MaxPaperDistance, LabSealSpec.DefaultPaperDistance, 0.1f, LabSealVarKind.Continuous,
                "Distancia del papel desde el comienzo de la pista.", 5),
            new LabSealVarDef(LabSealVarId.PaperYaw, "Papel: giro", "°", -180f, 180f, 0f, 15f, LabSealVarKind.Discrete,
                "0 dispara al fondo, 180 hacia vos, ±90 a los lados.", 5),
            new LabSealVarDef(LabSealVarId.PaperHeight, "Papel: altura", "m", 0f, LabSealSpec.MaxPaperHeight, LabSealSpec.DefaultPaperHeight, 0.05f, LabSealVarKind.Continuous,
                "Altura del centro del papel: desde el piso hasta 1,5 m.", 5)
        };

        // Ids por grupo, en el orden en que se muestran en la consola (izquierda a derecha).
        private static readonly LabSealVarId[][] GroupIds =
        {
            // Laboratorio v4: lo mas importante arriba (hechizo, signos, papel/tinta, afinado). El 5 es el papel del campo; el
            // atajo de posicion va al final para que un reset de grupo lo deje en su valor despues de mover X, distancia, etc.
            new[] { LabSealVarId.BaseRecipe, LabSealVarId.Sigil },
            new[] { LabSealVarId.SignType, LabSealVarId.SignCount, LabSealVarId.BaseAngle, LabSealVarId.TurnDeg },
            new[] { LabSealVarId.Ink, LabSealVarId.PenWidth, LabSealVarId.PaperFace },
            new[] { LabSealVarId.SigilScale, LabSealVarId.RingFill, LabSealVarId.RingGap, LabSealVarId.SignLength,
                    LabSealVarId.Asymmetry, LabSealVarId.SignDistance, LabSealVarId.Tremor },
            new[] { LabSealVarId.PaperX, LabSealVarId.PaperDistance, LabSealVarId.PaperYaw, LabSealVarId.PaperHeight,
                    LabSealVarId.PaperTilt, LabSealVarId.PaperSize, LabSealVarId.PaperPose }
        };

        public static int Count => Defs.Length;

        public static LabSealVarDef Get(LabSealVarId id) => Defs[(int)id];

        /// <summary>Grupo de consola (1..<see cref="GroupCount"/>) de una variable.</summary>
        public static int GroupOf(LabSealVarId id) => Defs[(int)id].Group;

        /// <summary>Variables de un grupo (1..<see cref="GroupCount"/>) en orden de consola; lista vacia si el grupo no existe. Sin allocations.</summary>
        public static IReadOnlyList<LabSealVarId> IdsOfGroup(int group)
            => group >= 1 && group <= GroupCount ? GroupIds[group - 1] : System.Array.Empty<LabSealVarId>();

        /// <summary>Titulo del grupo (vacio si no existe).</summary>
        public static string GroupName(int group) => group >= 1 && group <= GroupCount ? GroupNames[group - 1] : string.Empty;
    }
}
