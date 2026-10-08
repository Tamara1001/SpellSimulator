using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SpellSimulator.Casting;
using SpellSimulator.Glyphs;
using SpellSimulator.Learning;
using SpellSimulator.Player;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Equipa la escena abierta con el aprendizaje (M6): Discovery y Grimoire en SystemManagers, y el texto de ayuda
    /// bajo la mira en el Canvas de la HUD. Menu: SpellSimulator > Learning > Setup Current Scene.
    /// </summary>
    public static class LearningSetup
    {
        [MenuItem("SpellSimulator/Learning/Setup Current Scene")]
        public static string SetupCurrentScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var sys = GameObject.Find("SystemManagers");
            if (sys == null) return "Sin SystemManagers";

            var discovery = sys.GetComponent<Discovery>() ?? sys.AddComponent<Discovery>();
            var dso = new SerializedObject(discovery);
            dso.FindProperty("loreJson").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Lore/glifos.json");
            dso.FindProperty("caster").objectReferenceValue = sys.GetComponent<SpellCaster>();
            dso.ApplyModifiedProperties();

            const string coverPath = "Assets/_Project/Materials/GrimoireCover.mat";
            var cover = AssetDatabase.LoadAssetAtPath<Material>(coverPath);
            if (cover == null)
            {
                cover = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(cover, coverPath);
            }
            cover.SetColor("_BaseColor", new Color(0.32f, 0.18f, 0.42f));
            EditorUtility.SetDirty(cover);

            var grimoire = sys.GetComponent<Grimoire>() ?? sys.AddComponent<Grimoire>();
            var gso = new SerializedObject(grimoire);
            gso.FindProperty("input").objectReferenceValue = Object.FindFirstObjectByType<PlayerInputReader>();
            gso.FindProperty("dictionary").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GlyphDictionaryAsset>("Assets/_Project/Data/GlyphDictionary.asset");
            gso.FindProperty("pageMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Canvas.mat");
            gso.FindProperty("brushMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/BrushMaterial.mat");
            gso.FindProperty("coverMaterial").objectReferenceValue = cover;
            gso.ApplyModifiedProperties();

            // La HUD y los avisos los arma UISetup (UI Toolkit).
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return $"Aprendizaje en {scene.name}: grimorio={grimoire != null}";
        }
    }
}
