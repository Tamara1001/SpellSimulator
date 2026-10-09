using System.Linq;
using UnityEditor;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Compiler;
using SpellSimulator.Core;
using SpellSimulator.Glyphs;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Dibuja sellos de plantilla en un lienzo y los lanza (Play Mode, por Unity MCP). Para probar hechizos y
    /// reacciones sin dibujar a mano. Ej.: HarnessSeals.Cast("PaperCanvas", "fire", ("column", 270f), ("column", 90f)).
    /// Cada signo: (id, angulo en el anillo en grados, Y arriba). El signo se orienta hacia el centro (pose canonica).
    /// </summary>
    public static class HarnessSeals
    {
        public const float RingRadius = 0.42f;
        public const float SignRadius = 0.3f;

        public static SpellIR Cast(string canvasName, string sigil, params (string id, float angleDeg)[] signs)
        {
            var canvas = GameObject.Find(canvasName).GetComponent<DrawingCanvas>();
            Draw(canvas, sigil, signs);
            return Object.FindFirstObjectByType<SpellCaster>().Cast(canvas);
        }

        public static void Draw(DrawingCanvas canvas, string sigil, params (string id, float angleDeg)[] signs)
        {
            var dictionary = Dictionary();
            var center = new Vector2(0.5f, 0.5f);
            canvas.Clear();
            AddTemplate(canvas.Session, dictionary, sigil, center, 0.24f, 0f);
            foreach (var (id, angle) in signs)
            {
                Vector2 at = center + GeometryUtils.VectorFromAngleDeg(angle) * SignRadius;
                // Plantilla: abajo del anillo (270) apuntando arriba. Rotarla lleva ese "abajo" a la posicion pedida.
                AddTemplate(canvas.Session, dictionary, id, at, 0.12f, angle - 270f);
            }
            var ring = new Stroke();
            for (int i = 0; i <= 64; i++) ring.AddPoint(center + GeometryUtils.VectorFromAngleDeg(i / 64f * 360f) * RingRadius);
            canvas.Session.AddStroke(ring);
            canvas.Redraw();
        }

        /// <summary>
        /// Dibuja un sello compuesto (circulos dentro de un gran circulo): cada interior con su sigilo, sus signos y su anillo,
        /// y el anillo EXTERIOR al final (canon: el anillo se cierra al final). Las medidas son en el cuadrado central del
        /// lienzo (lado = lado menor, 1 = todo el cuadrado; centro en (0,5; 0,5)), asi los circulos son circulos en lienzos no
        /// cuadrados. Cada interior: (sigilo, signos (id, angulo), desplazamiento desde el centro, radio).
        /// </summary>
        public static void DrawNested(DrawingCanvas canvas, float outerRadius,
            params (string sigil, (string id, float angleDeg)[] signs, Vector2 offset, float radius)[] inners)
            => DrawNested(canvas, outerRadius, 1f, inners);

        /// <param name="innerCoverage">Fraccion de vuelta de cada anillo interior (menos de 1 = interiores abiertos).</param>
        public static void DrawNested(DrawingCanvas canvas, float outerRadius, float innerCoverage,
            params (string sigil, (string id, float angleDeg)[] signs, Vector2 offset, float radius)[] inners)
        {
            var dictionary = Dictionary();
            var square = new DrawingSession();
            var center = new Vector2(0.5f, 0.5f);
            foreach (var inner in inners)
            {
                Vector2 c = center + inner.offset;
                float k = inner.radius / RingRadius;
                AddTemplate(square, dictionary, inner.sigil, c, 0.24f * k, 0f);
                foreach (var (id, angle) in inner.signs ?? new (string, float)[0])
                    AddTemplate(square, dictionary, id, c + GeometryUtils.VectorFromAngleDeg(angle) * SignRadius * k, 0.12f * k, angle - 270f);
                square.AddStroke(Circle(c, inner.radius, innerCoverage));
            }
            square.AddStroke(Circle(center, outerRadius, 1f));

            // Cuadrado central -> UV del lienzo.
            Vector2 iso = canvas.UvToIsotropic;
            float side = Mathf.Min(iso.x, iso.y);
            canvas.Clear();
            foreach (var stroke in square.Strokes)
            {
                var uv = new Stroke();
                foreach (var p in stroke.Points)
                {
                    Vector2 q = iso * 0.5f + (p - center) * side;
                    uv.AddPoint(new Vector2(q.x / iso.x, q.y / iso.y));
                }
                canvas.Session.AddStroke(uv);
            }
            canvas.Redraw();
        }

        private static Stroke Circle(Vector2 center, float radius, float coverage)
        {
            var ring = new Stroke();
            int steps = Mathf.Max(8, Mathf.RoundToInt(64 * Mathf.Clamp01(coverage)));
            float sweep = 360f * Mathf.Clamp01(coverage);
            for (int i = 0; i <= steps; i++) ring.AddPoint(center + GeometryUtils.VectorFromAngleDeg(i / (float)steps * sweep) * radius);
            return ring;
        }

        private static GlyphDictionary Dictionary() =>
            AssetDatabase.FindAssets("t:GlyphDictionaryAsset")
                .Select(g => AssetDatabase.LoadAssetAtPath<GlyphDictionaryAsset>(AssetDatabase.GUIDToAssetPath(g)))
                .First().Dictionary;

        private static void AddTemplate(DrawingSession session, GlyphDictionary dictionary, string id, Vector2 center, float size, float rotationDeg)
        {
            foreach (var points in dictionary.Get(id).TemplateStrokes)
            {
                var stroke = new Stroke();
                foreach (var p in points)
                {
                    Vector2 q = GeometryUtils.RotatePointAroundPivot(p, new Vector2(0.5f, 0.5f), rotationDeg);
                    stroke.AddPoint((q - new Vector2(0.5f, 0.5f)) * size + center);
                }
                session.AddStroke(stroke);
            }
        }
    }
}
