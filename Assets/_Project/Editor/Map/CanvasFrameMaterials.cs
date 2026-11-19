using UnityEditor;
using UnityEngine;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>Materiales lisos de los muebles armados con primitivas (restocker, atril): se crean una vez con la API del editor y se reutilizan.</summary>
    public static class CanvasFrameMaterials
    {
        private const string Folder = "Assets/_Project/Materials";

        /// <summary>Material liso URP/Lit de un color (muebles armados con primitivas: restocker).</summary>
        public static Material Solid(string name, Color color)
        {
            string path = $"{Folder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
