using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Iconos de glifos para las grillas del Laboratorio v4 (punto 3): pinta las plantillas de trazos (GlyphEntry.TemplateStrokes)
    /// en una celda de un arreglo de pixeles (atlas). Logica pura y sin Unity runtime (solo structs): se usa UNA vez al construir
    /// la grilla, nunca por frame (el trazado del lienzo sigue por GPU; esto es una textura chica de iconos). La plantilla se
    /// escala uniforme para entrar en la celda con margen y conserva el «arriba» (y crece hacia arriba, como el UV).
    /// </summary>
    public static class LabGlyphIcons
    {
        /// <summary>Margen (fraccion de la celda) alrededor del glifo.</summary>
        public const float Margin = 0.14f;

        /// <summary>Llena una celda con un color (fondo de pergamino).</summary>
        public static void Fill(Color32[] pixels, int atlasWidth, int cellX, int cellY, int cellSize, Color32 color)
        {
            for (int y = 0; y < cellSize; y++)
            {
                int row = (cellY + y) * atlasWidth + cellX;
                for (int x = 0; x < cellSize; x++) pixels[row + x] = color;
            }
        }

        /// <summary>
        /// Pinta los trazos en la celda (<paramref name="cellX"/>, <paramref name="cellY"/> = esquina de abajo a la izquierda, en
        /// pixeles) con lineas de <paramref name="thicknessPx"/> de grosor. Devuelve cuantos pixeles pinto (0 si no hay trazos).
        /// </summary>
        public static int Draw(IReadOnlyList<List<Vector2>> strokes, Color32[] pixels, int atlasWidth, int cellX, int cellY, int cellSize,
            Color32 ink, float thicknessPx)
        {
            if (strokes == null || pixels == null || cellSize <= 0) return 0;
            if (!Bounds(strokes, out Vector2 min, out Vector2 max)) return 0;
            Vector2 size = max - min;
            float side = Mathf.Max(size.x, size.y, 1e-5f);
            float usable = cellSize * (1f - 2f * Margin);
            float scale = usable / side;
            // Centrado en la celda.
            Vector2 offset = new Vector2(cellX + cellSize * 0.5f - (min.x + size.x * 0.5f) * scale,
                cellY + cellSize * 0.5f - (min.y + size.y * 0.5f) * scale);
            int painted = 0;
            for (int s = 0; s < strokes.Count; s++)
            {
                List<Vector2> points = strokes[s];
                if (points == null || points.Count == 0) continue;
                if (points.Count == 1)
                {
                    painted += Disc(pixels, atlasWidth, cellX, cellY, cellSize, points[0] * scale + offset, thicknessPx * 0.5f, ink);
                    continue;
                }
                for (int i = 1; i < points.Count; i++)
                    painted += Segment(pixels, atlasWidth, cellX, cellY, cellSize, points[i - 1] * scale + offset, points[i] * scale + offset, thicknessPx, ink);
            }
            return painted;
        }

        /// <summary>Icono de «ninguno»: un circulo con una barra cruzada.</summary>
        public static int DrawNone(Color32[] pixels, int atlasWidth, int cellX, int cellY, int cellSize, Color32 ink, float thicknessPx)
        {
            var c = new Vector2(cellX + cellSize * 0.5f, cellY + cellSize * 0.5f);
            float r = cellSize * (0.5f - Margin);
            int painted = 0;
            const int segments = 24;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                painted += Segment(pixels, atlasWidth, cellX, cellY, cellSize, c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r,
                    c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r, thicknessPx, ink);
            }
            Vector2 d = new Vector2(0.7071f, 0.7071f) * r;
            painted += Segment(pixels, atlasWidth, cellX, cellY, cellSize, c - d, c + d, thicknessPx, ink);
            return painted;
        }

        /// <summary>Caja que contiene todos los puntos; false si no hay puntos.</summary>
        public static bool Bounds(IReadOnlyList<List<Vector2>> strokes, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            bool any = false;
            for (int s = 0; s < strokes.Count; s++)
            {
                List<Vector2> points = strokes[s];
                if (points == null) continue;
                for (int i = 0; i < points.Count; i++)
                {
                    min = Vector2.Min(min, points[i]);
                    max = Vector2.Max(max, points[i]);
                    any = true;
                }
            }
            return any;
        }

        private static int Segment(Color32[] pixels, int atlasWidth, int cellX, int cellY, int cellSize, Vector2 a, Vector2 b, float thickness, Color32 ink)
        {
            float length = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length * 2f));
            int painted = 0;
            for (int i = 0; i <= steps; i++)
                painted += Disc(pixels, atlasWidth, cellX, cellY, cellSize, Vector2.Lerp(a, b, i / (float)steps), thickness * 0.5f, ink);
            return painted;
        }

        /// <summary>Disco de tinta recortado a la celda; cuenta solo pixeles que cambiaron.</summary>
        private static int Disc(Color32[] pixels, int atlasWidth, int cellX, int cellY, int cellSize, Vector2 center, float radius, Color32 ink)
        {
            int painted = 0;
            int x0 = Mathf.Max(cellX, Mathf.FloorToInt(center.x - radius)), x1 = Mathf.Min(cellX + cellSize - 1, Mathf.CeilToInt(center.x + radius));
            int y0 = Mathf.Max(cellY, Mathf.FloorToInt(center.y - radius)), y1 = Mathf.Min(cellY + cellSize - 1, Mathf.CeilToInt(center.y + radius));
            float r2 = radius * radius;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - center.x, dy = y + 0.5f - center.y;
                    if (dx * dx + dy * dy > r2) continue;
                    int index = y * atlasWidth + x;
                    Color32 p = pixels[index];
                    if (p.r == ink.r && p.g == ink.g && p.b == ink.b && p.a == ink.a) continue;
                    pixels[index] = ink;
                    painted++;
                }
            return painted;
        }
    }
}
