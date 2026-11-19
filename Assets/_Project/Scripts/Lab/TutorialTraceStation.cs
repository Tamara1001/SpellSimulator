using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Core;
using SpellSimulator.Interfaces;
using SpellSimulator.World;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Mini tutorial de calco del piso 1 (sigilo de tierra, columna, circulo). Vive en el lienzo del pedestal junto a un
    /// <see cref="TutorialPaperGuide"/> (que dibuja la plantilla fantasma): cuando los trazos dibujados copian esa plantilla
    /// (<see cref="TutorialTraceRules"/>), completa el <see cref="TutorialExercise"/> de la estacion (farolito de «hecho»).
    /// Al reiniciar el tutorial (el ejercicio vuelve a «no hecho») borra el lienzo.
    /// </summary>
    [RequireComponent(typeof(DrawingCanvas))]
    public class TutorialTraceStation : MonoBehaviour, ISealGate
    {
        [SerializeField, Tooltip("Ejercicio (farolito) que se completa al calcar bien.")] private TutorialExercise exercise;

        private const float CheckPeriod = 0.25f;

        private DrawingCanvas canvas;
        private TutorialPaperGuide guide;
        private List<IReadOnlyList<Vector2>> template;
        private int checkedSignature = -1;
        private float timer;
        private bool matched;

        public void Configure(TutorialExercise target) => exercise = target;

        /// <summary>Estos papeles solo se comparan con la plantilla: el sello nunca se lanza (ni sale el cartel rojo de «hechizo invalido»).</summary>
        public bool SealBlocked => true;

        /// <summary>Ultimo error medio medido (para diagnostico; float.MaxValue si aun no se midio).</summary>
        public float LastError { get; private set; } = float.MaxValue;

        private void Awake()
        {
            canvas = GetComponent<DrawingCanvas>();
            guide = GetComponent<TutorialPaperGuide>();
        }

        private void Update()
        {
            if (canvas == null || exercise == null) return;
            // Reinicio del tutorial: el ejercicio se rearma y el papel se limpia.
            if (matched && !exercise.Done)
            {
                matched = false;
                checkedSignature = -1;
                canvas.Clear();
                return;
            }
            if (exercise.Done) return;
            timer += Time.deltaTime;
            if (timer < CheckPeriod) return;
            timer = 0f;
            // El trazo se agrega al empezar y sigue creciendo: la firma incluye los puntos para volver a medir cuando se termina.
            int signature = canvas.Session.Revision * 7919;
            foreach (Stroke stroke in canvas.Session.Strokes) signature += stroke.Points.Count;
            if (signature == checkedSignature) return;
            checkedSignature = signature;
            if (Check()) { matched = true; exercise.Complete(); }
        }

        /// <summary>¿Lo dibujado copia la plantilla? (publico para las pruebas del harness).</summary>
        public bool Check()
        {
            if (canvas == null || guide == null || canvas.Session.IsEmpty) return false;
            if (template == null)
            {
                List<List<Vector2>> strokes = TutorialPaperGuide.GlyphStrokes(guide.GlyphId, guide.GlyphSize, guide.WithRing);
                if (strokes == null) return false;
                template = new List<IReadOnlyList<Vector2>>(strokes.Count);
                foreach (var s in strokes) template.Add(s);
            }
            // Ambos en el espacio isotropico del lienzo (la plantilla se acomoda como la dibuja el fantasma).
            Vector2 iso = canvas.UvToIsotropic;
            var drawn = new List<IReadOnlyList<Vector2>>(canvas.Session.Strokes.Count);
            foreach (Stroke stroke in canvas.Session.Strokes)
            {
                var pts = new List<Vector2>(stroke.Points.Count);
                foreach (Vector2 uv in stroke.Points) pts.Add(new Vector2(uv.x * iso.x, uv.y * iso.y));
                drawn.Add(pts);
            }
            var want = new List<IReadOnlyList<Vector2>>(template.Count);
            foreach (var s in template)
            {
                var pts = new List<Vector2>(s.Count);
                foreach (Vector2 p in s)
                {
                    Vector2 uv = LabSealBenchMath.SquareToCanvasUv(p, iso);
                    pts.Add(new Vector2(uv.x * iso.x, uv.y * iso.y));
                }
                want.Add(pts);
            }
            bool ok = TutorialTraceRules.Matches(drawn, want, out float error);
            LastError = error;
            return ok;
        }
    }
}
