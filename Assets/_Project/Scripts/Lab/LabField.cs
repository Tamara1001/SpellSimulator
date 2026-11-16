using System;
using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Net;
using SpellSimulator.Player;

namespace SpellSimulator.Lab
{
    /// <summary>Variable que editan los controles del objeto elegido del campo (Laboratorio v4). Los valores no se renumeran.</summary>
    public enum LabFieldVar { X = 0, Distance = 1, Yaw = 2, Height = 3, Tilt = 4, Size = 5, Preset = 6 }

    /// <summary>
    /// Objetos del campo del Laboratorio v4 (punto 4 y 7): el papel de prueba (siempre, casilla 0) y hasta
    /// <see cref="LabSpawner.MaxBoxCount"/> cajas (casillas 1..4). Uno esta «elegido»: lo marca un aro luminoso en el piso y los
    /// controles del panel de objetos lo editan (<see cref="Get"/>/<see cref="Set"/>). El papel se edita a traves del banco
    /// (variables del grupo 5: quedan en el registro y en el resumen); las cajas se mueven directo (X, distancia por la pista y
    /// giro), se apoyan en el piso y se frenan. La masa de la mesa se aplica a la caja elegida (y al elegir una caja la mesa
    /// toma su masa). En red no hace nada. Sin allocations por frame: el aro se mueve en LateUpdate solo si cambio de lugar.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class LabField : MonoBehaviour
    {
        /// <summary>Casillas de la lista: papel + cajas.</summary>
        public const int SlotCount = 1 + LabSpawner.MaxBoxCount;
        /// <summary>Casilla del papel de prueba.</summary>
        public const int PaperSlot = 0;
        /// <summary>Texto de un control que no aplica al objeto elegido (p. ej. altura de una caja).</summary>
        public const string NotApplicable = "—";
        /// <summary>Lado de la caja (m), igual que LabSpawner.</summary>
        private const float BoxHalf = 0.3f;
        private const string NoticeId = "lab";

        /// <summary>Campo activo (null si no hay laboratorio).</summary>
        public static LabField Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = null;

        [SerializeField, Tooltip("Banco de sellos (papel de prueba y marco de la pista).")]
        private LabSealBench bench;
        [SerializeField, Tooltip("Materializador (cajas).")]
        private LabSpawner spawner;
        [SerializeField, Tooltip("Aro luminoso que marca el objeto elegido (se apoya en el piso).")]
        private Transform selectionRing;

        /// <summary>Cambio el objeto elegido o la lista (los controles rearman sus lecturas).</summary>
        public event Action SelectionChanged;

        private readonly List<GameObject> boxes = new List<GameObject>(LabSpawner.MaxBoxCount + 1);
        private int selected;
        private LabParameters parameters;
        private bool subscribed;
        private Vector3 shownRing = new Vector3(float.NaN, 0f, 0f);
        private bool syncingMass;

        /// <summary>Casilla elegida (0 papel, 1..4 cajas).</summary>
        public int Selected => selected;
        /// <summary>True si la casilla tiene objeto (el papel siempre).</summary>
        public bool HasObject(int slot) => slot == PaperSlot || (slot >= 1 && slot <= boxes.Count && boxes[slot - 1] != null);
        /// <summary>Cantidad de cajas en el campo.</summary>
        public int BoxCount { get { Sync(); return boxes.Count; } }
        /// <summary>Caja de una casilla (1..4) o null.</summary>
        public GameObject BoxAt(int slot) => slot >= 1 && slot <= boxes.Count ? boxes[slot - 1] : null;
        /// <summary>True si el elegido es una caja.</summary>
        public bool BoxSelected => selected != PaperSlot && BoxAt(selected) != null;

        // ---------------------------------------------------------------- ciclo de vida

        private void Awake() => Current = this;

        private void OnEnable() => Subscribe();

        private void Start() => Subscribe();

        private void OnDisable()
        {
            if (!subscribed) return;
            if (spawner != null) spawner.Materialized -= OnMaterialized;
            if (parameters != null) parameters.ValueChanged -= OnParameterChanged;
            subscribed = false;
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        private void Subscribe()
        {
            if (subscribed) return;
            if (bench == null) bench = LabSealBench.Current;
            if (spawner == null) spawner = LabSpawner.Current;
            parameters = LabParameters.Current;
            if (spawner == null) return;
            spawner.Materialized += OnMaterialized;
            if (parameters != null) parameters.ValueChanged += OnParameterChanged;
            subscribed = true;
        }

        private void LateUpdate()
        {
            Sync();
            if (selectionRing == null) return;
            // Una caja que se retiro (tope, reinicio, Borrar) deja de estar elegida.
            if (selected != PaperSlot && BoxAt(selected) == null) Select(PaperSlot);
            Vector3 at;
            if (selected == PaperSlot)
            {
                if (bench == null || bench.PaperTransform == null) return;
                at = bench.PaperGroundWorld;
            }
            else
            {
                GameObject box = BoxAt(selected);
                if (box == null) return;
                Vector3 p = box.transform.position;
                at = new Vector3(p.x, p.y - BoxHalf, p.z);
            }
            if ((at - shownRing).sqrMagnitude < 1e-6f) return;
            shownRing = at;
            selectionRing.position = at + Vector3.up * 0.03f;
        }

        // ---------------------------------------------------------------- API

        /// <summary>Elige una casilla (vacia: no hace nada).</summary>
        public void Select(int slot)
        {
            Sync();
            if (!HasObject(slot)) return;
            if (slot == selected) return;
            selected = slot;
            shownRing.x = float.NaN;
            // La mesa toma la masa de la caja elegida (asi la perilla muestra la suya).
            GameObject box = BoxAt(slot);
            if (box != null && parameters != null && box.TryGetComponent(out Rigidbody rb))
            {
                syncingMass = true;
                try { parameters.Set(LabVariableId.Mass, rb.mass); }
                finally { syncingMass = false; }
            }
            SelectionChanged?.Invoke();
        }

        /// <summary>True si la variable aplica al objeto elegido (las cajas solo tienen X, distancia y giro).</summary>
        public bool Applies(LabFieldVar v) => selected == PaperSlot || v == LabFieldVar.X || v == LabFieldVar.Distance || v == LabFieldVar.Yaw;

        /// <summary>Valor actual de la variable del elegido. Lo que no aplica a una caja devuelve el valor del papel.</summary>
        public float Get(LabFieldVar v)
        {
            GameObject box = BoxSelected ? BoxAt(selected) : null;
            if (box != null && Applies(v) && bench != null && bench.TrackOrigin != null)
            {
                Transform track = bench.TrackOrigin;
                Vector3 local = track.InverseTransformPoint(box.transform.position);
                switch (v)
                {
                    case LabFieldVar.X: return local.x;
                    case LabFieldVar.Distance: return local.z - bench.TrackStart;
                    default:
                        Vector3 f = track.InverseTransformDirection(box.transform.forward);
                        return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                }
            }
            return bench != null ? bench.GetVar(SealVar(v)) : 0f;
        }

        /// <summary>Escribe la variable del elegido (recortada a la pista). En red o si no aplica, avisa y no cambia nada.</summary>
        public void Set(LabFieldVar v, float value)
        {
            if (NetSession.Exists || bench == null) return;
            if (!Applies(v))
            {
                Hud.Notify(NoticeId, "Laboratorio: eso solo cambia el papel de prueba", 2f);
                return;
            }
            GameObject box = BoxSelected ? BoxAt(selected) : null;
            if (box == null)
            {
                bench.SetVar(SealVar(v), value);
                return;
            }
            float x = Get(LabFieldVar.X), distance = Get(LabFieldVar.Distance), yaw = Get(LabFieldVar.Yaw);
            switch (v)
            {
                case LabFieldVar.X: x = value; break;
                case LabFieldVar.Distance: distance = value; break;
                default: yaw = value; break;
            }
            PlaceBox(box, x, distance, yaw);
        }

        /// <summary>Lectura de un valor del elegido («—» si no aplica).</summary>
        public string Format(LabFieldVar v, float value)
        {
            if (!Applies(v)) return NotApplicable;
            return bench != null ? bench.FormatVar(SealVar(v), value) : value.ToString("0.##", LabCsv.NumberFormat);
        }

        /// <summary>Borra el elegido (una caja). El papel no se borra: avisa.</summary>
        public void DeleteSelected()
        {
            if (NetSession.Exists) return;
            GameObject box = BoxSelected ? BoxAt(selected) : null;
            if (box == null)
            {
                Hud.Notify(NoticeId, "Laboratorio: el papel de prueba no se borra (elegí una caja)", 2.5f);
                return;
            }
            if (spawner != null) spawner.Remove(box);
            boxes.Remove(box);
            selected = PaperSlot;
            shownRing.x = float.NaN;
            SelectionChanged?.Invoke();
        }

        /// <summary>Borra todo lo materializado (cajas, viento y la marca del fuego); el papel queda.</summary>
        public void DeleteAll()
        {
            if (NetSession.Exists) return;
            if (spawner != null) spawner.ClearAll();
            boxes.Clear();
            selected = PaperSlot;
            shownRing.x = float.NaN;
            SelectionChanged?.Invoke();
        }

        /// <summary>Variable del banco que corresponde a una del campo (para el papel).</summary>
        public static LabSealVarId SealVar(LabFieldVar v)
        {
            switch (v)
            {
                case LabFieldVar.X: return LabSealVarId.PaperX;
                case LabFieldVar.Distance: return LabSealVarId.PaperDistance;
                case LabFieldVar.Yaw: return LabSealVarId.PaperYaw;
                case LabFieldVar.Height: return LabSealVarId.PaperHeight;
                case LabFieldVar.Tilt: return LabSealVarId.PaperTilt;
                case LabFieldVar.Size: return LabSealVarId.PaperSize;
                default: return LabSealVarId.PaperPose;
            }
        }

        // ---------------------------------------------------------------- interno

        private void OnMaterialized(LabSpawnKind kind, GameObject go)
        {
            if (kind != LabSpawnKind.Box || go == null) return;
            Sync();
            if (!boxes.Contains(go)) boxes.Add(go);
            // La caja nueva queda elegida (casilla = su lugar en la lista).
            selected = PaperSlot;
            Select(boxes.IndexOf(go) + 1);
        }

        private void OnParameterChanged(LabVariableId id, float previous, float next)
        {
            if (id != LabVariableId.Mass || syncingMass || spawner == null) return;
            GameObject box = BoxSelected ? BoxAt(selected) : null;
            if (box != null) spawner.ApplyMass(box, next);
        }

        /// <summary>Saca de la lista las cajas que el spawner ya retiro (tope, reinicio del mundo) manteniendo el orden.</summary>
        private void Sync()
        {
            if (spawner == null) return;
            IReadOnlyList<GameObject> alive = spawner.Boxes;
            bool changed = false;
            for (int i = boxes.Count - 1; i >= 0; i--)
            {
                bool found = false;
                for (int k = 0; k < alive.Count; k++) if (ReferenceEquals(alive[k], boxes[i])) { found = true; break; }
                if (found && boxes[i] != null) continue;
                boxes.RemoveAt(i);
                changed = true;
            }
            if (!changed) return;
            if (selected > boxes.Count) selected = PaperSlot;
            shownRing.x = float.NaN;
            SelectionChanged?.Invoke();
        }

        /// <summary>Lleva una caja a (X, distancia) de la pista con su giro, apoyada en el piso y quieta.</summary>
        private void PlaceBox(GameObject box, float x, float distance, float yaw)
        {
            Transform track = bench.TrackOrigin;
            if (track == null) return;
            x = Mathf.Clamp(x, -LabSealSpec.MaxPaperX, LabSealSpec.MaxPaperX);
            distance = Mathf.Clamp(distance, LabSealSpec.MinPaperDistance, LabSealSpec.MaxPaperDistance);
            Vector3 world = track.TransformPoint(new Vector3(x, 0f, bench.TrackStart + distance));
            float ground = GroundY(world, box.transform);
            Vector3 position = new Vector3(world.x, ground + BoxHalf + 0.01f, world.z);
            Quaternion rotation = track.rotation * Quaternion.Euler(0f, yaw, 0f);
            if (box.TryGetComponent(out Rigidbody rb))
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = position;
                rb.rotation = rotation;
            }
            box.transform.SetPositionAndRotation(position, rotation);
            shownRing.x = float.NaN;
        }

        private static readonly RaycastHit[] Hits = new RaycastHit[8];

        /// <summary>Altura del piso bajo un punto, sin contar la propia caja, otras cajas ni triggers.</summary>
        private float GroundY(Vector3 point, Transform self)
        {
            int n = Physics.RaycastNonAlloc(point + Vector3.up * 20f, Vector3.down, Hits, 40f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue, y = point.y;
            for (int i = 0; i < n; i++)
            {
                Transform h = Hits[i].collider.transform;
                if (h.IsChildOf(self) || (spawner != null && spawner.Owns(h))) continue;
                Rigidbody body = Hits[i].collider.attachedRigidbody;
                if (body != null && !body.isKinematic) continue;
                if (Hits[i].distance < best) { best = Hits[i].distance; y = Hits[i].point.y; }
            }
            return y;
        }
    }
}
