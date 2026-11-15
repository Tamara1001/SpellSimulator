using System;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Compiler;
using SpellSimulator.Net;
using SpellSimulator.Output;
using SpellSimulator.Reactions;
using SpellSimulator.World;

namespace SpellSimulator.Lab
{
    /// <summary>Que mide una <see cref="LabMeasurement"/>.</summary>
    public enum LabMeasurementKind
    {
        /// <summary>Desplazamiento horizontal (m) de una caja en un segmento de movimiento.</summary>
        BoxDisplacement,
        /// <summary>Area quemada (m2) en un episodio de fuego del pasto.</summary>
        FireArea,
        /// <summary>Alcance (m) de un hechizo en su ventana de 5 s.</summary>
        CastReach,
        /// <summary>Duracion (s) del hechizo (SpellIR.duration).</summary>
        CastDuration
    }

    /// <summary>Resultado medido de un experimento del laboratorio (por valor, sin allocations).</summary>
    public readonly struct LabMeasurement
    {
        /// <summary>Tipo de medicion.</summary>
        public readonly LabMeasurementKind Kind;
        /// <summary>Experimento al que pertenece (fila del CSV que lo origino).</summary>
        public readonly int ExperimentId;
        /// <summary>Valor medido.</summary>
        public readonly float Value;
        /// <summary>Unidad del valor.</summary>
        public readonly string Unit;
        /// <summary>True si habia otro experimento del mismo tipo abierto (la medicion puede estar contaminada).</summary>
        public readonly bool Overlapped;
        /// <summary>Descripcion corta del resultado (columna "resultado").</summary>
        public readonly string Label;

        public LabMeasurement(LabMeasurementKind kind, int experimentId, float value, string unit, bool overlapped, string label)
        {
            Kind = kind;
            ExperimentId = experimentId;
            Value = value;
            Unit = unit;
            Overlapped = overlapped;
            Label = label;
        }
    }

    /// <summary>
    /// Mediciones del laboratorio, muestreadas a 10 Hz y sin GC por frame (arrays preasignados por slot, sin diccionarios):
    /// (a) segmentos de movimiento de cada <see cref="Movable"/> (desplazamiento horizontal desde la posicion previa al movimiento
    /// hasta el reposo de 1 s o el corte de 15 s); (b) episodios de fuego del pasto (desde que <see cref="GrassFire.BurningCells"/>
    /// pasa de 0 a &gt; 0 hasta que vuelve a 0 o 40 s; area = celdas quemadas en el episodio x 4 m2); (c) ventana de 5 s tras cada
    /// cast valido y activo (duracion del SpellIR y alcance). Emite <see cref="Measured"/>; tambien reparte los id de experimento.
    /// <para>Alcance (decision): desplazamiento horizontal maximo, durante la ventana, de las cajas que estaban a menos de 8 m del
    /// sello al lanzarlo (es un resultado fisico medido). Si no habia ninguna caja cerca, se informa el largo del cono
    /// (<see cref="MagicField.ConeLength"/>) del campo que salio de ese lienzo, con otro rotulo en la columna "resultado".</para>
    /// <para>Id de experimento: cada medicion se cuelga del ultimo experimento abierto (<see cref="CurrentExperimentId"/>) al empezar,
    /// salvo las cajas a &lt; 8 m de un cast abierto, que se cuelgan de ese cast. Superpuesto: cajas = otro segmento abierto con otro
    /// id; fuego = se abrio otro experimento durante el episodio (la grilla es una sola); cast = otra ventana de cast abierta.</para>
    /// </summary>
    public class LabMeasurements : MonoBehaviour
    {
        [SerializeField, Tooltip("SpellCaster de la escena (ventana de medicion de cada cast).")]
        private SpellCaster caster;

        /// <summary>Periodo de muestreo (s): 10 Hz.</summary>
        public const float SampleInterval = 0.1f;
        /// <summary>Tope de un episodio de fuego (s).</summary>
        public const float FireMaxSeconds = 40f;
        /// <summary>Duracion de la ventana de medicion de un cast (s).</summary>
        public const float CastWindowSeconds = 5f;
        /// <summary>Radio horizontal (m) alrededor del sello en el que se siguen cajas para el alcance.</summary>
        public const float CastBoxRadius = 8f;
        /// <summary>Desplazamiento minimo (m) para informar un segmento de caja (evita ruido de asentarse al aparecer).</summary>
        public const float MinReportedDisplacement = 0.05f;

