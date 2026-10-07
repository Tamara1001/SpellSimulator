using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Core;

namespace SpellSimulator.Tests
{
    /// <summary>
    /// Fabricas de trazos sinteticos (en coordenadas UV) para los tests.
    /// </summary>
    public static class TestStrokes
    {
        public static Stroke Circle(Vector2 center, float radius, int points = 48, float closureGapDegrees = 0f)
        {
            var stroke = new Stroke();
            float sweep = 360f - closureGapDegrees;
            for (int i = 0; i <= points; i++)
            {
                float angle = sweep * i / points * Mathf.Deg2Rad;
                stroke.AddPoint(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
            return stroke;
        }

        public static Stroke Line(Vector2 from, Vector2 to, int points = 12)
        {
            var stroke = new Stroke();
            for (int i = 0; i <= points; i++)
                stroke.AddPoint(Vector2.Lerp(from, to, (float)i / points));
            return stroke;
        }

        public static DrawingSession Session(params Stroke[] strokes)
        {
            var session = new DrawingSession();
            foreach (var s in strokes) session.AddStroke(s);
            return session;
        }

        public static int CountInk(IReadOnlyList<float> tensor)
        {
            int count = 0;
            foreach (float v in tensor) if (v > 0f) count++;
            return count;
        }
    }
}
