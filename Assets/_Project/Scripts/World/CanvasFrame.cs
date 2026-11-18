using UnityEngine;
using SpellSimulator.Canvas;

namespace SpellSimulator.World
{
    /// <summary>
    /// Marco de un lienzo que dice por que cara sale el hechizo (Docs/LIENZOS_PLAN.md, «cara de disparo»): dorado = por la cara
    /// donde se dibuja, azul = por la de atras, con una flecha hacia donde sale. La cara es <see cref="DrawingCanvas.InkSign"/>;
    /// el boton del marco (<see cref="CanvasFaceButton"/>) la invierte sin mover el lienzo. Los dos marcos los arma el editor
    /// (MapBuilder) y aca solo se prende uno: cero allocations.
    /// </summary>
    public class CanvasFrame : MonoBehaviour
    {
        [SerializeField] private DrawingCanvas canvas;
        [SerializeField, Tooltip("Marco dorado (sale por la cara de dibujo).")] private GameObject frontFrame;
        [SerializeField, Tooltip("Marco azul (sale por la cara de atras).")] private GameObject backFrame;

        private float shownSign;

        public DrawingCanvas Canvas => canvas;

        /// <summary>Cara de disparo actual.</summary>
        public FiringFace Face => canvas != null ? CanvasRules.FaceOf(canvas.InkSign) : FiringFace.Front;

        /// <summary>Lo llama el generador de escena (Editor) despues de AddComponent.</summary>
        public void Configure(DrawingCanvas canvas, GameObject front, GameObject back)
        {
            this.canvas = canvas;
            frontFrame = front;
            backFrame = back;
        }

        private void Awake()
        {
            if (canvas == null) canvas = GetComponentInParent<DrawingCanvas>();
            shownSign = 0f;
            Refresh();
        }

        /// <summary>Invierte la cara de disparo.</summary>
        public void Toggle() => Set(CanvasRules.Flip(Face));

        public void Set(FiringFace face)
        {
            if (canvas == null) return;
            canvas.SetInkSign(CanvasRules.InkSign(face));
            Refresh();
        }

        // La cara tambien cambia sola (NoteInkViewer: la cara desde la que se empieza a dibujar): se vuelve a leer cada frame.
        private void Update()
        {
            if (canvas != null && canvas.InkSign != shownSign) Refresh();
        }

        private void Refresh()
        {
            if (canvas == null) return;
            shownSign = canvas.InkSign;
            bool front = shownSign >= 0f;
            if (frontFrame != null && frontFrame.activeSelf != front) frontFrame.SetActive(front);
            if (backFrame != null && backFrame.activeSelf == front) backFrame.SetActive(!front);
        }
    }
}