        /// <summary>Unidad de distancias.</summary>
        public const string UnitMeters = "m";
        /// <summary>Unidad del area quemada (resolucion de la grilla del fuego).</summary>
        public const string UnitFireArea = "m2 (celdas de 4 m2)";
        /// <summary>Unidad de tiempo.</summary>
        public const string UnitSeconds = "s";

        private const int MaxBoxSlots = 64;
        private const int MaxCastWindows = 4;
        private const int CastClaimRing = 16;

        // ---------------------------------------------------------------- estatico: evento e ids de experimento

        /// <summary>Se emite al cerrar cada medicion (desde el muestreo o un evento, nunca por frame sin datos).</summary>
        public static event Action<LabMeasurement> Measured;

        /// <summary>Id del ultimo experimento abierto (0 = ninguno). Lo usa <see cref="LabSessionLog"/> para enlazar.</summary>
        public static int CurrentExperimentId { get; private set; }

        private static int experimentCounter;
        // Reparto de ids de cast entre quienes escuchan SpellCompiled (orden de suscripcion indistinto): clave = (frame, n-esimo cast del frame).
        private static readonly long[] castClaimKeys = new long[CastClaimRing];
        private static readonly int[] castClaimIds = new int[CastClaimRing];
        private static int castClaimCursor;

        // Con Enter Play Mode sin recarga de dominio los estaticos sobrevivirian entre sesiones de Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Measured = null;
            experimentCounter = 0;
            CurrentExperimentId = 0;
            castClaimCursor = 0;
            for (int i = 0; i < CastClaimRing; i++)
            {
                castClaimKeys[i] = 0;
                castClaimIds[i] = 0;
            }
        }

        /// <summary>Abre un experimento nuevo y devuelve su id (pasa a ser <see cref="CurrentExperimentId"/>).</summary>
        public static int NextExperimentId()
        {
            CurrentExperimentId = ++experimentCounter;
            return CurrentExperimentId;
        }

        /// <summary>
        /// Id de experimento del cast que se esta notificando por SpellCaster.SpellCompiled. Todo el que escuche ese evento lo llama
        /// al principio de su handler (siempre, aunque despues descarte el hechizo) con sus propios contadores (inicializar
        /// <paramref name="lastFrame"/> en -1): el primero que llega abre el experimento y los demas reciben el mismo id.
        /// </summary>
        public static int ExperimentIdForCast(ref int lastFrame, ref int indexInFrame)
        {
            int frame = Time.frameCount;
            if (frame != lastFrame)
            {
                lastFrame = frame;
                indexInFrame = 0;
            }
            else indexInFrame++;

            long key = ((long)frame << 16) | (long)(indexInFrame & 0xFFFF);
            for (int i = 0; i < CastClaimRing; i++)
                if (castClaimIds[i] > 0 && castClaimKeys[i] == key) return castClaimIds[i];

            int id = NextExperimentId();
            castClaimKeys[castClaimCursor] = key;
            castClaimIds[castClaimCursor] = id;
            castClaimCursor = (castClaimCursor + 1) % CastClaimRing;
            return id;
        }

        /// <summary>Rotulo en español de un tipo de medicion (columna "variable" del CSV y contadores).</summary>
        public static string KindLabel(LabMeasurementKind kind)
        {
            switch (kind)
            {
                case LabMeasurementKind.BoxDisplacement: return "distancia de la caja";
                case LabMeasurementKind.FireArea: return "área quemada";
                case LabMeasurementKind.CastReach: return "alcance del hechizo";
                case LabMeasurementKind.CastDuration: return "duración del hechizo";
                default: return "medición";
            }
        }

        // ---------------------------------------------------------------- estado por instancia (preasignado)

        private float sampleTimer;
        private int castLastFrame = -1, castIndexInFrame;

        // (a) cajas, indexadas por slot
        private readonly Movable[] slotOwner = new Movable[MaxBoxSlots];
        private readonly bool[] slotSeen = new bool[MaxBoxSlots];
        private readonly SettleState[] slotState = new SettleState[MaxBoxSlots];
        private readonly Vector3[] slotLast = new Vector3[MaxBoxSlots];
        private readonly Vector3[] slotStart = new Vector3[MaxBoxSlots];
        private readonly int[] slotExperiment = new int[MaxBoxSlots];
        private readonly bool[] slotOverlapped = new bool[MaxBoxSlots];

