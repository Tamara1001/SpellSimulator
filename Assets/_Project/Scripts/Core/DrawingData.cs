using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.Core
{
    /// <summary>
    /// Un trazo: puntos en coordenadas del lienzo entre pointer-down y pointer-up.
    /// </summary>
    [Serializable]
    public class Stroke
    {
        public List<Vector2> Points = new List<Vector2>();
        /// <summary>Ancho de la tinta en metros (0 = el del lienzo). Solo visual: el reconocimiento usa los puntos.</summary>
        public float Width;
        /// <summary>Clase de tinta con que se dibujo (decide potencia y resistencia al agua del hechizo).</summary>
        public InkKind Ink;
        /// <summary>Jugador que dibujo el trazo (0 = jugador local / solitario). Lo usa la sincronizacion de red.</summary>
        public int OwnerId;
        /// <summary>Identificador del trazo dentro de su lienzo y dueno (creciente; 0 = aun sin asignar). Lo asigna <see cref="DrawingSession.AddStroke"/>.</summary>
        public uint StrokeId;

        public Stroke() { }

        public Stroke(List<Vector2> points)
        {
            Points = points;
        }

        public int Count => Points.Count;

        public void AddPoint(Vector2 point)
        {
            Points.Add(point);
        }
    }

    /// <summary>
    /// Conjunto de trazos de un lienzo. Cada DrawingCanvas es duenio de la suya.
    /// </summary>
    [Serializable]
    public class DrawingSession
    {
        public List<Stroke> Strokes = new List<Stroke>();

        [SerializeField] private uint lastStrokeId;
        [SerializeField] private int revision;
        [SerializeField] private int ownerForNewStrokes;

        public bool IsEmpty => Strokes.Count == 0;

        /// <summary>Proximo <see cref="Stroke.StrokeId"/> que se va a asignar (empieza en 1).</summary>
        public uint NextStrokeId => lastStrokeId + 1;

        /// <summary>Contador que sube con cada cambio (trazo nuevo, borrado, deshacer, limpiar). Sirve para sincronizar.</summary>
        public int Revision => revision;

        /// <summary>Duenio que se le pone a los trazos nuevos que no traen uno (0 = jugador local).</summary>
        public int OwnerForNewStrokes => ownerForNewStrokes;

        public void SetOwnerForNewStrokes(int ownerId) => ownerForNewStrokes = ownerId;

        /// <summary>Marca un cambio hecho directamente sobre <see cref="Strokes"/> (quitar o insertar sin pasar por la API).</summary>
        public void MarkChanged() => revision++;

        /// <summary>
        /// Agrega un trazo. Si no tiene id se le asigna el siguiente; si ya lo tiene (copias, red) se respeta. Un trazo sin duenio
        /// recibe <see cref="OwnerForNewStrokes"/>.
        /// </summary>
        public void AddStroke(Stroke stroke)
        {
            if (stroke.StrokeId == 0) stroke.StrokeId = ++lastStrokeId;
            else if (stroke.StrokeId > lastStrokeId) lastStrokeId = stroke.StrokeId;
            if (stroke.OwnerId == 0) stroke.OwnerId = ownerForNewStrokes;
            Strokes.Add(stroke);
            revision++;
        }

        public bool UndoLastStroke()
        {
            if (Strokes.Count == 0) return false;
            Strokes.RemoveAt(Strokes.Count - 1);
            revision++;
            return true;
        }

        public void Clear()
        {
            if (Strokes.Count == 0) return;
            Strokes.Clear();
            revision++;
        }

        /// <summary>
        /// Limites de todos los puntos dibujados. Devuelve false si no hay puntos.
        /// </summary>
        public bool GetBounds(out Vector2 min, out Vector2 max)
        {
            return GeometryUtils.GetBounds(Strokes, out min, out max);
        }

        /// <summary>
        /// Copia profunda con cada punto escalado. Se usa para pasar de UV a un espacio
        /// isotropico cuando el lienzo no es cuadrado.
        /// </summary>
        public DrawingSession Scaled(Vector2 scale)
        {
            var copy = new DrawingSession { lastStrokeId = lastStrokeId, ownerForNewStrokes = ownerForNewStrokes };
            foreach (var stroke in Strokes)
            {
                var points = new List<Vector2>(stroke.Points.Count);
                foreach (var p in stroke.Points) points.Add(Vector2.Scale(p, scale));
                copy.AddStroke(new Stroke(points) { Width = stroke.Width, Ink = stroke.Ink, OwnerId = stroke.OwnerId, StrokeId = stroke.StrokeId });
            }
            return copy;
        }
    }
}
