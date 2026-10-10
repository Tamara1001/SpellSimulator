using System;
using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Reactions;

namespace SpellSimulator.World
{
    /// <summary>
    /// Wall Breaker: abre un hueco esferico en todas las estructuras <see cref="Breakable"/> que toca y deja escombros. Respeta lo
    /// <see cref="Indestructible"/> (libros, portales): no corta cerca de ellos. Registra cada corte para que la red lo repita en quien entra tarde.
    /// Se limpia con el reinicio del mundo.
    /// </summary>
    public static class MeshBreaker
    {
        /// <summary>Distancia (m) alrededor de un libro o portal donde nunca se corta.</summary>
        public const float ProtectedRadius = 1.6f;
        public const int MaxDebris = 120, DebrisPerCut = 14;
        public const float DebrisSeconds = 14f;
        /// <summary>Cortes guardados para quien entra tarde a una sala.</summary>
        public const int MaxLoggedCuts = 64;

        public struct CutRecord
        {
            public Vector3 Center;
            public float Radius;
        }

        /// <summary>(centro, radio) cada vez que se corta algo en esta maquina (la red lo anuncia).</summary>
        public static event Action<Vector3, float> Broke;

        public static readonly List<CutRecord> Log = new List<CutRecord>();

        private static readonly List<Vector3> protectedPoints = new List<Vector3>();
        private static readonly List<Vector3> centroids = new List<Vector3>();
        private static readonly Queue<GameObject> debris = new Queue<GameObject>();
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static bool subscribed;

        /// <summary>Corta la esfera. Devuelve los triangulos sacados (0 = no habia nada que romper).</summary>
        public static int Break(Vector3 center, float radius, bool announce = true)
        {
            Subscribe();
            protectedPoints.Clear();
            var safe = Indestructible.All;
            for (int i = 0; i < safe.Count; i++) if (safe[i] != null) protectedPoints.Add(safe[i].transform.position);

            centroids.Clear();
            int total = 0;
            Material material = null;
            for (int i = Breakable.All.Count - 1; i >= 0; i--)
            {
                Breakable breakable = Breakable.All[i];
                if (breakable == null) continue;
                if (breakable.Bounds.SqrDistance(center) > radius * radius) continue;
                int n = breakable.Cut(center, radius, protectedPoints, ProtectedRadius, centroids, DebrisPerCut);
                if (n > 0 && material == null && breakable.TryGetComponent(out MeshRenderer r)) material = r.sharedMaterial;
                total += n;
            }
            if (total == 0)
            {
                int near = 0;
                for (int i = 0; i < Breakable.All.Count; i++)
                    if (Breakable.All[i] != null && Breakable.All[i].Bounds.SqrDistance(center) <= radius * radius) near++;
                Debug.Log($"[Romper] sin cortes en {center} (radio {radius:F1}): {Breakable.All.Count} rompibles, {near} cerca, {protectedPoints.Count} protegidos");
                return 0;
            }

            SpawnDebris(material, center);
            if (Log.Count < MaxLoggedCuts) Log.Add(new CutRecord { Center = center, Radius = radius });
            if (announce) Broke?.Invoke(center, radius);
            return total;
        }

        /// <summary>Todo vuelve a su lugar (reinicio del mundo).</summary>
        public static void RestoreAll()
        {
            for (int i = 0; i < Breakable.All.Count; i++) if (Breakable.All[i] != null) Breakable.All[i].Restore();
            Log.Clear();
            while (debris.Count > 0)
            {
                GameObject go = debris.Dequeue();
                if (go != null) UnityEngine.Object.Destroy(go);
            }
        }

        private static void Subscribe()
        {
            if (subscribed) return;
            subscribed = true;
            MagicWorld.WorldReset += RestoreAll;
        }

        private static void SpawnDebris(Material material, Vector3 center)
        {
            var block = new MaterialPropertyBlock();
            for (int i = 0; i < centroids.Count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Escombro";
                float size = UnityEngine.Random.Range(0.14f, 0.34f);
                go.transform.SetPositionAndRotation(centroids[i], UnityEngine.Random.rotation);
                go.transform.localScale = new Vector3(size, size * UnityEngine.Random.Range(0.5f, 1f), size);
                var renderer = go.GetComponent<Renderer>();
                if (material != null) renderer.sharedMaterial = material;
                float shade = UnityEngine.Random.Range(0.45f, 0.7f);
                block.SetColor(BaseColorId, new Color(shade, shade * 0.92f, shade * 0.8f));
                renderer.SetPropertyBlock(block);
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 6f;
                Vector3 away = centroids[i] - center;
                rb.AddExplosionForce(6f, center, Mathf.Max(away.magnitude * 2f, 2f), 0.4f, ForceMode.Impulse);
                UnityEngine.Object.Destroy(go, DebrisSeconds);
                debris.Enqueue(go);
                while (debris.Count > MaxDebris)
                {
                    GameObject old = debris.Dequeue();
                    if (old != null) UnityEngine.Object.Destroy(old);
                }
            }
        }
    }
}
