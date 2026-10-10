using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.World
{
    /// <summary>
    /// Recorte de una malla por una esfera (logica pura, con test EditMode): saca los triangulos cuyo centroide cae dentro, salvo los que
    /// estan cerca de puntos protegidos (libros, portales). Lo usa <see cref="Breakable"/> para que la tierra con el signo Crush (Wall Breaker)
    /// abra huecos en estructuras fijas.
    /// </summary>
    public static class MeshCut
    {
        /// <summary>
        /// Copia en <paramref name="kept"/> los indices de los triangulos que quedan y en <paramref name="removedCentroids"/> (hasta
        /// <paramref name="maxCentroids"/>) el centroide de los que se sacaron. Devuelve cuantos triangulos se sacaron.
        /// <paramref name="worldVertices"/> ya esta en espacio mundo.
        /// </summary>
        public static int Select(IList<Vector3> worldVertices, IList<int> triangles, Vector3 center, float radius,
            IList<Vector3> protectedPoints, float protectedRadius, List<int> kept, List<Vector3> removedCentroids, int maxCentroids)
        {
            kept.Clear();
            float r2 = radius * radius, p2 = protectedRadius * protectedRadius;
            int removed = 0;
            for (int i = 0; i + 2 < triangles.Count; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                Vector3 centroid = (worldVertices[a] + worldVertices[b] + worldVertices[c]) * (1f / 3f);
                bool cut = (centroid - center).sqrMagnitude <= r2;
                if (cut && protectedPoints != null)
                    for (int p = 0; p < protectedPoints.Count; p++)
                        if ((centroid - protectedPoints[p]).sqrMagnitude <= p2) { cut = false; break; }
                if (!cut)
                {
                    kept.Add(a); kept.Add(b); kept.Add(c);
                    continue;
                }
                removed++;
                if (removedCentroids != null && removedCentroids.Count < maxCentroids) removedCentroids.Add(centroid);
            }
            return removed;
        }
    }
}
