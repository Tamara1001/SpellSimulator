using UnityEngine;
using SpellSimulator.Core;

namespace SpellSimulator.Recognition
{
    /// <summary>
    /// Rasteriza un candidato a la entrada del modelo: 96x96, fondo -1, tinta +1.
    /// Replica renderCandidateTensor (parser/ml/rendering.ts) de la referencia, que es tambien como se
    /// rasterizaron los datos de entrenamiento: canvas 2x, linea de 2 px (4 px en 2x), margen total 20%,
    /// escala uniforme anclada arriba-izquierda (el eje corto NO se centra) y promedio 2x2 (antialias).
    /// </summary>
    public static class TensorRasterizer
    {
        public const int Size = 96;
        public const int PixelCount = Size * Size;
        public const float Margin = 0.2f;
        public const float StrokeWidth = 2f;
        private const int RenderScale = 2;
        private const int CanvasSize = Size * RenderScale;

        // Buffer de trabajo reutilizado (solo se usa desde el hilo principal).
        private static readonly bool[] ink = new bool[CanvasSize * CanvasSize];

        /// <summary>Atajo que aloca un tensor nuevo.</summary>
        public static float[] Rasterize(DrawingSession session, float rotationDeg = 0f)
        {
            var tensor = new float[PixelCount];
            Rasterize(session, rotationDeg, tensor, 0);
            return tensor;
        }

        /// <summary>
        /// Escribe el tensor en buffer[offset .. offset + 9216). Rota el candidato alrededor del centro de su
        /// bounding box antes de normalizar (para test-time augmentation).
        /// </summary>
        public static void Rasterize(DrawingSession session, float rotationDeg, float[] buffer, int offset)
        {
            System.Array.Clear(ink, 0, ink.Length);

            if (session.GetBounds(out Vector2 rawMin, out Vector2 rawMax))
            {
                Vector2 pivot = (rawMin + rawMax) * 0.5f;
                float radians = rotationDeg * Mathf.Deg2Rad;
                float cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);

                // Bounds del candidato ya rotado.
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
                foreach (var s in session.Strokes)
                    foreach (var p in s.Points)
                    {
                        Vector2 r = Rotate(p, pivot, cos, sin);
                        min = Vector2.Min(min, r);
                        max = Vector2.Max(max, r);
                    }

                float marginPx = CanvasSize * Margin / 2f;
                float usable = CanvasSize - marginPx * 2f;
                // En la referencia el minimo es 1 px de canvas; aca las coordenadas son de lienzo [0,1].
                float scale = Mathf.Max(max.x - min.x, max.y - min.y, 1e-4f);
                float k = (usable - 1f) / scale;
                float halfWidth = StrokeWidth * RenderScale * 0.5f;
                float dotRadius = StrokeWidth * RenderScale;

                foreach (var s in session.Strokes)
                {
                    if (s.Points.Count == 0) continue;
                    Vector2 prev = Project(Rotate(s.Points[0], pivot, cos, sin), min, max, marginPx, k);
                    if (s.Points.Count == 1)
                    {
                        StampSegment(prev, prev, dotRadius);
                        continue;
                    }
                    for (int i = 1; i < s.Points.Count; i++)
                    {
                        Vector2 next = Project(Rotate(s.Points[i], pivot, cos, sin), min, max, marginPx, k);
                        StampSegment(prev, next, halfWidth);
                        prev = next;
                    }
                }
            }

            const float samples = RenderScale * RenderScale;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int hits = 0;
                    for (int dy = 0; dy < RenderScale; dy++)
                        for (int dx = 0; dx < RenderScale; dx++)
                            if (ink[(y * RenderScale + dy) * CanvasSize + x * RenderScale + dx]) hits++;
                    float pixel = hits / samples;
                    buffer[offset + y * Size + x] = (pixel - 0.5f) / 0.5f;
                }
            }
        }

        private static Vector2 Rotate(Vector2 p, Vector2 pivot, float cos, float sin)
        {
            Vector2 d = p - pivot;
            return new Vector2(d.x * cos - d.y * sin, d.x * sin + d.y * cos) + pivot;
        }

        /// <summary>Lienzo (Y arriba) -> pixel del canvas 2x (Y abajo), anclado a la esquina superior izquierda.</summary>
        private static Vector2 Project(Vector2 p, Vector2 min, Vector2 max, float marginPx, float k)
        {
            return new Vector2(marginPx + (p.x - min.x) * k, marginPx + (max.y - p.y) * k);
        }

        private static void StampSegment(Vector2 a, Vector2 b, float radius)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - radius - 1f));
            int maxX = Mathf.Min(CanvasSize - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + radius + 1f));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - radius - 1f));
            int maxY = Mathf.Min(CanvasSize - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + radius + 1f));
            float radiusSq = radius * radius;

            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    // Centro del pixel, como en canvas 2D.
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float t = lengthSq > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq) : 0f;
                    if ((p - (a + ab * t)).sqrMagnitude <= radiusSq) ink[y * CanvasSize + x] = true;
                }
            }
        }

        /// <summary>Tensor -> Texture2D en escala de grises para la UI de diagnostico.</summary>
        public static Texture2D DebugTensorToTexture(float[] tensor, int offset = 0)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGB24, false) { filterMode = FilterMode.Point };
            var colors = new Color32[PixelCount];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // Fila 0 del tensor es arriba; fila 0 de la textura es abajo.
                    byte v = (byte)Mathf.RoundToInt((tensor[offset + y * Size + x] + 1f) * 127.5f);
                    colors[(Size - 1 - y) * Size + x] = new Color32(v, v, v, 255);
                }
            }
            tex.SetPixels32(colors);
            tex.Apply();
            return tex;
        }
    }
}
