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

        public bool IsEmpty => Strokes.Count == 0;

        public void AddStroke(Stroke stroke)
        {
            Strokes.Add(stroke);
        }

        public bool UndoLastStroke()
        {
            if (Strokes.Count == 0) return false;
            Strokes.RemoveAt(Strokes.Count - 1);
            return true;
        }

        public void Clear()
        {
            Strokes.Clear();
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
            var copy = new DrawingSession();
            foreach (var stroke in Strokes)
            {
                var points = new List<Vector2>(stroke.Points.Count);
                foreach (var p in stroke.Points) points.Add(Vector2.Scale(p, scale));
                copy.AddStroke(new Stroke(points) { Width = stroke.Width, Ink = stroke.Ink });
            }
            return copy;
        }
    }
}
