using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.Learning
{
    /// <summary>
    /// Convierte lo dibujado en un lienzo (trazos en UV) a los trazos de un hechizo guardado: unidades de anillo (centro 0,0, radio 1, Y arriba), sin el anillo
    /// cerrado del dibujo; en su lugar va el anillo casi cerrado de siempre (<see cref="SpellStrokeBuilder.Ring"/>) para que el jugador lo cierre a mano al
    /// usar el sello. Logica pura.
    /// </summary>
    public static class SpellRecording
    {
        /// <summary>Un trazo es el anillo si todo cae cerca del radio 1 y da (casi) toda la vuelta.</summary>
        public const float RingBand = 0.2f, RingMinCoverageDeg = 250f;
        public const int MaxStrokes = 64, MaxPointsPerStroke = 400;

        public static List<List<Vector2>> ToRingUnits(IList<List<Vector2>> strokesUV, Vector2 ringCenterUV, Vector2 worldSize, float radiusMeters, float gapDeg)
        {
            if (strokesUV == null || radiusMeters <= 1e-4f) return null;
            var result = new List<List<Vector2>>(strokesUV.Count + 1);
            for (int s = 0; s < strokesUV.Count && result.Count < MaxStrokes; s++)
            {
                List<Vector2> src = strokesUV[s];
                if (src == null || src.Count < 2) continue;
                int step = Mathf.Max(1, Mathf.CeilToInt(src.Count / (float)MaxPointsPerStroke));
                var stroke = new List<Vector2>(src.Count / step + 1);
                for (int i = 0; i < src.Count; i += step)
                {
                    Vector2 d = src[i] - ringCenterUV;
                    stroke.Add(new Vector2(d.x * worldSize.x, d.y * worldSize.y) / radiusMeters);
                }
                if (IsRingLike(stroke)) continue;
                result.Add(stroke);
            }
            if (result.Count == 0) return null;
            result.Add(SpellStrokeBuilder.Ring(gapDeg));
            return result;
        }

        /// <summary>True si el trazo (en unidades de anillo) es el anillo del sello.</summary>
        public static bool IsRingLike(IList<Vector2> stroke)
        {
            if (stroke == null || stroke.Count < 8) return false;
            var angles = new List<float>(stroke.Count);
            for (int i = 0; i < stroke.Count; i++)
            {
                float r = stroke[i].magnitude;
                if (Mathf.Abs(r - 1f) > RingBand) return false;
                angles.Add(Mathf.Atan2(stroke[i].y, stroke[i].x) * Mathf.Rad2Deg);
            }
            angles.Sort();
            float maxGap = 360f - (angles[angles.Count - 1] - angles[0]); // el hueco que cierra la vuelta
            for (int i = 1; i < angles.Count; i++) maxGap = Mathf.Max(maxGap, angles[i] - angles[i - 1]);
            return 360f - maxGap >= RingMinCoverageDeg;
        }

        /// <summary>Elemento del SpellIR (enum) como texto del catalogo: fire | water | wind | earth | light.</summary>
        public static string ElementId(Compiler.ElementType element)
        {
            switch (element)
            {
                case Compiler.ElementType.Fire: return "fire";
                case Compiler.ElementType.Water: return "water";
                case Compiler.ElementType.Wind: return "wind";
                case Compiler.ElementType.Earth: return "earth";
                case Compiler.ElementType.Light: return "light";
                default: return "";
            }
        }
    }
}
