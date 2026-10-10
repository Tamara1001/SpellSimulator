using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Compiler;

namespace SpellSimulator.Casting
{
    /// <summary>
    /// Copia de lo dibujado en un sello al lanzarlo (los trazos pueden borrarse despues): trazos en UV, centro del anillo y tamano del lienzo en metros.
    /// Se arma una vez por lanzamiento (no por cuadro). El grimorio la convierte en un hechizo propio (<c>SpellRecording</c>).
    /// </summary>
    public sealed class CastSnapshot
    {
        public SpellIR Spell;
        public Vector2 RingCenterUV;
        public Vector2 WorldSize;
        public readonly List<List<Vector2>> Strokes = new List<List<Vector2>>();

        public static CastSnapshot Capture(DrawingCanvas canvas, SpellIR spell, Vector2 ringCenterUV)
        {
            if (canvas == null || canvas.Session.IsEmpty) return null;
            var snap = new CastSnapshot { Spell = spell, RingCenterUV = ringCenterUV, WorldSize = canvas.WorldSize };
            foreach (var stroke in canvas.Session.Strokes) snap.Strokes.Add(new List<Vector2>(stroke.Points));
            return snap;
        }
    }
}
