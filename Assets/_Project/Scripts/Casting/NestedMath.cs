using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.Casting
{
    /// <summary>
    /// Geometria y potencia de los sellos compuestos (circulos magicos dentro de un gran circulo). Logica pura.
    /// Regla (decidida por Ale, 2026-10-09): el anillo EXTERIOR es un contenedor que amplifica; cada anillo INTERIOR
    /// cerrado es un hechizo completo; todos salen a la vez desde su posicion. Ver NestedSealReader.
    /// </summary>
    public static class NestedMath
    {
        /// <summary>Radio maximo de un interior respecto del exterior.</summary>
        public const float MaxInnerRadiusRatio = 0.6f;
        /// <summary>Margen (fraccion del radio exterior) que debe quedar entre un interior y la linea del exterior.</summary>
        public const float ContainmentMargin = 0.08f;
        /// <summary>Tolerancia para asignar un trazo a un interior: su centroide cae a menos de r * (1 + esto) del centro.</summary>
        public const float StrokeTolerance = 0.15f;
        public const int MinInner = 2;
        public const int MaxInner = 4;

        /// <summary>Peso del logaritmo de (radio exterior / mayor interior) en la amplificacion.</summary>
        public const float SizeGain = 0.5f;
        /// <summary>Peso del logaritmo de la cantidad de interiores en la amplificacion.</summary>
        public const float CountGain = 0.5f;
        public const float MaxAmplification = 2.5f;

        /// <summary>
        /// True si el circulo interior cabe entero dentro del exterior dejando un margen de
        /// <see cref="ContainmentMargin"/> x radio exterior hasta la linea del exterior.
        /// </summary>
        public static bool IsInside(Vector2 innerCenter, float innerRadius, Vector2 outerCenter, float outerRadius)
        {
            if (innerRadius <= 0f || outerRadius <= 0f) return false;
            float reach = Vector2.Distance(innerCenter, outerCenter) + innerRadius;
            return reach <= outerRadius * (1f - ContainmentMargin);
        }

        /// <summary>True si el interior es un candidato valido: chico respecto del exterior y contenido con margen.</summary>
        public static bool FitsAsInner(Vector2 innerCenter, float innerRadius, Vector2 outerCenter, float outerRadius)
        {
            return innerRadius <= outerRadius * MaxInnerRadiusRatio && IsInside(innerCenter, innerRadius, outerCenter, outerRadius);
        }

        /// <summary>Dos circulos se solapan si la distancia entre centros es menor que la suma de radios.</summary>
        public static bool Overlaps(Vector2 centerA, float radiusA, Vector2 centerB, float radiusB)
        {
            return Vector2.Distance(centerA, centerB) < radiusA + radiusB;
        }

        /// <summary>True si un punto (centroide de un trazo) pertenece al circulo con la tolerancia de <see cref="StrokeTolerance"/>.</summary>
        public static bool BelongsTo(Vector2 point, Vector2 center, float radius)
        {
            return Vector2.Distance(point, center) <= radius * (1f + StrokeTolerance);
        }

        /// <summary>Centroide de los puntos de un trazo (promedio simple). Vacio = (0,0).</summary>
        public static Vector2 Centroid(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count == 0) return Vector2.zero;
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < points.Count; i++) sum += points[i];
            return sum / points.Count;
        }

        /// <summary>
        /// Multiplicador de potencia que aporta el exterior a cada interior:
        /// <c>1 + SizeGain * ln(outer / maxInner) + CountGain * ln(n)</c>, acotado a [1, 2.5].
        /// - Mas grande el exterior respecto del mayor interior, mas potencia (el contenedor "concentra" mas tinta).
        /// - Mas interiores, mas potencia (cada circulo resuena con los demas).
        /// - Logaritmos: rendimientos decrecientes (duplicar el exterior suma ~0,35; pasar de 2 a 4 interiores, ~0,35).
        /// Ejemplos: interior al 60 % y 2 interiores = 1,60; interiores al 33 % y 4 = 2,24; el tope 2,5 llega con 4 interiores al 20 %.
        /// La tinta poderosa se aplica aparte (inkPower de cada interior x esta amplificacion).
        /// </summary>
        public static float Amplification(float outerRadius, IReadOnlyList<float> innerRadii)
        {
            if (outerRadius <= 0f || innerRadii == null || innerRadii.Count == 0) return 1f;
            float maxInner = 0f;
            for (int i = 0; i < innerRadii.Count; i++) maxInner = Mathf.Max(maxInner, innerRadii[i]);
            if (maxInner <= 0f) return 1f;

            float sizeTerm = SizeGain * Mathf.Log(Mathf.Max(1f, outerRadius / maxInner));
            float countTerm = CountGain * Mathf.Log(innerRadii.Count);
            return Mathf.Clamp(1f + sizeTerm + countTerm, 1f, MaxAmplification);
        }

        /// <summary>
        /// Escala visual que recibe cada interior por la amplificacion: raiz cuadrada (el area del efecto crece como la
        /// potencia; asi un 2,5x no hace un efecto 2,5 veces mas ancho).
        /// </summary>
        public static float EffectScaleFactor(float amplification) => Mathf.Sqrt(Mathf.Max(1f, amplification));
    }
}
