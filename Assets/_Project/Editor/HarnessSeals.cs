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
