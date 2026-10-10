using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.World
{
    /// <summary>
    /// Estructura fija (pared de la torre, cabana, muro de piedra...) que la tierra con el signo Crush puede romper (<see cref="MeshBreaker"/>).
    /// Al primer corte se arma una copia de la malla (hija "Corte", sin batching estatico) con su propio MeshCollider y se apagan el renderer y los
    /// colliders originales; los cortes siguientes sacan mas triangulos de esa copia. <see cref="Restore"/> lo deja como estaba (reinicio del mundo).
    /// El marcado lo hacen los constructores del mapa (editor); no se puede romper lo que lleva <see cref="Indestructible"/> (libros, portales).
    /// </summary>
    public sealed class Breakable : MonoBehaviour
    {
        public static readonly List<Breakable> All = new List<Breakable>();

        private MeshFilter filter;
        private MeshRenderer sourceRenderer;
        private Mesh cutMesh;
        private GameObject cutView;
        private MeshCollider cutCollider;
        private readonly List<Collider> disabled = new List<Collider>();
        private readonly List<int> kept = new List<int>();
        private Vector3[] worldVertices;
        private bool broken;

        public bool IsBroken => broken;
        public Bounds Bounds => sourceRenderer != null ? sourceRenderer.bounds : new Bounds(transform.position, Vector3.zero);

        private void OnEnable() => All.Add(this);

        private void OnDisable() => All.Remove(this);

        private void Awake()
        {
            filter = GetComponent<MeshFilter>();
            sourceRenderer = GetComponent<MeshRenderer>();
        }

        /// <summary>Saca los triangulos dentro de la esfera. Devuelve cuantos se sacaron (0 si no tocaba nada o la malla no se puede leer).</summary>
        public int Cut(Vector3 center, float radius, IList<Vector3> protectedPoints, float protectedRadius, List<Vector3> removedCentroids, int maxCentroids)
        {
            if (filter == null || filter.sharedMesh == null || sourceRenderer == null) return 0;
            if (!broken && !filter.sharedMesh.isReadable)
            {
                Debug.LogWarning($"[Romper] {name}: la malla no es legible, no se puede cortar.");
                return 0;
            }
            if (!broken) Begin();

            Matrix4x4 toWorld = cutView.transform.localToWorldMatrix;
            Vector3[] local = cutMesh.vertices;
            if (worldVertices == null || worldVertices.Length != local.Length) worldVertices = new Vector3[local.Length];
            for (int i = 0; i < local.Length; i++) worldVertices[i] = toWorld.MultiplyPoint3x4(local[i]);

            int removedTotal = 0;
            for (int s = 0; s < cutMesh.subMeshCount; s++)
            {
                int[] tris = cutMesh.GetTriangles(s);
                int removed = MeshCut.Select(worldVertices, tris, center, radius, protectedPoints, protectedRadius, kept, removedCentroids, maxCentroids);
                if (removed == 0) continue;
                cutMesh.SetTriangles(kept, s);
                removedTotal += removed;
            }
            if (removedTotal > 0)
            {
                cutMesh.RecalculateBounds();
                cutCollider.sharedMesh = null;
                cutCollider.sharedMesh = cutMesh; // vuelve a cocinar el collider con el hueco
            }
            return removedTotal;
        }

        private void Begin()
        {
            broken = true;
            cutMesh = Instantiate(filter.sharedMesh);
            cutMesh.name = filter.sharedMesh.name + "_Corte";
            cutView = new GameObject("Corte");
            cutView.transform.SetParent(transform, false);
            cutView.layer = gameObject.layer;
            cutView.AddComponent<MeshFilter>().sharedMesh = cutMesh;
            var view = cutView.AddComponent<MeshRenderer>();
            view.sharedMaterials = sourceRenderer.sharedMaterials;
            cutCollider = cutView.AddComponent<MeshCollider>();
            cutCollider.sharedMesh = cutMesh;

            // Lo que se ve es lo que choca: se apagan el renderer y todos los colliders propios (no los de libros, portales ni cuerpos sueltos).
            sourceRenderer.enabled = false;
            foreach (Collider c in GetComponentsInChildren<Collider>(true))
            {
                if (c == cutCollider || !c.enabled || c.isTrigger) continue;
                if (c.attachedRigidbody != null && c.attachedRigidbody.gameObject != gameObject) continue;
                if (c.GetComponentInParent<Indestructible>() != null) continue;
                if (c.GetComponentInParent<Breakable>() != this) continue;
                // Solo los colliders propios de la estructura (en su raiz o hijos "...Collider"), no los de cofres, tableros ni otros objetos.
                if (c.gameObject != gameObject && !c.gameObject.name.EndsWith("Collider")) continue;
                c.enabled = false;
                disabled.Add(c);
            }
        }

        /// <summary>Vuelve a la malla y los colliders originales.</summary>
        public void Restore()
        {
            if (!broken) return;
            broken = false;
            if (cutView != null) Destroy(cutView);
            if (cutMesh != null) Destroy(cutMesh);
            cutView = null;
            cutMesh = null;
            cutCollider = null;
            if (sourceRenderer != null) sourceRenderer.enabled = true;
            foreach (Collider c in disabled) if (c != null) c.enabled = true;
            disabled.Clear();
        }
    }

    /// <summary>Marca lo que ningun hechizo puede destruir ni dejar sin apoyo cerca: libros de los tutoriales, portales y sus placas.</summary>
    public sealed class Indestructible : MonoBehaviour
    {
        public static readonly List<Indestructible> All = new List<Indestructible>();

        private void OnEnable() => All.Add(this);

        private void OnDisable() => All.Remove(this);
    }
}
