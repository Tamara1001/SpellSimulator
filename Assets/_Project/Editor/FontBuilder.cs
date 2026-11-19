using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Genera los assets SDF de TextMeshPro de las tipografias del proyecto (Docs/UI_ESTETICA.md). Hoy: «Caveat Brush» para carteles y rotulos.
    /// Ejecutar: menu SpellSimulator/Fuentes/Generar tipografias (o llamar a <see cref="Build"/>).
    /// </summary>
    public static class FontBuilder
    {
        public const string SignFontPath = "Assets/_Project/Art/Fonts/CaveatBrush SDF.asset";
        private const string SourcePath = "Assets/_Project/Art/Fonts/CaveatBrush-Regular.ttf";

        // ASCII imprimible + espanol (acentos, ñ, ¿ ¡) + signos usados en los carteles (·, ×, −, «, »).
        private const string Characters =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "áéíóúüñÁÉÍÓÚÜÑ¿¡·×−«»–—…°";

        [MenuItem("SpellSimulator/Fuentes/Generar tipografias")]
        public static void Build()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
            if (font == null) { Debug.LogError("[FontBuilder] falta " + SourcePath); return; }
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SignFontPath) != null) AssetDatabase.DeleteAsset(SignFontPath);

            var asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            asset.name = "CaveatBrush SDF";
            AssetDatabase.CreateAsset(asset, SignFontPath);
            asset.atlasTexture.name = "CaveatBrush Atlas";
            asset.material.name = "CaveatBrush Material";
            AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            asset.TryAddCharacters(Characters, out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("[FontBuilder] caracteres sin glifo: " + missing);
            // Estatico: los carteles no cambian en juego, asi que no se necesita el atlas dinamico.
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log("[FontBuilder] generado " + SignFontPath);
        }

        public static TMP_FontAsset LoadSignFont() => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SignFontPath);
    }
}
