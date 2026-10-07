using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using SpellSimulator.Learning;

namespace SpellSimulator.Tests
{
    /// <summary>Descubrimiento de glifos (DiscoveryState) y textos del grimorio (GlyphLore).</summary>
    public class LearningTests
    {
        private static GlyphLore Lore() =>
            GlyphLore.FromJson(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Lore/glifos.json").text);

        [Test]
        public void Lore_CoversEveryDictionaryGlyph_AndBooksTeachKnownGlyphs()
        {
            var lore = Lore();
            var dictionary = TestAssets.Dictionary;
            foreach (var entry in dictionary.Entries)
            {
                if (entry.TemplateStrokes.Count == 0) continue;
                Assert.IsTrue(lore.Has(entry.Id), $"Falta el texto de '{entry.Id}' en glifos.json.");
            }
            foreach (var book in lore.Books)
                foreach (var g in book.Glyphs)
                    Assert.IsTrue(lore.Has(g), $"El libro '{book.Id}' enseña '{g}', que no existe.");
            CollectionAssert.IsNotEmpty(lore.Starting);
        }

        [Test]
        public void EveryGlyph_IsTaughtByABook_OrKnownFromTheStart()
        {
            var lore = Lore();
            var learnable = lore.Starting.Concat(lore.Books.SelectMany(b => b.Glyphs)).ToHashSet();
            foreach (var id in lore.Ids) Assert.IsTrue(learnable.Contains(id), $"'{id}' no se puede aprender en ningun libro.");
        }

        [Test]
        public void State_DiscoversOnce_AndRoundTrips()
        {
            var state = DiscoveryState.Starting(new[] { "fire", "column" });
            Assert.IsTrue(state.Discover("water"));
            Assert.IsFalse(state.Discover("water"), "Descubrir dos veces no cuenta.");
            Assert.IsTrue(state.MarkRead("elements"));

            var back = DiscoveryState.FromJson(state.ToJson());
            Assert.AreEqual(3, back.GlyphCount);
            Assert.IsTrue(back.Knows("water"));
            Assert.IsTrue(back.HasRead("elements"));
            Assert.IsFalse(back.Knows("earth"));
        }
    }
}