        // (b) fuego (una sola grilla: un episodio a la vez)
        private bool fireOpen;
        private float fireElapsed;
        private int fireBurntAtStart;
        private int fireExperiment;
        private int fireLastBurning;
        private bool fireOverlapped;

        // (c) ventanas de cast
        private readonly bool[] castOpen = new bool[MaxCastWindows];
        private readonly float[] castElapsed = new float[MaxCastWindows];
        private readonly int[] castExperiment = new int[MaxCastWindows];
        private readonly bool[] castOverlapped = new bool[MaxCastWindows];
        private readonly Vector3[] castOrigin = new Vector3[MaxCastWindows];
        private readonly GameObject[] castSource = new GameObject[MaxCastWindows];
        private readonly float[] castMaxDisplacement = new float[MaxCastWindows];
        private readonly float[] castConeLength = new float[MaxCastWindows];
        private readonly bool[] castHadBoxes = new bool[MaxCastWindows];
        private readonly bool[] castDurationPending = new bool[MaxCastWindows];
        private readonly float[] castDuration = new float[MaxCastWindows];
        private readonly bool[] castTracked = new bool[MaxCastWindows * MaxBoxSlots];
        private readonly Vector3[] castBoxStart = new Vector3[MaxCastWindows * MaxBoxSlots];

        private void OnEnable()
        {
            if (caster != null) caster.SpellCompiled += OnSpellCompiled;
            MagicWorld.WorldReset += OnWorldReset;
        }

        private void OnDisable()
        {
            if (caster != null) caster.SpellCompiled -= OnSpellCompiled;
            MagicWorld.WorldReset -= OnWorldReset;
        }

        private void Update()
        {
            // En red no se replica nada del laboratorio y el fuego del cliente no simula (riesgo 11).
            if (NetSession.Exists) return;
            sampleTimer += Time.deltaTime;
            if (sampleTimer < SampleInterval) return;
            float dt = sampleTimer;
            sampleTimer = 0f;

            SampleBoxes(dt);
            SampleFire(dt);
            SampleCasts(dt);
        }

        // ---------------------------------------------------------------- (a) cajas

        /// <summary>Asigna un slot a cada Movable activo y libera los de los que ya no estan. Sin allocations.</summary>
        private void AssignSlots()
        {
            for (int s = 0; s < MaxBoxSlots; s++) slotSeen[s] = false;

            var all = Movable.All;
            for (int i = 0; i < all.Count; i++)
            {
                Movable m = all[i];
                if (m == null) continue;
                int slot = FindSlot(m);
                if (slot < 0) slot = TakeFreeSlot(m);
                if (slot >= 0) slotSeen[slot] = true;
            }

            for (int s = 0; s < MaxBoxSlots; s++)
                if (!ReferenceEquals(slotOwner[s], null) && !slotSeen[s]) ClearSlot(s);
        }

        private int FindSlot(Movable m)
        {
            for (int s = 0; s < MaxBoxSlots; s++)
                if (ReferenceEquals(slotOwner[s], m)) return s;
            return -1;
        }

        private int TakeFreeSlot(Movable m)
        {
            for (int s = 0; s < MaxBoxSlots; s++)
            {
                if (!ReferenceEquals(slotOwner[s], null)) continue;
                slotOwner[s] = m;
                slotState[s] = default;
                slotOverlapped[s] = false;
                slotExperiment[s] = 0;
                var body = m.Body;
                slotLast[s] = body != null ? body.position : m.transform.position;
                return s;
            }
            return -1; // mas de MaxBoxSlots objetos: los demas no se miden
        }

        private void ClearSlot(int s)
        {
            slotOwner[s] = null;
            slotState[s] = default;
            for (int w = 0; w < MaxCastWindows; w++) castTracked[w * MaxBoxSlots + s] = false;
        }

