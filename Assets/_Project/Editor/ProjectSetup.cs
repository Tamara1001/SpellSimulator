using UnityEditor;
using UnityEngine;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Configuracion del proyecto que debe quedar versionada. Menu: SpellSimulator/Project/Fijar escenas del build.
    /// Tambien se puede correr en batch: Unity -batchmode -quit -projectPath . -executeMethod SpellSimulator.EditorTools.ProjectSetup.FixBuildScenes
    /// </summary>
    public static class ProjectSetup
    {
        /// <summary>Escenas del build: Pradera (indice 0, el juego) y Escena (laboratorio).</summary>
        public static readonly string[] BuildScenes =
        {
            "Assets/_Project/Scenes/Pradera.unity",
            "Assets/_Project/Scenes/Escena.unity",
        };

        [MenuItem("SpellSimulator/Project/Fijar escenas del build")]
        public static void FixBuildScenes()
        {
            var scenes = new EditorBuildSettingsScene[BuildScenes.Length];
            for (int i = 0; i < scenes.Length; i++) scenes[i] = new EditorBuildSettingsScene(BuildScenes[i], true);
            EditorBuildSettings.scenes = scenes;
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] Escenas del build: " + string.Join(", ", BuildScenes));
        }
    }
}
