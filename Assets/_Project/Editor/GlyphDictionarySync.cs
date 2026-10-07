using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using SpellSimulator.Glyphs;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Copia diccionario y modelo desde el repo de referencia (wha-spell-simulator) y regenera el
    /// GlyphDictionary.asset. La ruta se guarda en EditorPrefs; por defecto es ../../wha-spell-simulator.
    /// </summary>
    public static class GlyphDictionarySync
    {
        private const string PathPrefKey = "SpellSimulator.ReferenceRepoPath";
        private const string DataFolder = "Assets/_Project/Data";
        private const string DictionaryFolder = DataFolder + "/Dictionary";
        public const string AssetPath = DataFolder + "/GlyphDictionary.asset";

        public static string ReferencePath
        {
            get => EditorPrefs.GetString(PathPrefKey, Path.GetFullPath(Path.Combine(Application.dataPath, "../../../wha-spell-simulator")));
            set => EditorPrefs.SetString(PathPrefKey, value);
        }

        [MenuItem("SpellSimulator/Dictionary/Sync From Reference")]
        public static void SyncFromMenu()
        {
            if (!Directory.Exists(ReferencePath))
            {
                string picked = EditorUtility.OpenFolderPanel("Ubicar wha-spell-simulator", "", "");
                if (string.IsNullOrEmpty(picked)) return;
                ReferencePath = picked;
            }
            Debug.Log(Sync(ReferencePath, copyModel: true));
        }

        [MenuItem("SpellSimulator/Dictionary/Rebuild Asset (sin copiar)")]
        public static void RebuildFromMenu() => Debug.Log(RebuildAsset());

        /// <summary>Copia JSON del diccionario (y opcionalmente el modelo) y regenera el asset.</summary>
        public static string Sync(string referencePath, bool copyModel)
        {
            string source = Path.Combine(referencePath, "src/lib/dictionary");
            int copied = 0;
            foreach (string kind in new[] { "sigils", "signs" })
            {
                string from = Path.Combine(source, kind);
                string to = Path.Combine(DictionaryFolder, kind);
                Directory.CreateDirectory(to);
                foreach (string file in Directory.GetFiles(from, "*.json"))
                {
                    File.Copy(file, Path.Combine(to, Path.GetFileName(file)), true);
                    copied++;
                }
            }

            if (copyModel)
            {
                string models = Path.Combine(referencePath, "static/models");
                foreach (string file in new[] { "glyph-recognizer.onnx", "glyph-recognizer.onnx.data", "glyph-class-to-idx.json" })
                    File.Copy(Path.Combine(models, file), Path.Combine(DataFolder, file), true);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return $"[Diccionario] {copied} glifos copiados{(copyModel ? " + modelo" : "")}. {RebuildAsset()}";
        }

        public static string RebuildAsset()
        {
            var classMap = AssetDatabase.LoadAssetAtPath<TextAsset>(DataFolder + "/glyph-class-to-idx.json");
            TextAsset[] Load(string kind) => AssetDatabase.FindAssets("t:TextAsset", new[] { $"{DictionaryFolder}/{kind}" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<TextAsset>)
                .ToArray();

            var asset = AssetDatabase.LoadAssetAtPath<GlyphDictionaryAsset>(AssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<GlyphDictionaryAsset>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }

            TextAsset[] sigils = Load("sigils"), signs = Load("signs");
            asset.SetSources(classMap, sigils, signs);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            var dictionary = asset.Dictionary;
            return $"Asset con {sigils.Length} sigilos y {signs.Length} signos; el modelo tiene {dictionary.ClassCount} clases.";
        }
    }
}