        private void SampleBoxes(float dt)
        {
            AssignSlots();
            for (int s = 0; s < MaxBoxSlots; s++)
            {
                Movable m = slotOwner[s];
                if (ReferenceEquals(m, null) || m == null) continue;
                Rigidbody body = m.Body;
                if (body == null) continue;

                Vector3 pos = body.position;
                bool wasStarted = slotState[s].Started;
                bool finished = LabMetricsMath.SettleStep(ref slotState[s], body.linearVelocity.magnitude, dt);

                if (!wasStarted && slotState[s].Started)
                {
                    // Se mide desde la ultima muestra quieta: la caja ya se movio hasta 0,1 s cuando se detecta.
                    slotStart[s] = slotLast[s];
                    slotExperiment[s] = ExperimentForBoxAt(slotStart[s]);
                    slotOverlapped[s] = false;
                    for (int o = 0; o < MaxBoxSlots; o++)
                    {
                        if (o == s || !slotState[o].Active || slotExperiment[o] == slotExperiment[s]) continue;
                        slotOverlapped[o] = true;
                        slotOverlapped[s] = true;
                    }
                }

                if (finished)
                {
                    float d = LabMetricsMath.HorizontalDisplacement(slotStart[s], pos);
                    if (d >= MinReportedDisplacement)
                    {
                        string label = slotState[s].TimedOut
                            ? "desplazamiento de la caja (cortado a 15 s)"
                            : "desplazamiento de la caja hasta el reposo";
                        Emit(new LabMeasurement(LabMeasurementKind.BoxDisplacement, slotExperiment[s], d, UnitMeters,
                            slotOverlapped[s], label));
                    }
                    slotState[s] = default;
                    slotOverlapped[s] = false;
                }
                slotLast[s] = pos;
            }
        }

        /// <summary>Experimento al que se cuelga un segmento de caja que empieza en <paramref name="pos"/>.</summary>
        private int ExperimentForBoxAt(Vector3 pos)
        {
            int best = 0;
            for (int w = 0; w < MaxCastWindows; w++)
            {
                if (!castOpen[w] || castExperiment[w] <= best) continue;
                if (LabMetricsMath.HorizontalDisplacement(castOrigin[w], pos) <= CastBoxRadius) best = castExperiment[w];
            }
            if (best > 0) return best;
            return CurrentExperimentId > 0 ? CurrentExperimentId : NextExperimentId();
        }

        // ---------------------------------------------------------------- (b) fuego

        private void SampleFire(float dt)
        {
            GrassFire fire = GrassFire.Current;
            if (fire == null)
            {
                // Sin grilla de fuego no se mide (ni se rompe): se descarta el episodio abierto.
                fireOpen = false;
                fireLastBurning = 0;
                return;
            }

            int burning = fire.BurningCells;
            if (!fireOpen)
            {
                if (burning > 0 && fireLastBurning == 0)
                {
                    fireOpen = true;
                    fireElapsed = 0f;
                    fireBurntAtStart = fire.BurntCells;
                    fireExperiment = CurrentExperimentId > 0 ? CurrentExperimentId : NextExperimentId();
                    fireOverlapped = false;
                }
            }
            else
            {
                fireElapsed += dt;
                if (CurrentExperimentId != fireExperiment) fireOverlapped = true;

                bool timedOut = fireElapsed >= FireMaxSeconds;
                if (burning == 0 || timedOut)
                {
                    fireOpen = false;
                    int burnt = fire.BurntCells;
                    // BurntCells menor que al empezar = hubo un reinicio de la grilla: el episodio no vale.
                    if (burnt >= fireBurntAtStart)
                    {
                        // Al cortar por tiempo, lo que sigue ardiendo tambien se va a quemar: se cuenta.
                        int cells = burnt - fireBurntAtStart + (timedOut ? burning : 0);
                        string label = timedOut ? "área quemada (cortado a 40 s)" : "área quemada hasta apagarse";
                        Emit(new LabMeasurement(LabMeasurementKind.FireArea, fireExperiment, LabMetricsMath.AreaBurnedM2(cells),
                            UnitFireArea, fireOverlapped, label));
                    }
                }
            }
            fireLastBurning = burning;
        }

        // ---------------------------------------------------------------- (c) casts

