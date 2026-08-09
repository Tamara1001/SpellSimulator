using UnityEngine;
using System.Collections.Generic;

namespace SpellSimulator.Core
{
    public class GroupedSession
    {
        public DrawingSession Sigil = new DrawingSession();
        public List<DrawingSession> Signs = new List<DrawingSession>();
    }

    public static class StrokeGrouper
    {
        /// <summary>
        /// Separa los trazos en Sigilo (interior) y Signos (exterior) basandose en el Anillo.
        /// Ademas agrupa trazos exteriores que esten muy cerca entre si.
        /// </summary>
        public static GroupedSession Group(DrawingSession session, RingInfo ring)
        {
            var result = new GroupedSession();

            if (!ring.Found)
            {
                foreach (var stroke in session.Strokes)
                    result.Sigil.AddStroke(stroke);
                return result;
            }

            // Umbral ajustado: 55% del radio para considerarse sigilo central.
            // Asi evitamos que signos dibujados en los bordes se mezclen con el sigilo.
            float innerThreshold = ring.Radius * 0.55f;
            float clusterTolerance = ring.Radius * 0.20f; // 20% del radio de tolerancia para fusionar signos

            List<DrawingSession> ungroupedSigns = new List<DrawingSession>();

            foreach (var stroke in session.Strokes)
            {
                if (stroke == ring.Stroke) continue;

                Vector2 centerOfMass = GetCenterOfMass(stroke);
                float distToCenter = Vector2.Distance(centerOfMass, ring.Center);

                if (distToCenter <= innerThreshold)
                {
                    result.Sigil.AddStroke(stroke);
                }
                else
                {
                    // Es un signo exterior. Lo ponemos en su propio contenedor temporal
                    var tempSession = new DrawingSession();
                    tempSession.AddStroke(stroke);
                    ungroupedSigns.Add(tempSession);
                }
            }

            // Algoritmo simple de Clustering para fusionar signos superpuestos/cercanos
            result.Signs = ClusterSigns(ungroupedSigns, clusterTolerance);

            return result;
        }

        private static List<DrawingSession> ClusterSigns(List<DrawingSession> signs, float tolerance)
        {
            List<DrawingSession> clusters = new List<DrawingSession>();

            foreach (var sign in signs)
            {
                bool added = false;
                sign.GetBounds(out Vector2 minA, out Vector2 maxA);

                foreach (var cluster in clusters)
                {
                    cluster.GetBounds(out Vector2 minB, out Vector2 maxB);

                    // Verificar superposicion de bounding boxes expandidos
                    bool overlapX = minA.x <= maxB.x + tolerance && maxA.x >= minB.x - tolerance;
                    bool overlapY = minA.y <= maxB.y + tolerance && maxA.y >= minB.y - tolerance;

                    if (overlapX && overlapY)
                    {
                        // Fusionar: agregar todos los trazos al cluster
                        foreach (var s in sign.Strokes) cluster.AddStroke(s);
                        added = true;
                        break;
                    }
                }

                if (!added)
                {
                    clusters.Add(sign);
                }
            }

            return clusters;
        }

        private static Vector2 GetCenterOfMass(Stroke stroke)
        {
            if (stroke.Points.Count == 0) return Vector2.zero;
            Vector2 sum = Vector2.zero;
            foreach (var p in stroke.Points) sum += p;
            return sum / stroke.Points.Count;
        }
    }
}
