using UnityEngine;
using SpellSimulator.Core;

namespace SpellSimulator.Casting
{
    /// <summary>
    /// Sellos partidos (canon, SYSTEMS_DESIGN: SplitJoin): un sello dibujado a medias en dos objetos que se completa al
    /// juntarlos. Logica pura: cuando dos mitades quedan pegadas lado a lado (con la misma cara hacia afuera) sus trazos se
    /// fusionan en un solo espacio UV, la mitad izquierda ocupando [0, wL/(wL+wR)] y la derecha el resto.
    /// </summary>
    public static class SplitJoinMath
    {
        /// <summary>Tolerancia (m) con la que dos mitades cuentan como pegadas.</summary>
        public const float DefaultTolerance = 0.08f;

        /// <summary>
        /// Fusiona las dos sesiones (UV de cada mitad, [0,1]) en una sola sesion UV [0,1] del lienzo unido. widthLeft y
        /// widthRight son los anchos en metros de cada mitad (el alto es el mismo).
        /// </summary>
        public static DrawingSession Merge(DrawingSession left, float widthLeft, DrawingSession right, float widthRight)
        {
            var merged = new DrawingSession();
            float total = Mathf.Max(widthLeft + widthRight, 1e-6f);
            float splitAt = widthLeft / total;
            Append(merged, left, 0f, splitAt);
            Append(merged, right, splitAt, 1f - splitAt);
            return merged;
        }

        private static void Append(DrawingSession target, DrawingSession source, float offsetX, float scaleX)
        {
            if (source == null) return;
            foreach (var stroke in source.Strokes)
            {
                var copy = new Stroke { Width = stroke.Width };
                foreach (var p in stroke.Points) copy.AddPoint(new Vector2(offsetX + p.x * scaleX, p.y));
                target.AddStroke(copy);
            }
        }

        /// <summary>
        /// True si B esta pegada al lado derecho de A: B a un ancho de mitad de cada una sobre el eje derecho de A, sin
        /// desfase lateral grande y con la cara hacia afuera casi igual.
        /// </summary>
        public static bool IsRightNeighbour(Vector3 posA, Vector3 rightA, Vector3 faceA, float widthA,
            Vector3 posB, Vector3 faceB, float widthB, float tolerance = DefaultTolerance, float minFaceDot = 0.9f)
        {
            if (Vector3.Dot(faceA, faceB) < minFaceDot) return false;
            Vector3 d = posB - posA;
            float along = Vector3.Dot(d, rightA);
            float expected = (widthA + widthB) * 0.5f;
            if (Mathf.Abs(along - expected) > tolerance) return false;
            Vector3 lateral = d - rightA * along;
            return lateral.magnitude <= tolerance * 1.5f;
        }
    }
}
