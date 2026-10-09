using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Core;
using SpellSimulator.Parsing;

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
        /// <summary>Hueco maximo (m) entre los bordes de las dos mitades.</summary>
        public const float MaxEdgeGap = 0.12f;
        /// <summary>Compatibilidad: tolerancia por defecto de <see cref="AreAdjacent"/> (= <see cref="MaxEdgeGap"/>).</summary>
        public const float DefaultTolerance = MaxEdgeGap;
        /// <summary>Distancia maxima (m) de un lienzo al plano del otro (coplanaridad).</summary>
        public const float MaxPlaneDistance = 0.10f;
        /// <summary>Minimo producto escalar entre las caras con tinta.</summary>
        public const float MinFaceDot = 0.8f;
        /// <summary>Fraccion minima del lado menor que debe apoyarse a lo largo del borde comun.</summary>
        public const float MinEdgeOverlap = 0.25f;
        /// <summary>Solape maximo (fraccion del lado menor) en el eje que cruza el borde comun: dos hojas apoyadas se montan un poco.</summary>
        public const float MaxEdgeOverlapDepth = 0.35f;
        /// <summary>Cobertura minima del anillo fusionado para curar la costura (<see cref="HealSeam"/>).</summary>
        public const float MinHealCompleteness = 0.85f;
        /// <summary>Fraccion minima de la circunferencia que debe aportar CADA mitad.</summary>
        public const float MinHalfShare = 0.25f;

        private const int ShareBins = RingDetector.BinCount;
        private const float ShareBandRatio = RingDetector.GlyphBandHalfWidthRatio;

        /// <summary>
        /// True si A y B estan pegados borde con borde: coplanares (dentro de <see cref="MaxPlaneDistance"/>), con la cara con
        /// tinta hacia el mismo lado (dot &gt;= 0,8), bordes a menos de <paramref name="maxGap"/>, apoyados al menos 25 % del lado
        /// menor y montados como mucho 35 % del lado menor. B puede estar girado en el plano (se usa su caja en el marco de A).
        /// </summary>
        public static bool AreAdjacent(in CanvasRect a, in CanvasRect b, float maxGap = MaxEdgeGap)
        {
            if (Vector3.Dot(a.face, b.face) < MinFaceDot) return false;
            if (Mathf.Abs(Vector3.Dot(b.center - a.center, a.face)) > MaxPlaneDistance) return false;

            ProjectBounds(a, b, out Vector2 bMin, out Vector2 bMax);
            float halfW = a.width * 0.5f, halfH = a.height * 0.5f;
            float bWidth = bMax.x - bMin.x, bHeight = bMax.y - bMin.y;

            // Solape (negativo = hueco) en cada eje del marco de A.
            float overlapX = Mathf.Min(halfW, bMax.x) - Mathf.Max(-halfW, bMin.x);
            float overlapY = Mathf.Min(halfH, bMax.y) - Mathf.Max(-halfH, bMin.y);

            float minW = Mathf.Min(a.width, bWidth), minH = Mathf.Min(a.height, bHeight);
            // Izquierda/derecha: borde vertical comun. Arriba/abajo: borde horizontal comun.
            bool sideBySide = overlapX >= -maxGap && overlapX <= MaxEdgeOverlapDepth * minW && overlapY >= MinEdgeOverlap * minH;
            bool stacked = overlapY >= -maxGap && overlapY <= MaxEdgeOverlapDepth * minH && overlapX >= MinEdgeOverlap * minW;
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

        // ---------------------------------------------------------------- costura

        /// <summary>
        /// Aporte de un grupo de trazos [start, start+count) al anillo (centro/radio en el espacio de los puntos): fraccion de la
        /// circunferencia cubierta por tramos que caen sobre el circulo (banda del glifo).
        /// </summary>
        public static float RingShare(IReadOnlyList<Stroke> strokes, int start, int count, Vector2 center, float radius)
        {
            if (radius <= 1e-6f) return 0f;
            var bins = new bool[ShareBins];
            float band = radius * ShareBandRatio;
            int end = Mathf.Min(strokes.Count, start + count);
            for (int s = Mathf.Max(0, start); s < end; s++)
            {
                var pts = strokes[s].Points;
                for (int i = 0; i < pts.Count; i++)
                {
                    if (Mathf.Abs(Vector2.Distance(pts[i], center) - radius) > band) continue;
                    bool prevOn = i > 0 && Mathf.Abs(Vector2.Distance(pts[i - 1], center) - radius) <= band;
                    MarkBin(bins, center, prevOn ? pts[i - 1] : pts[i], pts[i]);
                }
            }
            int covered = 0;
            for (int i = 0; i < ShareBins; i++) if (bins[i]) covered++;
            return (float)covered / ShareBins;
        }

        /// <summary>
        /// True si cada mitad (los primeros <paramref name="countA"/> trazos son de A, el resto de B) aporta al menos
        /// <see cref="MinHalfShare"/> de la circunferencia del anillo. Evita que una hoja con un garabato se una a otra.
        /// <paramref name="scaledMerged"/> y el anillo estan en el mismo espacio (UV escalado a isotropico).
        /// </summary>
        public static bool EachHalfContributes(DrawingSession scaledMerged, int countA, RingInfo ring)
        {
            if (scaledMerged == null || ring == null || !ring.Found) return false;
            var strokes = scaledMerged.Strokes;
            float a = RingShare(strokes, 0, countA, ring.Center, ring.Radius);
            float b = RingShare(strokes, countA, strokes.Count - countA, ring.Center, ring.Radius);
            return a >= MinHalfShare && b >= MinHalfShare;
        }

        /// <summary>
        /// Cierra la costura: si el anillo detectado (en el espacio de <paramref name="merged"/> escalado por
        /// <paramref name="isotropic"/>) tiene cobertura &gt;= 0,85 pero no esta completo, agrega a <paramref name="merged"/> un trazo
        /// puente por cada tramo sin tinta, sobre la circunferencia del anillo y con el mismo Width/Ink que sus trazos. Devuelve
        /// cuantos trazos agrego (0 = nada que curar).
        /// </summary>
        public static int HealSeam(DrawingSession merged, RingInfo ring, Vector2 isotropic)
        {
            if (merged == null || ring == null || !ring.Found || ring.Complete || ring.Completeness < MinHealCompleteness) return 0;
            if (ring.Strokes.Count == 0 || isotropic.x <= 1e-6f || isotropic.y <= 1e-6f) return 0;

            var bins = new bool[ShareBins];
            foreach (var stroke in ring.Strokes)
                for (int i = 0; i < stroke.Points.Count; i++)
                    MarkBin(bins, ring.Center, stroke.Points[Mathf.Max(0, i - 1)], stroke.Points[i]);

            // Se recorre desde el primer bin cubierto para no partir un tramo vacio en el 0.
            int first = -1;
            for (int i = 0; i < ShareBins; i++) if (bins[i]) { first = i; break; }
            if (first < 0) return 0;

            float step = 360f / ShareBins;
            float width = ring.Strokes[0].Width;
            InkKind ink = ring.Strokes[0].Ink;
            int added = 0;
            int k = 0;
            while (k < ShareBins)
            {
                int bin = (first + k) % ShareBins;
                if (bins[bin]) { k++; continue; }
                int run = 0;
                while (k + run < ShareBins && !bins[(first + k + run) % ShareBins]) run++;
                // El tramo vacio ocupa [primer bin vacio, ultimo vacio + 1); se extiende 1,5 bins hacia cada lado para solapar.
                float from = (first + k - 1.5f) * step;
                float to = (first + k + run + 1.5f) * step;
                int pointCount = Mathf.Max(3, Mathf.CeilToInt((to - from) / (step * 0.5f)) + 1);
                var bridge = new Stroke { Width = width, Ink = ink };
                for (int i = 0; i < pointCount; i++)
                {
                    float angle = Mathf.Lerp(from, to, (float)i / (pointCount - 1));
                    Vector2 p = ring.Center + GeometryUtils.VectorFromAngleDeg(angle) * ring.Radius;
                    bridge.AddPoint(new Vector2(p.x / isotropic.x, p.y / isotropic.y));
                }
                merged.AddStroke(bridge);
                added++;
                k += run;
            }
            return added;
        }

        private static void MarkBin(bool[] bins, Vector2 center, Vector2 a, Vector2 b)
        {
            int from = AngleBin(center, a), to = AngleBin(center, b);
            int forward = (to - from + ShareBins) % ShareBins;
            if (forward <= ShareBins / 2)
                for (int k = 0; k <= forward; k++) bins[(from + k) % ShareBins] = true;
            else
                for (int k = 0; k <= ShareBins - forward; k++) bins[(to + k) % ShareBins] = true;
        }

        private static int AngleBin(Vector2 center, Vector2 p) => Mathf.Min(ShareBins - 1, (int)(GeometryUtils.AngleDeg(p - center) / 360f * ShareBins));
    }
}
