using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.Core
{
    /// <summary>Calculos puros de gasto y clase de tinta.</summary>
    public static class InkMath
    {
        /// <summary>Lado (m) del lienzo de referencia: ahi el gasto por metro es el nominal.</summary>
        public const float ReferenceCanvasSide = 0.5f;
        /// <summary>Tope de la normalizacion: un lienzo enorme no hace la tinta infinita.</summary>
        public const float MaxCanvasScale = 8f;

        /// <summary>Cuantas veces el lienzo es mas grande que el de referencia (lado geometrico), entre 1 y MaxCanvasScale.</summary>
        public static float CanvasScale(Vector2 worldSize)
        {
            float side = Mathf.Sqrt(Mathf.Max(0f, worldSize.x) * Mathf.Max(0f, worldSize.y));
            return Mathf.Clamp(side / ReferenceCanvasSide, 1f, MaxCanvasScale);
        }

        /// <summary>Tinta que cuesta un trazo de <paramref name="meters"/> metros: normalizada al tamano del lienzo. Nunca negativa.</summary>
        public static float StrokeCost(float meters, Vector2 canvasWorldSize, float inkPerMeter, InkKind kind)
        {
            float cost = meters * inkPerMeter * InkKindInfo.CostMultiplier(kind) / CanvasScale(canvasWorldSize);
            return Mathf.Max(0f, cost);
        }

        /// <summary>Clase con mayor longitud total de trazo (en UV). Normal si no hay trazos o hay empate.</summary>
        public static InkKind Dominant(IReadOnlyList<Stroke> strokes)
        {
            if (strokes == null || strokes.Count == 0) return InkKind.Normal;
            float normal = 0f, powerful = 0f, resistant = 0f;
            for (int i = 0; i < strokes.Count; i++)
            {
                Stroke s = strokes[i];
                float len = 0f;
                for (int j = 1; j < s.Points.Count; j++) len += Vector2.Distance(s.Points[j - 1], s.Points[j]);
                switch (s.Ink)
                {
                    case InkKind.Powerful: powerful += len; break;
                    case InkKind.Resistant: resistant += len; break;
                    default: normal += len; break;
                }
            }
            float max = Mathf.Max(normal, Mathf.Max(powerful, resistant));
            int winners = (normal == max ? 1 : 0) + (powerful == max ? 1 : 0) + (resistant == max ? 1 : 0);
            if (winners != 1) return InkKind.Normal;
            return powerful == max ? InkKind.Powerful : resistant == max ? InkKind.Resistant : InkKind.Normal;
        }
    }
}
