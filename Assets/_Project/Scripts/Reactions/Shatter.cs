using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.Reactions
{
    /// <summary>
    /// Fractura simple: reemplaza el objeto por una grilla de bloques (n x n x n) con su material y color, que salen
    /// despedidos. Estilizado y barato; la fractura pre-calculada (Voronoi) para mesas y paredes queda para despues.
    /// Hay un presupuesto global de fragmentos: los mas viejos se borran primero.
    /// </summary>
    public static class Shatter
    {
        public const int MaxFragments = 160;
        public const float FragmentLifetime = 25f;

        private static readonly Queue<GameObject> fragments = new Queue<GameObject>();
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public static void Break(ElementReactive target, Vector3 impulseOrigin, float impulse)
        {
            var source = target.GetComponentInChildren<Renderer>();
            Transform t = source != null ? source.transform : target.transform;
            var filter = t.GetComponent<MeshFilter>();
            Bounds local = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            int n = target.Profile != null ? Mathf.Clamp(target.Profile.fragmentsPerAxis, 1, 4) : 2;

            Vector3 lossy = t.lossyScale;
            Vector3 piece = Vector3.Scale(local.size, lossy) / n;
            float mass = Mathf.Max(target.Mass / (n * n * n), 0.05f);
            Color tint = target.Tint(source != null && source.sharedMaterial != null && source.sharedMaterial.HasProperty(BaseColorId)
                ? source.sharedMaterial.GetColor(BaseColorId) : Color.white);
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, tint);

            for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
            for (int z = 0; z < n; z++)
            {
                Vector3 cell = local.min + Vector3.Scale(local.size, new Vector3((x + 0.5f) / n, (y + 0.5f) / n, (z + 0.5f) / n));
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"{target.name}_Fragment";
                go.transform.SetPositionAndRotation(t.TransformPoint(cell), t.rotation);
                go.transform.localScale = piece * 0.96f;
                var r = go.GetComponent<Renderer>();
                if (source != null) r.sharedMaterial = source.sharedMaterial;
                r.SetPropertyBlock(block);
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = mass;
                rb.AddExplosionForce(impulse, impulseOrigin, piece.magnitude * n * 2f, 0.3f, ForceMode.Impulse);
                Track(go);
            }
            target.gameObject.SetActive(false);
        }

        /// <summary>Borra todos los fragmentos (reinicio del mundo).</summary>
        public static void ClearAll()
        {
            while (fragments.Count > 0)
            {
                var go = fragments.Dequeue();
                if (go != null) Object.Destroy(go);
            }
        }

        private static void Track(GameObject fragment)
        {
            Object.Destroy(fragment, FragmentLifetime);
            fragments.Enqueue(fragment);
            while (fragments.Count > MaxFragments)
            {
                var old = fragments.Dequeue();
                if (old != null) Object.Destroy(old);
            }
        }
    }
}