        private void OnSpellCompiled(SpellIR spell, DrawingCanvas canvas)
        {
            int id = ExperimentIdForCast(ref castLastFrame, ref castIndexInFrame);
            if (NetSession.Exists || !spell.valid || !spell.active) return;

            bool overlapped = false;
            for (int w = 0; w < MaxCastWindows; w++)
            {
                if (!castOpen[w]) continue;
                castOverlapped[w] = true;
                overlapped = true;
            }

            int win = -1;
            for (int w = 0; w < MaxCastWindows; w++)
                if (!castOpen[w]) { win = w; break; }
            if (win < 0)
            {
                // Mas de 4 casts en 5 s: se pisa la ventana mas vieja sin informarla.
                win = 0;
                for (int w = 1; w < MaxCastWindows; w++)
                    if (castElapsed[w] > castElapsed[win]) win = w;
            }

            castOpen[win] = true;
            castElapsed[win] = 0f;
            castExperiment[win] = id;
            castOverlapped[win] = overlapped;
            castOrigin[win] = canvas != null ? canvas.transform.position : Vector3.zero;
            castSource[win] = canvas != null ? canvas.gameObject : null;
            castMaxDisplacement[win] = 0f;
            castConeLength[win] = -1f;
            castHadBoxes[win] = false;
            // La duracion se emite en la proxima muestra, para que la fila "hechizo" quede antes en el CSV.
            castDurationPending[win] = true;
            castDuration[win] = spell.duration;

            AssignSlots();
            int baseIndex = win * MaxBoxSlots;
            for (int s = 0; s < MaxBoxSlots; s++)
            {
                castTracked[baseIndex + s] = false;
                Movable m = slotOwner[s];
                if (ReferenceEquals(m, null) || m == null) continue;
                Rigidbody body = m.Body;
                if (body == null) continue;
                if (LabMetricsMath.HorizontalDisplacement(castOrigin[win], body.position) > CastBoxRadius) continue;
                castTracked[baseIndex + s] = true;
                castBoxStart[baseIndex + s] = body.position;
                castHadBoxes[win] = true;
            }
        }

        private void SampleCasts(float dt)
        {
            for (int w = 0; w < MaxCastWindows; w++)
            {
                if (!castOpen[w]) continue;
                castElapsed[w] += dt;

                if (castDurationPending[w])
                {
                    castDurationPending[w] = false;
                    Emit(new LabMeasurement(LabMeasurementKind.CastDuration, castExperiment[w], castDuration[w], UnitSeconds,
                        castOverlapped[w], "duración del hechizo"));
                }

                // Largo del cono del campo que salio de este lienzo (el campo se crea despues de SpellCompiled y puede morir antes de 5 s).
                if (castSource[w] != null)
                {
                    var fields = MagicField.Active;
                    for (int f = 0; f < fields.Count; f++)
                    {
                        MagicField field = fields[f];
                        if (field == null || !ReferenceEquals(field.Source, castSource[w])) continue;
                        if (field.ConeLength > castConeLength[w]) castConeLength[w] = field.ConeLength;
                    }
                }

                int baseIndex = w * MaxBoxSlots;
                for (int s = 0; s < MaxBoxSlots; s++)
                {
                    if (!castTracked[baseIndex + s]) continue;
                    Movable m = slotOwner[s];
                    if (ReferenceEquals(m, null) || m == null) continue;
                    Rigidbody body = m.Body;
                    if (body == null) continue;
                    float d = LabMetricsMath.HorizontalDisplacement(castBoxStart[baseIndex + s], body.position);
                    if (d > castMaxDisplacement[w]) castMaxDisplacement[w] = d;
                }

                if (castElapsed[w] < CastWindowSeconds) continue;

                castOpen[w] = false;
                castSource[w] = null;
                for (int s = 0; s < MaxBoxSlots; s++) castTracked[baseIndex + s] = false;
                if (castHadBoxes[w])
                    Emit(new LabMeasurement(LabMeasurementKind.CastReach, castExperiment[w], castMaxDisplacement[w], UnitMeters,
                        castOverlapped[w], "alcance: desplazamiento máx. de cajas a menos de 8 m"));
                else if (castConeLength[w] >= 0f)
                    Emit(new LabMeasurement(LabMeasurementKind.CastReach, castExperiment[w], castConeLength[w], UnitMeters,
                        castOverlapped[w], "alcance: largo del cono (sin cajas cerca)"));
            }
        }

        // ---------------------------------------------------------------- reinicio

        private void OnWorldReset()
        {
            // El reinicio corta todo lo abierto sin informarlo (las cajas vuelven a su lugar: no es un resultado).
            // Se liberan los slots: en la proxima muestra se toman de nuevo con la posicion ya restaurada.
            for (int s = 0; s < MaxBoxSlots; s++)
            {
                ClearSlot(s);
                slotOverlapped[s] = false;
            }
            for (int w = 0; w < MaxCastWindows; w++)
            {
                castOpen[w] = false;
                castSource[w] = null;
                castDurationPending[w] = false;
            }
            for (int i = 0; i < castTracked.Length; i++) castTracked[i] = false;
            fireOpen = false;
            fireLastBurning = 0;
        }

        private static void Emit(in LabMeasurement m)
        {
            Measured?.Invoke(m);
        }
    }
}
