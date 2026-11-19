using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Canvas;

namespace SpellSimulator.World
{
    /// <summary>
    /// Atril con un slot para un lienzo o una libreta (Docs/LIENZOS_PLAN.md): al soltar (E) o dejar flotando (G) un lienzo cerca,
    /// se acopla al slot en la pose del atril; si ya habia otro, se cambian (el anterior sale al costado). Agarrar el lienzo
    /// lo libera. El atril es un objeto fisico pesado (Movable): se empuja, se agarra y se cambia de lugar; el lienzo lo sigue.
    /// Un boton con flechas circulares en la base (<see cref="EaselReverseButton"/>) invierte el SENTIDO de disparo del atril: afecta a
    /// cualquier lienzo que tenga puesto y se apaga al sacarlo (el lienzo vuelve a disparar por la cara donde se escribe).
    /// </summary>
    public class CanvasEasel : MonoBehaviour
    {
        private const int MaxEasels = 32;

        [SerializeField, Tooltip("Pose del lienzo: forward = hacia donde mira la cara visible, up = borde de arriba.")]
        private Transform slot;

        private static readonly List<CanvasEasel> All = new List<CanvasEasel>();
        private static readonly Vector3[] SlotPositions = new Vector3[MaxEasels];
        private static readonly bool[] Occupied = new bool[MaxEasels];

        [SerializeField, Tooltip("Lienzo que arranca en el slot (y vuelve a el con el reinicio del mundo).")]
        private PhysicalCanvas startCanvas;

        private PhysicalCanvas occupant;

        public PhysicalCanvas Occupant => occupant != null && occupant.Easel == this ? occupant : null;
        public Transform Slot => slot;

        private bool reversed;
        private Vector3 lastSlotPosition;
        private Quaternion lastSlotRotation = Quaternion.identity;

        /// <summary>El atril dispara hacia el lado contrario al de escritura.</summary>
        public bool Reversed => reversed;

        /// <summary>Lo llama el generador de escena (Editor) despues de AddComponent.</summary>
        public void Configure(Transform slot, PhysicalCanvas startCanvas)
        {
            this.slot = slot;
            this.startCanvas = startCanvas;
        }

        /// <summary>Invierte (o no) el sentido de disparo y lo aplica al lienzo puesto.</summary>
        public void SetReversed(bool value)
        {
            reversed = value;
            ApplyReverse();
        }

        public void ToggleReversed() => SetReversed(!reversed);

        private void ApplyReverse()
        {
            PhysicalCanvas c = Occupant;
            if (c != null && c.TryGetComponent(out DrawingCanvas drawing)) drawing.SetReversedByEasel(reversed);
        }

        /// <summary>Si el atril se movio (empujado, agarrado, caido) el lienzo lo sigue en el mismo frame.</summary>
        private void LateUpdate()
        {
            if (slot == null || occupant == null || occupant.Easel != this) return;
            if ((slot.position - lastSlotPosition).sqrMagnitude < 1e-8f && Quaternion.Angle(slot.rotation, lastSlotRotation) < 0.01f) return;
            lastSlotPosition = slot.position;
            lastSlotRotation = slot.rotation;
            occupant.FollowEasel(slot.position, Quaternion.LookRotation(-slot.forward, slot.up));
        }

        private void Start()
        {
            if (startCanvas != null)
            {
                startCanvas.Home = this;
                Place(startCanvas);
            }
        }

        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() => All.Remove(this);

        /// <summary>Acopla <paramref name="canvas"/> al slot mas cercano si esta a menos de <see cref="CanvasRules.SlotSnapDistance"/>.</summary>
        public static bool TrySnap(PhysicalCanvas canvas)
        {
            if (canvas == null || All.Count == 0) return false;
            Vector3 center = canvas.WorldCenter;
            int n = Mathf.Min(All.Count, MaxEasels);
            for (int i = 0; i < n; i++)
            {
                CanvasEasel e = All[i];
                SlotPositions[i] = e.slot != null ? e.slot.position : e.transform.position;
                Occupied[i] = e.Occupant != null && e.Occupant != canvas;
            }
            int index = CanvasRules.NearestSlot(center, SlotPositions, Occupied, n, true);
            if (index < 0) return false;
            All[index].Place(canvas);
            return true;
        }

        /// <summary>Pone <paramref name="canvas"/> en el slot (el que estaba sale al costado).</summary>
        public void Place(PhysicalCanvas canvas)
        {
            if (slot == null) return;
            PhysicalCanvas previous = Occupant;
            if (previous != null && previous != canvas) previous.Eject(slot.position + slot.right * CanvasRules.SlotEjectOffset);
            occupant = canvas;
            Redock();
        }

        private void Redock()
        {
            if (occupant == null || slot == null) return;
            lastSlotPosition = slot.position;
            lastSlotRotation = slot.rotation;
            occupant.Dock(slot.position, Quaternion.LookRotation(-slot.forward, slot.up), this);
            ApplyReverse();
        }

        /// <summary>El lienzo salio del slot (lo agarraron o lo soltaron aparte).</summary>
        public void Release(PhysicalCanvas canvas)
        {
            if (occupant != canvas) return;
            if (canvas != null && canvas.TryGetComponent(out DrawingCanvas drawing)) drawing.SetReversedByEasel(false); // fuera del atril vuelve a lo normal
            occupant = null;
        }
    }
}
