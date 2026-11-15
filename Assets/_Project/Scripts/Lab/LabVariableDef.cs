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
        FireMaxGenerations = 5,
        WaterFlow = 6,
        PowerScale = 7
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
            new LabVariableDef(LabVariableId.FireMaxGenerations, "Fuego: generaciones máx.", "", 0f, 12f, 1f, 6f, "0"),
            new LabVariableDef(LabVariableId.WaterFlow, "Agua: caudal", "x", 0.5f, 3f, 0.25f, 1f, "0.00"),
            new LabVariableDef(LabVariableId.PowerScale, "Multiplicador de fuerza", "x", 0.5f, 2f, 0.25f, 1f, "0.00")
        };

        /// <summary>Cantidad de variables del catalogo.</summary>
        public static int Count => Defs.Length;

        /// <summary>Definicion de una variable (sin allocations: devuelve una copia del struct).</summary>
        public static LabVariableDef Get(LabVariableId id) => Defs[(int)id];
    }
}
