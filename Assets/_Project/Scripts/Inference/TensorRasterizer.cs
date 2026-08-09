using System;
using UnityEngine;
using SpellSimulator.Core;

namespace SpellSimulator.Inference
{
    public static class TensorRasterizer
    {
        /// <summary>
        /// Rasteriza una sesion de dibujo vectorial en un tensor float de tamano especificado,
        /// aplicando un margen y un grosor de trazo fijo.
        /// Los valores de retorno son -1 (fondo) y 1 (tinta).
        /// </summary>
        public static float[] Rasterize(DrawingSession session, int tensorSize = 96, float strokeRadius = 2f, float marginPercent = 0.1f)
        {
            float[] tensor = new float[tensorSize * tensorSize];
            
            // Inicializar a fondo (-1.0)
            for (int i = 0; i < tensor.Length; i++)
                tensor[i] = -1.0f;

            if (!session.GetBounds(out Vector2 min, out Vector2 max))
                return tensor;

            float width = max.x - min.x;
            float height = max.y - min.y;
            float maxDim = Mathf.Max(width, height, 0.0001f); // Evitar division por 0

            float cx = min.x + width / 2f;
            float cy = min.y + height / 2f;

            float margin = tensorSize * marginPercent;
            float usableSize = tensorSize - margin * 2;
            float scale = usableSize / maxDim;

            // Proyectar un punto del espacio original al espacio del tensor [0, tensorSize]
            Vector2 Project(Vector2 p)
            {
                float px = margin + (p.x - cx) * scale + (usableSize / 2f);
                float py = margin + (p.y - cy) * scale + (usableSize / 2f);
                return new Vector2(px, py);
            }

            foreach (var stroke in session.Strokes)
            {
                if (stroke.Points.Count == 0) continue;

                if (stroke.Points.Count == 1)
                {
                    Vector2 p = Project(stroke.Points[0]);
                    DrawCircle(tensor, tensorSize, p, strokeRadius);
                    continue;
                }

                for (int i = 0; i < stroke.Points.Count - 1; i++)
                {
                    Vector2 a = Project(stroke.Points[i]);
                    Vector2 b = Project(stroke.Points[i + 1]);
                    
                    // Invertir Y para que coincida con el formato del modelo (Y=0 arriba)
                    // Unity UV usualmente tiene Y=0 abajo. Si proyectamos, Y aumenta hacia arriba.
                    // Para que Y=0 sea arriba, invertimos Y.
                    a.y = tensorSize - 1 - a.y;
                    b.y = tensorSize - 1 - b.y;

                    DrawThickLine(tensor, tensorSize, a, b, strokeRadius);
                }
            }

            return tensor;
        }

        private static void DrawCircle(float[] tensor, int size, Vector2 p, float radius)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(p.x - radius - 1));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(p.x + radius + 1));
            int minY = Mathf.Max(0, Mathf.FloorToInt(p.y - radius - 1));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(p.y + radius + 1));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (Vector2.Distance(new Vector2(x, y), p) <= radius)
                    {
                        tensor[y * size + x] = 1.0f;
                    }
                }
            }
        }

        private static void DrawThickLine(float[] tensor, int size, Vector2 a, Vector2 b, float radius)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - radius - 1));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + radius + 1));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - radius - 1));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + radius + 1));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (DistanceToSegment(new Vector2(x, y), a, b) <= radius)
                    {
                        tensor[y * size + x] = 1.0f;
                    }
                }
            }
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float sqrLen = ab.sqrMagnitude;
            if (sqrLen < 0.0001f) return Vector2.Distance(p, a);

            Vector2 ap = p - a;
            float t = Vector2.Dot(ap, ab) / sqrLen;
            t = Mathf.Clamp01(t);

            Vector2 closest = a + ab * t;
            return Vector2.Distance(p, closest);
        }
        
        /// <summary>
        /// Convierte el tensor array en una Texture2D para debuggear.
        /// </summary>
        public static Texture2D DebugTensorToTexture(float[] tensor, int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            tex.filterMode = FilterMode.Point;
            Color[] colors = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Volver a invertir Y visualmente para que se vea derecho en la UI de Unity
                    int texY = size - 1 - y;
                    float val = tensor[y * size + x];
                    // -1 es negro, 1 es blanco
                    float normalized = (val + 1f) / 2f; 
                    colors[texY * size + x] = new Color(normalized, normalized, normalized);
                }
            }
            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }
    }
}
