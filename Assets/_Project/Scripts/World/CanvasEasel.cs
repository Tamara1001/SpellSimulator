using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Interfaces;

namespace SpellSimulator.World
{
    /// <summary>
    /// Atril con un slot para un lienzo o una libreta (Docs/LIENZOS_PLAN.md): al soltar (E) o dejar flotando (G) un lienzo cerca,
    /// se acopla al slot en la pose del atril; si ya habia otro, se cambian (el anterior sale al costado). Agarrar el lienzo
    /// lo libera. Apuntando a la base y con clic el atril gira <see cref="CanvasRules.EaselTurnStep"/>° sobre su eje y el lienzo
    /// lo sigue: asi se elige hacia donde mira (y por lo tanto hacia donde dispara).
    /// </summary>
    public class CanvasEasel : MonoBehaviour, IPressable
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

        public bool PressWithClick => true;

        /// <summary>Lo llama el generador de escena (Editor) despues de AddComponent.</summary>
        public void Configure(Transform slot, PhysicalCanvas startCanvas)
        {
            this.slot = slot;
            this.startCanvas = startCanvas;
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
            occupant.Dock(slot.position, Quaternion.LookRotation(-slot.forward, slot.up), this);
        }

        /// <summary>El lienzo salio del slot (lo agarraron o lo soltaron aparte).</summary>
        public void Release(PhysicalCanvas canvas)
        {
            if (occupant == canvas) occupant = null;
        }

        public string GetPressPrompt() => "Girar el atril";

        public void Press()
        {
            transform.Rotate(0f, CanvasRules.EaselTurnStep, 0f, Space.World);
            Redock();
        }
    }
}
