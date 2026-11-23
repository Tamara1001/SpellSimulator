using UnityEngine;
using SpellSimulator.Canvas;

namespace SpellSimulator.World
{
    /// <summary>
    /// Pila del almacen de lienzos: muestra flotando un lienzo o libreta real (<see cref="CanvasSpec"/>) sobre su peana. Cuando el jugador lo
    /// agarra (o deja de flotar) pasa a ser suyo y la pila repone otro igual. Sin allocations en Update (solo comparaciones).
    /// </summary>
    public class CanvasPile : MonoBehaviour
    {
        [SerializeField] private CanvasRestocker restocker;
        [SerializeField] private Transform anchor;
        [SerializeField] private CanvasKind kind;
        [SerializeField] private CanvasSize size;
        [SerializeField] private CanvasShape shape;
        [SerializeField, Tooltip("Segundos de enfriamiento antes de reponer el lienzo tomado.")]
        private float refillDelay = 1.0f;

        private PhysicalCanvas top;
        private float refillAt;

        public float RefillDelay { get => refillDelay; set => refillDelay = value; }

        public void Configure(CanvasRestocker restocker, Transform anchor, CanvasSpec spec)
        {
            this.restocker = restocker;
            this.anchor = anchor;
            kind = spec.kind;
            size = spec.size;
            shape = spec.shape;
        }

        public CanvasSpec Spec => CanvasRules.Sanitize(new CanvasSpec(kind, size, shape, kind == CanvasKind.Notebook ? CanvasRules.MaxPages : 1));

        private void Start() => Refill();

        private void Update()
        {
            if (top != null)
            {
                if (top.IsFloating) return;
                // Se lo llevaron (agarrado, arrojado o caido): ahora cuenta como sacado y se repone tras el enfriamiento.
                if (restocker != null) restocker.Release(top.gameObject);
                top = null;
                refillAt = Time.time + refillDelay;
            }

            if (Time.time >= refillAt) Refill();
        }

        private void Refill()
        {
            if (restocker == null || anchor == null) return;
            GameObject go = restocker.SpawnDisplay(Spec, anchor.position, anchor.rotation);
            if (go != null)
            {
                top = go.GetComponent<PhysicalCanvas>();
            }
            if (top == null)
            {
                refillAt = Time.time + refillDelay;
            }
        }
    }
}
