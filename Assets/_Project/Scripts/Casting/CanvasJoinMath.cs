using UnityEngine;
using SpellSimulator.Core;

namespace SpellSimulator.Casting
{
    /// <summary>
    /// Un lienzo como rectangulo orientado en 3D: centro, ejes del UV (right = u creciente, up = v creciente), cara con
    /// tinta (normal) y tamano en metros.
    /// </summary>
    public struct CanvasRect
    {
        public Vector3 center;
        public Vector3 right;
        public Vector3 up;
        public Vector3 face;
        public float width;
        public float height;

        public CanvasRect(Vector3 center, Vector3 right, Vector3 up, Vector3 face, float width, float height)
        {
            this.center = center;
            this.right = right;
            this.up = up;
            this.face = face;
            this.width = width;
            this.height = height;
        }

        /// <summary>UV [0,1] del rectangulo -> punto del mundo.</summary>
        public Vector3 UvToWorld(Vector2 uv) => center + right * ((uv.x - 0.5f) * width) + up * ((uv.y - 0.5f) * height);
    }

    /// <summary>Rectangulo envolvente de dos lienzos unidos, en el marco (ejes) del primero.</summary>
    public struct MergedLayout
    {
        public Vector3 center;
        public Vector3 right;
        public Vector3 up;
        /// <summary>Rotacion del marco de A (right/up/forward = right x up).</summary>
        public Quaternion rotation;
        public float width;
        public float height;
    }

    /// <summary>
    /// Mitades de un sello (canon Sylph Shoes) para cualquier lienzo. Logica pura: decide si dos lienzos estan pegados
    /// borde con borde (por cualquier borde, en el mismo plano y con la tinta hacia el mismo lado) y fusiona sus trazos en
    /// una sola sesion UV del rectangulo envolvente. Generaliza <see cref="SplitJoinMath"/> (solo lado a lado).
    /// </summary>
    public static class CanvasJoinMath
    {
        /// <summary>Tolerancia (m) de distancia al plano y entre bordes.</summary>
        public const float DefaultTolerance = 0.06f;
        /// <summary>Minimo producto escalar entre las caras con tinta.</summary>
        public const float MinFaceDot = 0.9f;
        /// <summary>Fraccion minima del lado menor que debe apoyarse a lo largo del borde comun.</summary>
        public const float MinEdgeOverlap = 0.4f;
        /// <summary>Fraccion maxima del area del lienzo menor que puede solaparse.</summary>
        public const float MaxAreaOverlap = 0.15f;

        /// <summary>
        /// True si A y B estan pegados borde con borde: coplanares (dentro de la tolerancia), con la cara con tinta hacia
        /// el mismo lado, bordes a menos de la tolerancia, apoyados al menos 40 % del lado menor y sin solaparse mas de
        /// 15 % del area del menor. B puede estar girado en el plano (se usa su caja en el marco de A).
        /// </summary>
        public static bool AreAdjacent(in CanvasRect a, in CanvasRect b, float tolerance = DefaultTolerance)
        {
            if (Vector3.Dot(a.face, b.face) < MinFaceDot) return false;
            if (Mathf.Abs(Vector3.Dot(b.center - a.center, a.face)) > tolerance) return false;

            ProjectBounds(a, b, out Vector2 bMin, out Vector2 bMax);
            float halfW = a.width * 0.5f, halfH = a.height * 0.5f;
            float bWidth = bMax.x - bMin.x, bHeight = bMax.y - bMin.y;

            // Solape (negativo = hueco) en cada eje del marco de A.
            float overlapX = Mathf.Min(halfW, bMax.x) - Mathf.Max(-halfW, bMin.x);
            float overlapY = Mathf.Min(halfH, bMax.y) - Mathf.Max(-halfH, bMin.y);

            float area = Mathf.Max(0f, overlapX) * Mathf.Max(0f, overlapY);
            float limit = MaxAreaOverlap * Mathf.Min(a.width * a.height, bWidth * bHeight);
            if (area > limit) return false;

            // Izquierda/derecha: borde vertical comun. Arriba/abajo: borde horizontal comun.
            bool sideBySide = overlapX >= -tolerance && overlapY >= MinEdgeOverlap * Mathf.Min(a.height, bHeight);
            bool stacked = overlapY >= -tolerance && overlapX >= MinEdgeOverlap * Mathf.Min(a.width, bWidth);
            return sideBySide || stacked;
        }

        /// <summary>
        /// Fusiona los trazos de A y B en una sesion UV [0,1] del rectangulo envolvente (ejes right/up de A). Cada punto UV
        /// se pasa a metros en el marco de A (B puede estar girado en el plano) y luego a UV del envolvente. Conserva
        /// Width e Ink de cada trazo.
        /// </summary>
        public static DrawingSession Merge(in CanvasRect a, DrawingSession sessionA, in CanvasRect b, DrawingSession sessionB, out MergedLayout layout)
        {
            ProjectBounds(a, b, out Vector2 bMin, out Vector2 bMax);
            float minX = Mathf.Min(-a.width * 0.5f, bMin.x), maxX = Mathf.Max(a.width * 0.5f, bMax.x);
            float minY = Mathf.Min(-a.height * 0.5f, bMin.y), maxY = Mathf.Max(a.height * 0.5f, bMax.y);
            float width = Mathf.Max(maxX - minX, 1e-6f), height = Mathf.Max(maxY - minY, 1e-6f);

            layout = new MergedLayout
            {
                center = a.center + a.right * ((minX + maxX) * 0.5f) + a.up * ((minY + maxY) * 0.5f),
                right = a.right,
                up = a.up,
                rotation = Quaternion.LookRotation(Vector3.Cross(a.right, a.up), a.up),
                width = width,
                height = height
            };

            var merged = new DrawingSession();
            Append(merged, sessionA, a, a, minX, minY, width, height);
            Append(merged, sessionB, b, a, minX, minY, width, height);
            return merged;
        }

        private static void Append(DrawingSession target, DrawingSession source, in CanvasRect from, in CanvasRect frame,
            float minX, float minY, float width, float height)
        {
            if (source == null) return;
            foreach (var stroke in source.Strokes)
            {
                var copy = new Stroke { Width = stroke.Width, Ink = stroke.Ink };
                foreach (var p in stroke.Points)
                {
                    Vector3 d = from.UvToWorld(p) - frame.center;
                    copy.AddPoint(new Vector2((Vector3.Dot(d, frame.right) - minX) / width, (Vector3.Dot(d, frame.up) - minY) / height));
                }
                target.AddStroke(copy);
            }
        }

        /// <summary>Caja de B (sus 4 esquinas) en el marco 2D de A, relativa al centro de A.</summary>
        private static void ProjectBounds(in CanvasRect a, in CanvasRect b, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            Vector3 hx = b.right * (b.width * 0.5f), hy = b.up * (b.height * 0.5f);
            for (int i = 0; i < 4; i++)
            {
                Vector3 corner = b.center + ((i & 1) == 0 ? hx : -hx) + ((i & 2) == 0 ? hy : -hy);
                Vector3 d = corner - a.center;
                var p = new Vector2(Vector3.Dot(d, a.right), Vector3.Dot(d, a.up));
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
        }
    }
}
