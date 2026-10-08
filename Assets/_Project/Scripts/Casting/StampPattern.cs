using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Core;

namespace SpellSimulator.Casting
{
    /// <summary>
    /// Trazos que deja una estampa (SYSTEMS_DESIGN 3). Logica pura en UV del lienzo, con tamaños en metros.
    /// - Sigilo: el sigilo + un anillo ABIERTO del diametro de la estampa (canon: se estampa abierto y se cierra a mano,
    ///   asi el sello queda preparado).
    /// - Signo: del tamaño que corresponde al anillo que ya hay en el papel, apuntando hacia su centro.
    /// Proporciones de las plantillas de prueba (HarnessSeals): sigilo = 0.57 R, signo = 0.29 R.
    /// </summary>
    public static class StampPattern
    {
        public const float SigilOfRadius = 0.57f;
        public const float SignOfRadius = 0.29f;
        /// <summary>Hueco del anillo estampado (grados, arriba): lo bastante grande para no contar como cerrado.</summary>
        public const float RingGapDeg = 34f;
        public const int RingSegments = 64;

        /// <summary>Id del glifo "Circulo Magico": al estamparlo deja solo el anillo abierto.</summary>
        public const string MagicCircleId = "magic_circle";

        /// <summary>Sigilo + anillo abierto centrados en centerUv. diameter en metros.</summary>
        public static List<Stroke> Sigil(List<List<Vector2>> template, Vector2 centerUv, float diameter, Vector2 canvasSize, float width)
        {
            var strokes = SigilOnly(template, centerUv, diameter, canvasSize, width);
            strokes.Add(OpenRing(centerUv, diameter, canvasSize, width));
            return strokes;
        }

        /// <summary>Solo el simbolo del sigilo (sin circulo exterior), a la proporcion que tiene dentro de un anillo de ese diametro.</summary>
        public static List<Stroke> SigilOnly(List<List<Vector2>> template, Vector2 centerUv, float diameter, Vector2 canvasSize, float width) =>
            Template(template, centerUv, diameter * 0.5f * SigilOfRadius, 0f, canvasSize, width);

        /// <summary>Anillo ABIERTO (hueco arriba) del diametro dado: el "Circulo Magico" que se cierra a mano.</summary>
        public static Stroke OpenRing(Vector2 centerUv, float diameter, Vector2 canvasSize, float width)
        {
            float radius = diameter * 0.5f;
            var ring = new Stroke { Width = width };
            float from = 90f + RingGapDeg * 0.5f, span = 360f - RingGapDeg;
            for (int i = 0; i <= RingSegments; i++)
            {
                float a = (from + span * i / RingSegments) * Mathf.Deg2Rad;
                ring.AddPoint(ToUv(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, centerUv, canvasSize));
            }
            return ring;
        }

        /// <summary>
        /// Signo en centerUv. ringRadius en metros (el del anillo existente). Si hay anillo, se rota para apuntar a su
        /// centro (plantilla canonica: abajo del anillo apuntando arriba).
        /// </summary>
        public static List<Stroke> Sign(List<List<Vector2>> template, Vector2 centerUv, float ringRadius, Vector2? ringCenterUv, Vector2 canvasSize, float width)
        {
            float rotation = 0f;
            if (ringCenterUv.HasValue)
            {
                Vector2 offset = Vector2.Scale(centerUv - ringCenterUv.Value, canvasSize);
                if (offset.sqrMagnitude > 1e-6f) rotation = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg - 270f;
            }
            return Template(template, centerUv, ringRadius * SignOfRadius, rotation, canvasSize, width);
        }

        /// <summary>Plantilla [0,1] (Y arriba) escalada a `extent` metros, rotada y ubicada en el lienzo.</summary>
        public static List<Stroke> Template(List<List<Vector2>> template, Vector2 centerUv, float extent, float rotationDeg, Vector2 canvasSize, float width)
        {
            var strokes = new List<Stroke>(template.Count);
            float c = Mathf.Cos(rotationDeg * Mathf.Deg2Rad), s = Mathf.Sin(rotationDeg * Mathf.Deg2Rad);
            foreach (var points in template)
            {
                var stroke = new Stroke { Width = width };
                foreach (var p in points)
                {
                    Vector2 local = (p - new Vector2(0.5f, 0.5f)) * extent;
                    local = new Vector2(local.x * c - local.y * s, local.x * s + local.y * c);
                    stroke.AddPoint(ToUv(local, centerUv, canvasSize));
                }
                strokes.Add(stroke);
            }
            return strokes;
        }

        private static Vector2 ToUv(Vector2 meters, Vector2 centerUv, Vector2 canvasSize) =>
            centerUv + new Vector2(meters.x / Mathf.Max(canvasSize.x, 1e-4f), meters.y / Mathf.Max(canvasSize.y, 1e-4f));
    }
}
