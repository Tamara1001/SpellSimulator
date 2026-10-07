using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.Glyphs
{
    /// <summary>
    /// Asset que agrupa los JSON del diccionario. Se regenera con el menu
    /// SpellSimulator/Dictionary/Sync From Reference.
    /// </summary>
    [CreateAssetMenu(menuName = "SpellSimulator/Glyph Dictionary", fileName = "GlyphDictionary")]
    public class GlyphDictionaryAsset : ScriptableObject
    {
        [SerializeField, Tooltip("glyph-class-to-idx.json del modelo.")]
        private TextAsset classMap;
        [SerializeField] private TextAsset[] sigils = new TextAsset[0];
        [SerializeField] private TextAsset[] signs = new TextAsset[0];

        private GlyphDictionary cached;

        public GlyphDictionary Dictionary => cached ??= Build();

        private GlyphDictionary Build()
        {
            var entries = new List<(string, GlyphKind)>();
            foreach (var t in sigils) if (t != null) entries.Add((t.text, GlyphKind.Sigil));
            foreach (var t in signs) if (t != null) entries.Add((t.text, GlyphKind.Sign));
            return GlyphDictionary.FromJson(classMap.text, entries);
        }

#if UNITY_EDITOR
        public void SetSources(TextAsset classMapAsset, TextAsset[] sigilAssets, TextAsset[] signAssets)
        {
            classMap = classMapAsset;
            sigils = sigilAssets;
            signs = signAssets;
            cached = null;
        }

        private void OnValidate() => cached = null;
#endif
    }
}
