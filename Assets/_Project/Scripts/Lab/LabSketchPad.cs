using System;
using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Core;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Logica pura de la vista previa del atril (Laboratorio v5, punto A.2): los trazos que escribe el banco llevan el dueño
    /// <see cref="PreviewOwner"/>; cualquier otro trazo es del jugador (lapicera de siempre, en red el dueño local).
    /// </summary>
    public static class LabSketchPreview
    {
        /// <summary>Dueño de los trazos de la vista previa (negativo: ningun jugador lo usa; AddStroke no lo pisa porque no es 0).</summary>
        public const int PreviewOwner = -7105;

        private static readonly Predicate<Stroke> IsPreviewStroke = s => s == null || s.OwnerId == PreviewOwner;

        /// <summary>True si el trazo es de la vista previa.</summary>
        public static bool IsPreview(Stroke stroke) => IsPreviewStroke(stroke);

        /// <summary>True si hay al menos un trazo que no es de la vista previa (el jugador empezo a dibujar).</summary>
        public static bool HasHandStroke(List<Stroke> strokes)
        {
            if (strokes == null) return false;
            for (int i = 0; i < strokes.Count; i++)
                if (!IsPreviewStroke(strokes[i])) return true;
            return false;
        }

        /// <summary>Saca los trazos de la vista previa (sin alocar). Devuelve cuantos saco.</summary>
        public static int RemovePreview(List<Stroke> strokes) => strokes == null ? 0 : strokes.RemoveAll(IsPreviewStroke);
    }

    /// <summary>
    /// Atril de dibujo del banco (Laboratorio v5, puntos A.1 y A.2): un <see cref="DrawingCanvas"/> normal del juego (hijo de este
    /// objeto). Se dibuja con la lapicera equipada de siempre (PhysicalTool: mismo input, trazo, estabilizador, deshacer y goma) y
    /// Tab abre su vista completa como en cualquier lienzo (PlayerInteractor → SurfaceFocus.ShowCanvas). Aca solo queda:
    /// - la compuerta <see cref="ISealGate"/> siempre bloqueada: SpellCaster no lo lanza solo al cerrar el anillo;
    /// - la vista previa: el banco dibuja el sello de los controles (<see cref="ShowPreview"/>); el primer trazo del jugador la
    ///   limpia (<see cref="HandDrawn"/>) y «Borrar dibujo» (<see cref="Clear"/>) vuelve a la vista previa;
    /// - el aviso <see cref="Changed"/> para el banco (copiar al papel del campo) y el cartel.
    /// Sin allocations por frame: LateUpdate solo mira la cantidad de trazos y el dueño del ultimo.
    /// </summary>
    public class LabSketchPad : MonoBehaviour, ISealGate
    {
        [SerializeField, Tooltip("Lienzo del atril (DrawingCanvas hijo, sin PhysicalCanvas, con collider solido).")]
        private DrawingCanvas canvas;
        [SerializeField, Tooltip("Lanzador de la escena (avisa cuando termina un trazo, se borra o se deshace). Vacio = el de la escena.")]
        private SpellCaster caster;

        /// <summary>El dibujo del jugador cambio (empezo, termino un trazo, se borro o volvio a la vista previa).</summary>
        public event Action Changed;

        private bool subscribed;
        private int seenCount;

        /// <summary>Lienzo del atril.</summary>
        public DrawingCanvas Canvas => canvas;
        /// <summary>True si el jugador dibujo a mano (la vista previa ya no se muestra hasta «Borrar dibujo»).</summary>
        public bool HandDrawn { get; private set; }
        /// <summary>
        /// True si hay un dibujo a mano con trazos (LANZAR lo usa en vez del sello de los controles). Revisa antes si aparecio un
        /// trazo del jugador en este mismo frame (LateUpdate todavia no corrio). No se consulta por frame.
        /// </summary>
        public bool HasDrawing
        {
            get
            {
                DetectHandStroke();
                return HandDrawn && canvas != null && !canvas.Session.IsEmpty;
            }
        }
        /// <summary>Siempre bloqueado: el atril nunca se lanza solo.</summary>
        public bool SealBlocked => true;

        /// <summary>Lo llama el generador de escena (Editor) despues de AddComponent.</summary>
        public void Configure(DrawingCanvas canvas, SpellCaster caster)
        {
            this.canvas = canvas;
            this.caster = caster;
        }

        private void Awake()
        {
            if (canvas == null) canvas = GetComponentInChildren<DrawingCanvas>(true);
        }

        private void OnEnable() => Subscribe();

        private void Start()
        {
            if (caster == null) caster = FindFirstObjectByType<SpellCaster>();
            Subscribe();
        }

        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (subscribed || caster == null) return;
            caster.CanvasEdited += OnCanvasEdited;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (caster != null) caster.CanvasEdited -= OnCanvasEdited;
            subscribed = false;
        }

        private void LateUpdate()
        {
            if (canvas == null) return;
            // Solo cuando cambia la cantidad de trazos (un trazo nuevo de la lapicera o la goma): no recorre la lista por frame.
            int count = canvas.Session.Strokes.Count;
            if (count == seenCount) return;
            seenCount = count;
            DetectHandStroke();
        }

        /// <summary>
        /// Vista previa del sello de los controles: reemplaza lo que haya (si no hay dibujo a mano) por estos trazos, en el
        /// espacio cuadrado del sello [0,1]² (LabSealSynth). Devuelve false si el jugador esta dibujando a mano (no se toca).
        /// </summary>
        public bool ShowPreview(List<List<Vector2>> squareStrokes, float widthMeters, InkKind ink)
        {
            if (canvas == null) return false;
            DetectHandStroke();
            if (HandDrawn) return false;
            DrawingSession session = canvas.Session;
            session.Clear();
            if (squareStrokes != null)
            {
                Vector2 iso = canvas.UvToIsotropic;
                for (int k = 0; k < squareStrokes.Count; k++)
                {
                    List<Vector2> source = squareStrokes[k];
                    if (source == null || source.Count == 0) continue;
                    var points = new List<Vector2>(source.Count);
                    for (int i = 0; i < source.Count; i++) points.Add(LabSealBenchMath.SquareToCanvasUv(source[i], iso));
                    session.AddStroke(new Stroke(points) { Width = widthMeters, Ink = ink, OwnerId = LabSketchPreview.PreviewOwner });
                }
            }
            seenCount = session.Strokes.Count;
            canvas.Redraw();
            return true;
        }

        /// <summary>«Borrar dibujo»: borra el dibujo a mano y vuelve a la vista previa de los controles.</summary>
        public void Clear()
        {
            if (canvas == null) return;
            HandDrawn = false;
            canvas.Clear();
            seenCount = 0;
            // La fase del sello vuelve a «dibujando» (sin lanzar: compuerta). CanvasEdited avisa Changed y el banco repone la
            // vista previa; sin lanzador se avisa directo.
            if (subscribed) caster.OnSealEdited(canvas);
            else Changed?.Invoke();
        }

        /// <summary>
        /// Si aparecio un trazo que no es de la vista previa, el jugador empezo a dibujar: se saca la vista previa (el trazo en
        /// curso sigue, PenTool guarda su referencia) y se repinta.
        /// </summary>
        private void DetectHandStroke()
        {
            if (HandDrawn || canvas == null) return;
            List<Stroke> strokes = canvas.Session.Strokes;
            if (!LabSketchPreview.HasHandStroke(strokes)) return;
            if (LabSketchPreview.RemovePreview(strokes) > 0) canvas.Session.MarkChanged();
            seenCount = strokes.Count;
            canvas.Redraw();
            HandDrawn = true;
            Changed?.Invoke();
        }

        /// <summary>Termino un trazo, se borro o se deshizo algo en el atril.</summary>
        private void OnCanvasEdited(DrawingCanvas edited)
        {
            if (edited != canvas) return;
            DetectHandStroke();
            // Con la goma o deshacer se fue todo lo dibujado a mano: vuelve la vista previa.
            if (HandDrawn && canvas.Session.IsEmpty) HandDrawn = false;
            Changed?.Invoke();
        }
    }
}
