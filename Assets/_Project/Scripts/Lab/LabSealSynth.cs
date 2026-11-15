using System;
using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Casting;
using SpellSimulator.Learning;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Sintetiza los trazos de un sello a partir de un <see cref="LabSealSpec"/> (logica pura). Misma geometria que
    /// <see cref="SpellStrokeBuilder"/> (unidades de anillo, Y arriba), pero con signos parametricos; el resultado
    /// se entrega en UV del papel: centro (0,5; 0,5), radio del anillo = ringFill / 2.
    /// </summary>
    public static class LabSealSynth
    {
        /// <summary>
        /// Arma los trazos (sigilo, signos, anillo al final). <paramref name="templateOf"/> devuelve la plantilla [0,1] de un
        /// glifo por id (o null). Con <paramref name="rng"/> null se usa <c>new Random(spec.seed)</c>: determinista por semilla.
        /// El spec se recorta con <see cref="LabSealSpec.Clamp"/>. Reserva listas (no llamar por frame).
        /// </summary>
        public static List<List<Vector2>> Build(in LabSealSpec spec, Func<string, List<List<Vector2>>> templateOf, System.Random rng = null)
        {
            LabSealSpec s = LabSealSpec.Clamp(spec);
            if (rng == null) rng = new System.Random(s.seed);
            var unit = new List<List<Vector2>>();

            if (!string.IsNullOrEmpty(s.sigilId) && templateOf != null)
                AddTemplate(unit, templateOf(s.sigilId), Vector2.zero, StampPattern.SigilOfRadius * s.sigilScale, 0f);

            if (s.signCount > 0 && !string.IsNullOrEmpty(s.signId) && templateOf != null)
            {
                var signTemplate = templateOf(s.signId);
                for (int i = 0; i < s.signCount; i++)
                {
                    float angle = s.baseAngleDeg + 360f * i / s.signCount;
                    float rad = angle * Mathf.Deg2Rad;
                    Vector2 at = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * s.signDistance;
                    float extent = StampPattern.SignOfRadius * s.signLength * (i == 0 ? s.asymmetry : 1f);
                    // Plantilla canonica: abajo del anillo (270) apuntando arriba; ver SpellStrokeBuilder.
                    AddTemplate(unit, signTemplate, at, extent, angle - 270f + s.turnDeg);
                }
            }

            unit.Add(SpellStrokeBuilder.Ring(s.ringGapDeg));

            if (s.tremor > 0f) ApplyTremor(unit, s.tremor, rng);

            float radiusUv = s.ringFill * 0.5f;
            var result = new List<List<Vector2>>(unit.Count);
            for (int k = 0; k < unit.Count; k++)
            {
                var src = unit[k];
                var stroke = new List<Vector2>(src.Count);
                for (int i = 0; i < src.Count; i++) stroke.Add(new Vector2(0.5f + src[i].x * radiusUv, 0.5f + src[i].y * radiusUv));
                result.Add(stroke);
            }
            return result;
        }

        /// <summary>Desplaza cada punto hasta amplitude (en radios de anillo), promediando con el vecino para que parezca pulso y no ruido.</summary>
        private static void ApplyTremor(List<List<Vector2>> strokes, float amplitude, System.Random rng)
        {
            for (int k = 0; k < strokes.Count; k++)
            {
                var pts = strokes[k];
                Vector2 prev = Vector2.zero;
                for (int i = 0; i < pts.Count; i++)
                {
                    float ang = (float)(rng.NextDouble() * Math.PI * 2.0);
                    float mag = (float)rng.NextDouble() * amplitude;
                    Vector2 jitter = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * mag;
                    pts[i] += i == 0 ? jitter : (prev + jitter) * 0.5f;
                    prev = jitter;
                }
            }
        }

        private static void AddTemplate(List<List<Vector2>> into, List<List<Vector2>> template, Vector2 center, float extent, float rotationDeg)
        {
            if (template == null) return;
            float c = Mathf.Cos(rotationDeg * Mathf.Deg2Rad), sn = Mathf.Sin(rotationDeg * Mathf.Deg2Rad);
            for (int k = 0; k < template.Count; k++)
            {
                var src = template[k];
                var stroke = new List<Vector2>(src.Count);
                for (int i = 0; i < src.Count; i++)
                {
                    Vector2 local = (src[i] - new Vector2(0.5f, 0.5f)) * extent;
                    stroke.Add(center + new Vector2(local.x * c - local.y * sn, local.x * sn + local.y * c));
                }
                into.Add(stroke);
            }
        }
    }
}
