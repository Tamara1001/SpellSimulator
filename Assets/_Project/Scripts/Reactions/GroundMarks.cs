using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Compiler;

namespace SpellSimulator.Reactions
{
    /// <summary>
    /// Marcas que dejan los hechizos en el suelo (SYSTEMS_DESIGN 5, "Suelo"): crater con rocas que emergen (tierra),
    /// quemadura (fuego) y charco que se seca (agua). Quads con el shader GroundMark; las rocas son piedra reactiva
    /// (el pull de tierra las atrae una vez sueltas). Presupuestos fijos: lo mas viejo se borra primero.
    /// </summary>
    public static class GroundMarks
    {
        public const int MaxMarks = 24;
        public const int MaxRocks = 30;
        public const float PuddleSeconds = 20f;

        /// <summary>(elemento, punto) cada vez que un hechizo marca el suelo.</summary>
        public static event System.Action<ElementType, Vector3> Marked;

        private static readonly Queue<GameObject> marks = new Queue<GameObject>();
        private static readonly Queue<GameObject> rocks = new Queue<GameObject>();
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int RimId = Shader.PropertyToID("_Rim");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");

        /// <summary>Deja la marca del elemento. radius en metros. Devuelve false si el elemento no marca el suelo.</summary>
        public static bool Mark(ElementType element, Vector3 point, Vector3 normal, float radius, Material markMaterial,
            Material rockMaterial, MaterialProfile rockProfile)
        {
            if (markMaterial == null) return false;
            if (element == ElementType.Earth || element == ElementType.Fire || element == ElementType.Water) Marked?.Invoke(element, point);
            switch (element)
            {
                case ElementType.Earth:
                    Spawn(point, normal, radius * 1.2f, markMaterial, new Color(0.18f, 0.13f, 0.09f, 0.85f), 1f, 0f);
                    SpawnRocks(point, normal, radius, rockMaterial, rockProfile);
                    return true;
                case ElementType.Fire:
                    Spawn(point, normal, radius, markMaterial, new Color(0.05f, 0.04f, 0.035f, 0.75f), 0f, 0f);
                    return true;
                case ElementType.Water:
                    Spawn(point, normal, radius * 1.3f, markMaterial, new Color(0.15f, 0.25f, 0.32f, 0.45f), 0f, PuddleSeconds);
                    return true;
                default:
                    return false;
            }
        }

        public static void ClearAll()
        {
            while (marks.Count > 0) { var m = marks.Dequeue(); if (m != null) Object.Destroy(m); }
            while (rocks.Count > 0) { var r = rocks.Dequeue(); if (r != null) Object.Destroy(r); }
        }

        private static void Spawn(Vector3 point, Vector3 normal, float radius, Material material, Color color, float rim, float fadeSeconds)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "GroundMark";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetPositionAndRotation(point + normal * 0.005f,
                Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
            go.transform.localScale = Vector3.one * radius * 2f;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            block.SetFloat(RimId, rim);
            block.SetFloat(SeedId, Random.Range(0f, 100f));
            r.SetPropertyBlock(block);
            if (fadeSeconds > 0f) go.AddComponent<MarkFade>().Init(fadeSeconds);
            Track(marks, go, MaxMarks);
        }

        private static void SpawnRocks(Vector3 point, Vector3 normal, float radius, Material material, MaterialProfile profile)
        {
            if (material == null) return;
            int count = Random.Range(3, 6);
            for (int i = 0; i < count; i++)
            {
                Vector2 ring = Random.insideUnitCircle.normalized * radius * Random.Range(0.6f, 1f);
                Vector3 tangent = Vector3.Cross(normal, Vector3.forward).sqrMagnitude > 1e-3f ? Vector3.Cross(normal, Vector3.forward).normalized : Vector3.right;
                Vector3 bitangent = Vector3.Cross(normal, tangent);
                Vector3 at = point + tangent * ring.x + bitangent * ring.y;
                float size = radius * Random.Range(0.25f, 0.45f);

                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Rock";
                go.transform.localScale = new Vector3(size, size * Random.Range(1.4f, 2.4f), size);
                // Inclinadas hacia afuera del crater, como empujadas desde abajo.
                Vector3 outward = (at - point).normalized;
                go.transform.rotation = Quaternion.FromToRotation(Vector3.up, (normal + outward * 0.5f).normalized) * Quaternion.Euler(0f, Random.Range(0f, 90f), 0f);
                go.GetComponent<Renderer>().sharedMaterial = material;
                var rb = go.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.mass = size * size * size * 2500f;
                if (profile != null)
                {
                    var reactive = go.AddComponent<ElementReactive>();
                    reactive.SetProfile(profile);
                }
                go.AddComponent<RockRise>().Init(at, go.transform.rotation, go.transform.localScale.y);
                Track(rocks, go, MaxRocks);
            }
        }

        private static void Track(Queue<GameObject> queue, GameObject go, int max)
        {
            queue.Enqueue(go);
            while (queue.Count > max) { var old = queue.Dequeue(); if (old != null) Object.Destroy(old); }
        }
    }
}
