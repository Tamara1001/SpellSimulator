using UnityEngine;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Matematica pura del overlay de las lentes (sin estado ni MonoBehaviour, testeable en EditMode). Trabaja sobre arrays que
    /// preasigna el llamador: cero allocations, apta para llamarse en cada refresco.
    /// </summary>
    public static class LabOverlayMath
    {
        /// <summary>
        /// Elige los N candidatos mas cercanos a <paramref name="center"/> dentro de <paramref name="radius"/> entre los primeros
        /// <paramref name="count"/> de <paramref name="positions"/>. N = min(outIndices.Length, scratchSqrDist.Length).
        /// Escribe en <paramref name="outIndices"/> los indices ordenados del mas cercano al mas lejano (y sus distancias al
        /// cuadrado en <paramref name="scratchSqrDist"/>) y devuelve cuantos eligio.
        /// </summary>
        public static int SelectNearest(Vector3 center, Vector3[] positions, int count, float radius, int[] outIndices,
            float[] scratchSqrDist)
        {
            if (positions == null || outIndices == null || scratchSqrDist == null || radius < 0f) return 0;
            int capacity = Mathf.Min(outIndices.Length, scratchSqrDist.Length);
            if (capacity <= 0) return 0;
            if (count > positions.Length) count = positions.Length;

            float maxSqr = radius * radius;
            int found = 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 d = positions[i] - center;
                float sqr = d.x * d.x + d.y * d.y + d.z * d.z;
                if (sqr > maxSqr) continue;
                if (found == capacity && sqr >= scratchSqrDist[found - 1]) continue;

                // Insercion ordenada: corre hacia la derecha los mas lejanos (descartando el ultimo si esta lleno).
                int slot = found < capacity ? found : capacity - 1;
                while (slot > 0 && scratchSqrDist[slot - 1] > sqr)
                {
                    scratchSqrDist[slot] = scratchSqrDist[slot - 1];
                    outIndices[slot] = outIndices[slot - 1];
                    slot--;
                }
                scratchSqrDist[slot] = sqr;
                outIndices[slot] = i;
                if (found < capacity) found++;
            }
            return found;
        }
    }
}
