using System;
using System.Collections;
using System.IO;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Compiler;
using SpellSimulator.Net;
using SpellSimulator.Reactions;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Registro de la sesion del laboratorio (consigna de Simuladores: cada cambio y su resultado). Escucha las perillas
    /// (<see cref="LabParameters.ValueChanged"/>, agrupadas), lo materializado (<see cref="LabSpawner.Materialized"/>), los hechizos
    /// (SpellCaster.SpellCompiled), las mediciones (<see cref="LabMeasurements.Measured"/>) y el reinicio, y escribe una fila por
    /// evento con <see cref="LabCsvWriter"/>. Columna evento: perilla, materializar, hechizo, medicion, reinicio. Cada medicion
    /// lleva el id_experimento de la fila que la origino. En sesiones de red no escribe nada (riesgo 11).
    /// Sin Update: el agrupado de perillas usa una corrutina que solo corre mientras hay un cambio pendiente.
    /// </summary>
    public class LabSessionLog : MonoBehaviour
    {
        [SerializeField, Tooltip("SpellCaster de la escena (fila \"hechizo\" por cada cast).")]
        private SpellCaster caster;
        [SerializeField, Tooltip("Ruta del CSV. Vacio = persistentDataPath/registro-sesion-AAAAMMDD-HHMM.csv. Una ruta relativa se " +
            "resuelve desde la carpeta de trabajo (en el Editor, la del proyecto: el smoke usa Logs/...).")]
        private string overridePath = "";

        /// <summary>Segundos sin pasos de perilla tras los cuales se escribe la fila agrupada.</summary>
        public const float KnobGroupSeconds = 0.6f;
        /// <summary>Periodo (s) con que se reintenta encontrar LabParameters/LabSpawner (pueden aparecer despues).</summary>
        public const float ResolveInterval = 1f;

        public const string EventKnob = "perilla";
        public const string EventMaterialize = "materializar";
        public const string EventSpell = "hechizo";
        public const string EventMeasurement = "medicion";
        public const string EventReset = "reinicio";
        public const string NoteOverlapped = "superpuesto";

        /// <summary>Ruta del CSV de la sesion actual (null si no hay registro habilitado).</summary>
        public static string CurrentPath { get; private set; }
        /// <summary>Filas escritas en esta sesion (sin contar el encabezado). Lo usa el smoke test.</summary>
        public static int RowsWritten { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            CurrentPath = null;
            RowsWritten = 0;
        }

        private LabCsvWriter writer;
        private string resolvedPath;
        private float sessionStart;
        private int castLastFrame = -1, castIndexInFrame;

        private LabParameters subscribedParameters;
        private LabSpawner subscribedSpawner;
        private Coroutine knobRoutine;
        private WaitForSecondsRealtime resolveWait;

        // Ultimos valores conocidos de cada variable (para anotar con que valores se materializo algo).
        private readonly float[] knownValues = new float[LabCatalog.Count];

        // Perilla pendiente de escribir (agrupado de pasos seguidos de la misma variable).
        private bool knobPending;
        private LabVariableId knobId;
        private float knobFirstPrevious;
        private float knobLast;
        private float knobDeadline;

        private void Awake()
        {
            resolvedPath = string.IsNullOrEmpty(overridePath) ? LabCsvWriter.BuildSessionPath() : Path.GetFullPath(overridePath);
            sessionStart = Time.realtimeSinceStartup;
            for (int i = 0; i < knownValues.Length; i++) knownValues[i] = LabCatalog.Defs[i].Default;
            resolveWait = new WaitForSecondsRealtime(ResolveInterval);
        }

        private void OnEnable()
        {
            CurrentPath = resolvedPath;
            if (caster != null) caster.SpellCompiled += OnSpellCompiled;
            LabMeasurements.Measured += OnMeasured;
            MagicWorld.WorldReset += OnWorldReset;
            TryResolveSources();
            // LabParameters/LabSpawner pueden habilitarse despues (o recrearse): se revisa a 1 Hz.
            StartCoroutine(ResolveLoop());
        }

        private void OnDisable()
        {
            if (knobPending) FlushKnob();
            StopAllCoroutines();
            knobRoutine = null;

            if (caster != null) caster.SpellCompiled -= OnSpellCompiled;
            LabMeasurements.Measured -= OnMeasured;
            MagicWorld.WorldReset -= OnWorldReset;
            if (!ReferenceEquals(subscribedParameters, null)) subscribedParameters.ValueChanged -= OnValueChanged;
            if (!ReferenceEquals(subscribedSpawner, null)) subscribedSpawner.Materialized -= OnMaterialized;
            subscribedParameters = null;
            subscribedSpawner = null;

            writer?.Dispose();
            writer = null;
        }

        // ---------------------------------------------------------------- suscripcion diferida

        private IEnumerator ResolveLoop()
        {
            while (true)
            {
                yield return resolveWait;
                TryResolveSources();
            }
        }

        private void TryResolveSources()
        {
            LabParameters parameters = LabParameters.Current;
            if (!ReferenceEquals(parameters, subscribedParameters))
            {
                if (!ReferenceEquals(subscribedParameters, null)) subscribedParameters.ValueChanged -= OnValueChanged;
                subscribedParameters = parameters != null ? parameters : null;
                if (!ReferenceEquals(subscribedParameters, null)) subscribedParameters.ValueChanged += OnValueChanged;
            }

            LabSpawner spawner = LabSpawner.Current;
            if (!ReferenceEquals(spawner, subscribedSpawner))
            {
                if (!ReferenceEquals(subscribedSpawner, null)) subscribedSpawner.Materialized -= OnMaterialized;
                subscribedSpawner = spawner != null ? spawner : null;
                if (!ReferenceEquals(subscribedSpawner, null)) subscribedSpawner.Materialized += OnMaterialized;
            }
        }

        // ---------------------------------------------------------------- perillas (agrupadas)

        private void OnValueChanged(LabVariableId id, float previous, float current)
        {
            int index = (int)id;
            if (index >= 0 && index < knownValues.Length) knownValues[index] = current;
            if (NetSession.Exists) return;

            // Otra variable: la anterior se cierra ya (el boton "por defecto" emite una por variable: una fila cada una).
            if (knobPending && knobId != id) FlushKnob();
            if (!knobPending)
            {
                knobPending = true;
                knobId = id;
                knobFirstPrevious = previous;
            }
            knobLast = current;
            knobDeadline = Time.unscaledTime + KnobGroupSeconds;
            if (knobRoutine == null && isActiveAndEnabled) knobRoutine = StartCoroutine(KnobTimer());
        }

        private IEnumerator KnobTimer()
        {
            while (knobPending && Time.unscaledTime < knobDeadline) yield return null;
            knobRoutine = null;
            if (knobPending) FlushKnob();
        }

        private void FlushKnob()
        {
            knobPending = false;
            // Subio y volvio al mismo valor: no hubo cambio que registrar.
            if (Mathf.Approximately(knobFirstPrevious, knobLast)) return;

            int index = (int)knobId;
            bool known = index >= 0 && index < LabCatalog.Count;
            string label = known ? LabCatalog.Defs[index].Label : knobId.ToString();
            string unit = known ? LabCatalog.Defs[index].Unit : null;
            int experiment = LabMeasurements.NextExperimentId();
            Write(new LabLogRow(DateTime.Now, SessionSeconds, EventKnob, label, knobFirstPrevious, knobLast,
                unit: string.IsNullOrEmpty(unit) ? null : unit, experimentId: experiment));
        }

        private void FlushPendingKnob()
        {
            if (knobPending) FlushKnob();
        }

        // ---------------------------------------------------------------- materializar, hechizo, medicion, reinicio

        private void OnMaterialized(LabSpawnKind kind, GameObject spawned)
        {
            if (NetSession.Exists) return;
            FlushPendingKnob();
            int experiment = LabMeasurements.NextExperimentId();

            float? value = null;
            string unit = null;
            string notes = null;
            switch (kind)
            {
                case LabSpawnKind.Box:
                    // La masa real del Rigidbody (GetComponent solo en el evento, no por frame).
                    Rigidbody body = spawned != null ? spawned.GetComponentInChildren<Rigidbody>() : null;
                    value = body != null ? body.mass : Known(LabVariableId.Mass);
                    unit = "kg";
                    break;
                case LabSpawnKind.Wind:
                    value = Known(LabVariableId.WindSpeed);
                    unit = "m/s";
                    notes = "dirección " + LabCsv.FormatNumber(Known(LabVariableId.WindYaw)) + " °";
                    break;
                case LabSpawnKind.Fire:
                    value = Known(LabVariableId.FireSeedRadius);
                    unit = "m";
                    notes = "debilitamiento " + LabCsv.FormatNumber(Known(LabVariableId.FireDecay))
                        + "; generaciones máx. " + LabCsv.FormatNumber(Known(LabVariableId.FireMaxGenerations));
                    break;
                case LabSpawnKind.Fountain:
                    value = Known(LabVariableId.WaterFlow);
                    unit = "x";
                    break;
            }
            Write(new LabLogRow(DateTime.Now, SessionSeconds, EventMaterialize, SpawnLabel(kind), current: value, unit: unit,
                experimentId: experiment, notes: notes));
        }

        private void OnSpellCompiled(SpellIR spell, DrawingCanvas canvas)
        {
            // Siempre primero: reparte el mismo id que LabMeasurements para este cast, en cualquier orden de suscripcion.
            int experiment = LabMeasurements.ExperimentIdForCast(ref castLastFrame, ref castIndexInFrame);
            if (NetSession.Exists || !spell.valid) return;
            FlushPendingKnob();

            string notes = "fuerza " + LabCsv.FormatNumber(spell.force)
                + "; alcance " + LabCsv.FormatNumber(spell.range)
                + "; calidad " + LabCsv.FormatNumber(spell.quality)
                + "; tinta x" + LabCsv.FormatNumber(spell.inkPower)
                + "; radio del sello " + LabCsv.FormatNumber(spell.sealRadius) + " m";
            if (!string.IsNullOrEmpty(spell.sigilId)) notes = "sigilo " + spell.sigilId + "; " + notes;
            Write(new LabLogRow(DateTime.Now, SessionSeconds, EventSpell, ElementLabel(spell.element),
                result: spell.active ? "lanzado" : "sin activar", experimentId: experiment, notes: notes));
        }

        private void OnMeasured(LabMeasurement m)
        {
            if (NetSession.Exists) return;
            FlushPendingKnob();
            Write(new LabLogRow(DateTime.Now, SessionSeconds, EventMeasurement, LabMeasurements.KindLabel(m.Kind),
                result: m.Label, measuredValue: m.Value, unit: m.Unit, experimentId: m.ExperimentId,
                notes: m.Overlapped ? NoteOverlapped : null));
        }

        private void OnWorldReset()
        {
            if (NetSession.Exists) return;
            FlushPendingKnob();
            Write(new LabLogRow(DateTime.Now, SessionSeconds, EventReset, result: "mundo reiniciado"));
        }

        // ---------------------------------------------------------------- escritura

        private float SessionSeconds => Time.realtimeSinceStartup - sessionStart;

        private void Write(in LabLogRow row)
        {
            if (NetSession.Exists) return;
            // El archivo se crea con la primera fila: una sesion sin experimentos no deja un CSV vacio.
            if (writer == null) writer = new LabCsvWriter(resolvedPath);
            writer.Append(LabCsv.FormatRow(row));
            RowsWritten++;
        }

        private float Known(LabVariableId id)
        {
            int index = (int)id;
            return index >= 0 && index < knownValues.Length ? knownValues[index] : 0f;
        }

        private static string SpawnLabel(LabSpawnKind kind)
        {
            switch (kind)
            {
                case LabSpawnKind.Box: return "caja";
                case LabSpawnKind.Wind: return "zona de viento";
                case LabSpawnKind.Fire: return "parche de fuego";
                case LabSpawnKind.Fountain: return "fuente";
                default: return kind.ToString();
            }
        }

        private static string ElementLabel(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return "fuego";
                case ElementType.Water: return "agua";
                case ElementType.Wind: return "viento";
                case ElementType.Earth: return "tierra";
                case ElementType.Light: return "luz";
                default: return "sin elemento";
            }
        }
    }
}
