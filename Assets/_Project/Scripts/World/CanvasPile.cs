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

        private PhysicalCanvas top;

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
            if (top == null) { Refill(); return; }
            if (top.IsFloating) return;
            // Se lo llevaron (agarrado, arrojado o caido): ahora cuenta como sacado y se repone.
            restocker.Release(top.gameObject);
            top = null;
            Refill();
        }

        private void Refill()
        {
            if (restocker == null || anchor == null) return;
            GameObject go = restocker.SpawnDisplay(Spec, anchor.position, anchor.rotation);
            if (go != null) top = go.GetComponent<PhysicalCanvas>();
        }
    }
}
