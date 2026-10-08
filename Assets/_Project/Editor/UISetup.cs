using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using SpellSimulator.Glyphs;
using SpellSimulator.Player;
using SpellSimulator.UI;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Equipa la escena abierta con la interfaz de UI Toolkit: genera los sprites, crea el PanelSettings (1920x1080,
    /// Scale With Screen Size) y el GameObject "UI" con UIDocument + HudController + BookReaderUI, y quita los restos de
    /// la HUD uGUI vieja (componentes faltantes en el Canvas). Idempotente. Menu: SpellSimulator > UI > Setup Current Scene.
    /// Lo llama MapBuilder.Build al final.
    /// </summary>
    public static class UISetup
    {
        public const string UiFolder = "Assets/_Project/UI";
        public const string PanelSettingsPath = UiFolder + "/SpellPanelSettings.asset";
        public const string ThemePath = UiFolder + "/SpellTheme.tss";
        public const string HudUxmlPath = UiFolder + "/Hud.uxml";
        public const string UssPath = UiFolder + "/SpellUI.uss";
        private const string DictionaryPath = "Assets/_Project/Data/GlyphDictionary.asset";
        private const string RootName = "UI";

        [MenuItem("SpellSimulator/UI/Setup Current Scene")]
        public static void SetupFromMenu() => Debug.Log("[UI] " + Apply());

        /// <summary>Solo regenera los sprites de la interfaz.</summary>
        [MenuItem("SpellSimulator/UI/Regenerate Sprites")]
        public static void RegenerateSprites() => UISpriteGenerator.GenerateAll();

        public static string Apply()
        {
            UISpriteGenerator.GenerateAll();
            // El USS resuelve sus url() al importarse: reimportarlo despues de (re)generar los sprites.
            AssetDatabase.ImportAsset(UssPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(ThemePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(HudUxmlPath, ImportAssetOptions.ForceUpdate);
            PanelSettings panel = EnsurePanelSettings();

            var go = GameObject.Find(RootName);
            if (go == null) go = new GameObject(RootName);
            var doc = go.GetComponent<UIDocument>() ?? go.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudUxmlPath);
            doc.sortingOrder = 10;

            var interactor = Object.FindFirstObjectByType<PlayerInteractor>();
            var hud = go.GetComponent<HudController>() ?? go.AddComponent<HudController>();
            var hso = new SerializedObject(hud);
            hso.FindProperty("interactor").objectReferenceValue = interactor;
            hso.ApplyModifiedPropertiesWithoutUndo();

            var reader = go.GetComponent<BookReaderUI>() ?? go.AddComponent<BookReaderUI>();
            var rso = new SerializedObject(reader);
            rso.FindProperty("dictionary").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GlyphDictionaryAsset>(DictionaryPath);
            rso.FindProperty("input").objectReferenceValue = Object.FindFirstObjectByType<PlayerInputReader>();
            rso.ApplyModifiedPropertiesWithoutUndo();

            int removed = RemoveOldHud();

            var scene = go.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return $"UI Toolkit en {scene.name}: HUD + lector de libros (jugador={(interactor != null)}, restos uGUI quitados={removed}).";
        }

        private static PanelSettings EnsurePanelSettings()
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelSettingsPath);
            }
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.sortingOrder = 10;
            panel.clearColor = false;
            EditorUtility.SetDirty(panel);
            return panel;
        }

        /// <summary>
        /// Quita del Canvas uGUI los componentes cuyo script se borro (InteractionPromptUI / PlayerHud). La mira y el
        /// panel de depuracion del Canvas siguen.
        /// </summary>
        private static int RemoveOldHud()
        {
            var canvas = GameObject.Find("Canvas");
            if (canvas == null) return 0;
            int removed = 0;
            foreach (var t in canvas.GetComponentsInChildren<Transform>(true))
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            return removed;
        }
    }
}
