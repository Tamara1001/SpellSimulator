using UnityEditor;
using UnityEngine;
using SpellSimulator.Canvas;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>Materiales del marco de cara de disparo (dorado / azul): se crean una vez con la API del editor y se reutilizan.</summary>
    public static class CanvasFrameMaterials
    {
        private const string Folder = "Assets/_Project/Materials";

        public static Material Gold => Get("CanvasFrameGold", CanvasRules.FrontFrame);
        public static Material Blue => Get("CanvasFrameBlue", CanvasRules.BackFrame);

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

        private static Material Get(string name, Color color)
        {
            string path = $"{Folder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(shader) { name = name };
            m.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
