using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpellSimulator.UI
{
    /// <summary>
    /// Ilustracion de un glifo en el lector de libros: el anillo de un sello (doble circulo con marcas) y los trazos de
    /// plantilla del diccionario ([0,1], Y hacia arriba) dibujados con tinta, con una animacion de trazado al mostrarse.
    /// Sin trazos dibuja solo el anillo y una estrella (ornamento de portada / colofon).
    /// </summary>
    public class GlyphSketch : VisualElement
    {
        private const float DrawSeconds = 1.3f;
        private const float GlyphScale = 0.6f;

        private static readonly Color Ink = new Color(0.17f, 0.10f, 0.24f, 0.95f);
        private static readonly Color RingInk = new Color(0.55f, 0.38f, 0.16f, 0.75f);
        private static readonly Color RingFaint = new Color(0.55f, 0.38f, 0.16f, 0.35f);

        private readonly List<List<Vector2>> strokes = new List<List<Vector2>>();
        private int totalPoints;
        private float progress = 1f;
        private float startTime;
        private readonly IVisualElementScheduledItem animation;

        public GlyphSketch()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            animation = schedule.Execute(Tick).Every(16);
            animation.Pause();
        }

        /// <summary>Muestra estos trazos (o ninguno: solo el ornamento) y arranca la animacion.</summary>
        public void SetStrokes(IReadOnlyList<List<Vector2>> source, bool animate = true)
        {
            strokes.Clear();
            totalPoints = 0;
            if (source != null)
                foreach (var s in source)
                {
                    if (s == null || s.Count == 0) continue;
                    strokes.Add(s);
                    totalPoints += s.Count;
                }
            progress = animate ? 0f : 1f;
            startTime = Time.unscaledTime;
            if (animate) animation.Resume();
            MarkDirtyRepaint();
        }

        private void Tick()
        {
            progress = Mathf.Clamp01((Time.unscaledTime - startTime) / DrawSeconds);
            MarkDirtyRepaint();
            if (progress >= 1f) animation.Pause();
        }

        private void Draw(MeshGenerationContext ctx)
        {
            Rect r = contentRect;
            float side = Mathf.Min(r.width, r.height);
            if (side < 4f) return;
            Vector2 c = r.center;
            float radius = side * 0.47f;
            var p = ctx.painter2D;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;

            // Anillo del sello: dos circulos y marcas cada 15 grados (las grandes cada 45).
            float ring = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress * 2.2f));
            p.strokeColor = RingInk;
            p.lineWidth = Mathf.Max(1.5f, side * 0.008f);
            Circle(p, c, radius, ring);
            p.strokeColor = RingFaint;
            p.lineWidth = Mathf.Max(1f, side * 0.004f);
            Circle(p, c, radius * 0.9f, ring);
            p.strokeColor = RingInk;
            int ticks = Mathf.RoundToInt(24 * ring);
            for (int i = 0; i < ticks; i++)
            {
                float a = i * Mathf.PI / 12f - Mathf.PI / 2f;
                float inner = radius * (i % 6 == 0 ? 0.84f : 0.875f);
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                p.BeginPath();
                p.MoveTo(c + dir * inner);
                p.LineTo(c + dir * radius * 0.9f);
                p.Stroke();
            }

            if (strokes.Count == 0) { Star(p, c, radius * 0.5f * ring); return; }

            // Trazos: cuadrado centrado de lado GlyphScale * diametro; Y de la plantilla hacia arriba.
            float size = radius * 2f * GlyphScale;
            Vector2 origin = new Vector2(c.x - size / 2f, c.y + size / 2f);
            int budget = Mathf.CeilToInt(totalPoints * progress);
            p.strokeColor = Ink;
            p.lineWidth = Mathf.Max(2.5f, side * 0.022f);
            foreach (var s in strokes)
            {
                if (budget <= 0) break;
                int n = Mathf.Min(s.Count, budget);
                budget -= n;
                p.BeginPath();
                p.MoveTo(origin + new Vector2(s[0].x * size, -s[0].y * size));
                if (n == 1) p.LineTo(origin + new Vector2(s[0].x * size + 0.5f, -s[0].y * size));
                for (int i = 1; i < n; i++) p.LineTo(origin + new Vector2(s[i].x * size, -s[i].y * size));
                p.Stroke();
            }
        }

        private static void Circle(Painter2D p, Vector2 c, float radius, float fraction)
        {
            if (fraction <= 0f) return;
            p.BeginPath();
            float start = -90f;
            p.Arc(c, radius, Angle.Degrees(start), Angle.Degrees(start + 360f * fraction));
            p.Stroke();
        }

        /// <summary>Estrella de cuatro puntas (ornamento de portada).</summary>
        private static void Star(Painter2D p, Vector2 c, float r)
        {
            if (r <= 1f) return;
            p.fillColor = new Color(0.62f, 0.44f, 0.18f, 0.85f);
            p.BeginPath();
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f - Mathf.PI / 2f;
                float len = i % 2 == 0 ? r : r * 0.22f;
                var pt = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * len;
                if (i == 0) p.MoveTo(pt); else p.LineTo(pt);
            }
            p.ClosePath();
            p.Fill();
        }
    }
}
