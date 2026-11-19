using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Genera los assets SDF de TextMeshPro de las tipografias del proyecto (Docs/UI_ESTETICA.md). «Caveat Brush» (carteles), «Nunito» (HUD y lecturas) y «Lora» (libros y textos narrativos).
    /// Ejecutar: menu SpellSimulator/Fuentes/Generar tipografias (o llamar a <see cref="Build"/>).
    /// </summary>
    public static class FontBuilder
    {
        public const string SignFontPath = "Assets/_Project/Resources/Fonts/CaveatBrush SDF.asset";
        public const string BodyFontPath = "Assets/_Project/Resources/Fonts/Nunito SDF.asset";
        public const string BookFontPath = "Assets/_Project/Resources/Fonts/Lora SDF.asset";

        // ASCII imprimible + espanol (acentos, ñ, ¿ ¡) + signos usados en los carteles (·, ×, −, «, »).
        private const string Characters =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "áéíóúüñÁÉÍÓÚÜÑ¿¡·×−«»–—…°";

        [MenuItem("SpellSimulator/Fuentes/Generar tipografias")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources/Fonts")) AssetDatabase.CreateFolder("Assets/_Project/Resources", "Fonts");
            BuildOne("Assets/_Project/Art/Fonts/CaveatBrush-Regular.ttf", SignFontPath, "CaveatBrush");
            BuildOne("Assets/_Project/Art/Fonts/Nunito-Regular.ttf", BodyFontPath, "Nunito");
            BuildOne("Assets/_Project/Art/Fonts/Lora-Regular.ttf", BookFontPath, "Lora");
            SetTmpDefault();
        }

        private static void BuildOne(string sourcePath, string assetPath, string name)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (font == null) { Debug.LogError("[FontBuilder] falta " + sourcePath); return; }
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null) AssetDatabase.DeleteAsset(assetPath);

            var asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            asset.name = name + " SDF";
            AssetDatabase.CreateAsset(asset, assetPath);
            asset.atlasTexture.name = name + " Atlas";
            asset.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            // Lo que no tenga glifo (flechas, simbolos raros) cae a Liberation Sans en vez de mostrar un cuadrado.
            var liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (liberation != null) asset.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { liberation };
            asset.TryAddCharacters(Characters, out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning($"[FontBuilder] {name}: caracteres sin glifo: {missing}");
            // Estatico: el texto de juego no pide glifos nuevos en ejecucion (si hace falta otro caracter, agregarlo a Characters y regenerar).
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log("[FontBuilder] generado " + assetPath);
        }

        /// <summary>Nunito pasa a ser la fuente por defecto de TextMeshPro: todo texto sin fuente explicita (HUD 3D, laboratorio, rotulos) la usa.</summary>
        private static void SetTmpDefault()
        {
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset");
            var body = LoadBodyFont();
            if (settings == null || body == null) { Debug.LogWarning("[FontBuilder] no se pudo fijar la fuente por defecto de TMP"); return; }
            var so = new SerializedObject(settings);
            so.FindProperty("m_defaultFontAsset").objectReferenceValue = body;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        public static TMP_FontAsset LoadBodyFont() => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
        public static TMP_FontAsset LoadBookFont() => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BookFontPath);
        public static TMP_FontAsset LoadSignFont() => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SignFontPath);
    }
}
