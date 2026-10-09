using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SpellSimulator.World;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Agrega <see cref="WaterContainer"/> a los recipientes de la escena abierta (los props con Rigidbody cuyo nombre
    /// contiene "Bucket": hoy el balde Bucket_Water de la estacion de agua). Idempotente: si ya lo tiene, solo reajusta la forma.
    /// TowerProps lo llama al regenerar el mapa. Menu: SpellSimulator/Water/Setup Containers.
    /// </summary>
    public static class WaterContainerSetup
    {
        [MenuItem("SpellSimulator/Water/Setup Containers")]
        public static void SetupContainers()
        {
            int count = 0;
            foreach (var rb in Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (rb == null || rb.name.IndexOf("Bucket", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (EditorUtility.IsPersistent(rb.gameObject)) continue;
                Configure(rb.gameObject);
                count++;
            }
            if (count > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Agua] Recipientes configurados: {count}.");
        }

        /// <summary>Agrega (si falta) y ajusta el recipiente con la forma del balde de TowerProps (frustum 0.13-0.16 m, 0.28 m de alto).</summary>
        public static WaterContainer Configure(GameObject go)
        {
            if (go == null) return null;
            var container = go.GetComponent<WaterContainer>();
            if (container == null) container = Undo.AddComponent<WaterContainer>(go);
            var so = new SerializedObject(container);
            so.FindProperty("capacityLiters").floatValue = 10f;
            // El balde de la estacion empieza con agua (por eso se llama Bucket_Water).
            so.FindProperty("startLiters").floatValue = go.name.Contains("Water") ? 6f : 0f;
            so.FindProperty("maxExtraMass").floatValue = 8f;
            so.FindProperty("bottomHeight").floatValue = 0.03f;
            so.FindProperty("rimHeight").floatValue = 0.26f;
            so.FindProperty("bottomRadius").floatValue = 0.125f;
            so.FindProperty("openingRadius").floatValue = 0.145f;
            so.ApplyModifiedProperties();
            return container;
        }
    }
}
