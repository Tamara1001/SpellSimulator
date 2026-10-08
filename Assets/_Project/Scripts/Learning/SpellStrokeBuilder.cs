using System;
using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Casting;

namespace SpellSimulator.Learning
{
    /// <summary>
    /// Arma los trazos de un hechizo en unidades de anillo (centro en 0,0; radio del anillo = 1; Y arriba).
    /// Mismas proporciones que las estampas y HarnessSeals: sigilo = 0.57 R, signos = 0.29 R puestos a 0.71 R del centro.
    /// El anillo va al final (casi cerrado: queda un hueco arriba) y se dibuja de ultimo, como en el canon.
    /// Logica pura.
    /// </summary>
    public static class SpellStrokeBuilder
    {
        /// <summary>Distancia del centro de un signo al centro del sello (en radios): 0.30 / 0.42 de HarnessSeals.</summary>
        public const float SignDistance = 0.714f;
        public const int RingSegments = 96;

        /// <summary>Trazos completos (sigilo, signos y anillo). templateOf devuelve la plantilla [0,1] de un glifo (o null).</summary>
        public static List<List<Vector2>> Build(SpellDef def, Func<string, List<List<Vector2>>> templateOf)
        {
            if (def.Strokes != null) return def.Strokes;
            var result = new List<List<Vector2>>();
            AddTemplate(result, templateOf(def.Sigil), Vector2.zero, StampPattern.SigilOfRadius, 0f);
            for (int i = 0; i < def.Signs.Count; i++)
            {
                SpellSign sign = def.Signs[i];
                float a = sign.AngleDeg * Mathf.Deg2Rad;
                Vector2 at = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * SignDistance;
                // Plantilla canonica: abajo del anillo (270) apuntando arriba; rotarla lleva ese "abajo" al angulo pedido.
                AddTemplate(result, templateOf(sign.Id), at, StampPattern.SignOfRadius, sign.AngleDeg - 270f + sign.TurnDeg);
            }
            result.Add(Ring(def.GapDeg));
            return result;
        }

        /// <summary>Anillo casi cerrado con el hueco arriba (gapDeg grados), radio 1.</summary>
        public static List<Vector2> Ring(float gapDeg)
        {
            var ring = new List<Vector2>(RingSegments + 1);
            float from = 90f + gapDeg * 0.5f, span = 360f - gapDeg;
            for (int i = 0; i <= RingSegments; i++)
            {
                float a = (from + span * i / RingSegments) * Mathf.Deg2Rad;
                ring.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
            }
            return ring;
        }

        private static void AddTemplate(List<List<Vector2>> into, List<List<Vector2>> template, Vector2 center, float extent, float rotationDeg)
        {
            if (template == null) return;
            float c = Mathf.Cos(rotationDeg * Mathf.Deg2Rad), s = Mathf.Sin(rotationDeg * Mathf.Deg2Rad);
            for (int k = 0; k < template.Count; k++)
            {
                var src = template[k];
                var stroke = new List<Vector2>(src.Count);
                for (int i = 0; i < src.Count; i++)
                {
                    Vector2 local = (src[i] - new Vector2(0.5f, 0.5f)) * extent;
                    stroke.Add(center + new Vector2(local.x * c - local.y * s, local.x * s + local.y * c));
                }
                into.Add(stroke);
            }
        }
    }
}
