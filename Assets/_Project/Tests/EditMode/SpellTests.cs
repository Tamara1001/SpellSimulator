using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using SpellSimulator.Learning;

namespace SpellSimulator.Tests
{
    /// <summary>Hechizos prearmados y de libros (Data/Spells, ADR-034): formato, ids, glifos y desbloqueo.</summary>
    public class SpellTests
    {
        private const string SpellDir = "Assets/_Project/Data/Spells";

        private static List<string> SpellJsons() =>
            AssetDatabase.FindAssets("t:TextAsset", new[] { SpellDir })
                .Select(g => AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(g)).text).ToList();

        private static SpellCatalog Catalog(List<string> errors = null) =>
            SpellCatalog.FromJsons(SpellJsons(), e => errors?.Add(e));

        private static GlyphLore Lore() =>
            GlyphLore.FromJson(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Lore/glifos.json").text);

        [Test]
        public void Catalog_LoadsEveryFile_WithoutErrorsOrRepeatedIds()
        {
            var errors = new List<string>();
            var catalog = Catalog(errors);
            CollectionAssert.IsEmpty(errors, string.Join("; ", errors));
            Assert.AreEqual(SpellJsons().Count, catalog.All.Count, "Hay un JSON que no se cargo.");
            Assert.GreaterOrEqual(catalog.All.Count, 6);
            Assert.AreEqual(catalog.All.Count, catalog.All.Select(s => s.Id).Distinct().Count());
        }

        [Test]
        public void EverySpell_HasFullFormat_AndUsesKnownGlyphs()
        {
            var dictionary = TestAssets.Dictionary;
            var lore = Lore();
            foreach (var spell in Catalog().All)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(spell.Name), spell.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(spell.Description), spell.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(spell.HowTo), spell.Id);
                Assert.IsTrue(new[] { "fire", "water", "wind", "earth", "light" }.Contains(spell.Element), $"{spell.Id}: elemento '{spell.Element}'.");
                Assert.That(spell.Difficulty, Is.InRange(1, 3), spell.Id);
                Assert.IsNotEmpty(spell.Sigil, spell.Id);
                foreach (var g in spell.GlyphIds())
                {
                    Assert.IsTrue(lore.Has(g), $"{spell.Id}: '{g}' no esta en glifos.json.");
                    Assert.Greater(dictionary.Get(g).TemplateStrokes.Count, 0, $"{spell.Id}: '{g}' no tiene plantilla en el diccionario.");
                }
            }
        }

        [Test]
        public void InitialSpells_AreUnlockedFromTheStart_AndBookSpellsNeedTheirBook()
        {
            var catalog = Catalog();
            Assert.IsTrue(catalog.Get("gust").IsInitial);
            Assert.IsTrue(catalog.Get("water_jet").IsInitial);

            var unlocked = new List<SpellDef>();
            catalog.Unlocked(_ => false, unlocked);
            CollectionAssert.AreEquivalent(new[] { "gust", "water_jet" }, unlocked.Select(s => s.Id).ToArray());

            catalog.Unlocked(book => book == "spells-wind-water", unlocked);
            var ids = unlocked.Select(s => s.Id).ToList();
            CollectionAssert.IsSupersetOf(ids, new[] { "gust", "water_jet", "wind_fan", "light_rain", "whirl" });
            CollectionAssert.DoesNotContain(ids, "small_flame");
        }

        [Test]
        public void SpellBooks_ListExistingSpells_AndEveryBookSpellHasItsBook()
        {
            var catalog = Catalog();
            var lore = Lore();
            foreach (var book in lore.Books)
                foreach (var id in book.Spells)
                {
                    var spell = catalog.Get(id);
                    Assert.IsNotNull(spell, $"El libro '{book.Id}' enseña '{id}', que no existe.");
                    Assert.AreEqual(book.Id, spell.Book, $"'{id}' dice que lo enseña otro libro.");
                }
            foreach (var spell in catalog.All.Where(s => !s.IsInitial))
            {
                var book = lore.Book(spell.Book);
                Assert.IsNotNull(book, $"{spell.Id}: no existe el libro '{spell.Book}'.");
                CollectionAssert.Contains(book.Spells, spell.Id);
            }
            Assert.IsNotNull(lore.Book("spells-wind-water"));
            Assert.IsNotNull(lore.Book("spells-fire-earth"));
        }

        [Test]
        public void Lore_StartingGlyphs_AreUnchanged()
        {
            CollectionAssert.AreEqual(new[] { "magic_circle", "fire", "water", "wind-directs-air", "earth" }, Lore().Starting);
        }

        [Test]
        public void Builder_DrawsSigilSignsAndAnAlmostClosedRingLast()
        {
            var dictionary = TestAssets.Dictionary;
            var gust = Catalog().Get("gust");
            var strokes = SpellStrokeBuilder.Build(gust, id => dictionary.Get(id).TemplateStrokes);
            int expected = dictionary.Get(gust.Sigil).TemplateStrokes.Count
                + gust.Signs.Sum(s => dictionary.Get(s.Id).TemplateStrokes.Count) + 1;
            Assert.AreEqual(expected, strokes.Count);

            var ring = strokes[strokes.Count - 1];
            foreach (var p in ring) Assert.AreEqual(1f, p.magnitude, 1e-3f);
            // Casi cerrado: queda un hueco (no es un anillo cerrado) pero chico.
            float gap = Vector2.Distance(ring[0], ring[ring.Count - 1]);
            Assert.That(gap, Is.GreaterThan(0.2f).And.LessThan(0.8f));

            foreach (var stroke in strokes.Take(strokes.Count - 1))
                foreach (var p in stroke) Assert.Less(p.magnitude, 1f, "Un glifo se sale del anillo.");
        }

        [Test]
        public void Builder_TurnedSign_PointsTheOtherWay()
        {
            var dictionary = TestAssets.Dictionary;
            Func<string, List<List<Vector2>>> templateOf = id => dictionary.Get(id).TemplateStrokes;
            SpellDef Make(float turn)
            {
                var def = new SpellDef { Id = "t", Name = "t", Sigil = "water" };
                def.Signs.Add(new SpellSign("column", 0f, turn));
                return def;
            }
            var inward = SpellStrokeBuilder.Build(Make(0f), templateOf);
            var outward = SpellStrokeBuilder.Build(Make(180f), templateOf);
            Assert.AreEqual(inward.Count, outward.Count);
            // Mismo lugar sobre el anillo, pero la plantilla girada media vuelta: los puntos cambian.
            int first = dictionary.Get("water").TemplateStrokes.Count;
            Assert.AreNotEqual(inward[first][0], outward[first][0]);
        }

        [Test]
        public void Parser_RejectsMissingId_AndAcceptsExplicitStrokes()
        {
            Assert.Throws<FormatException>(() => SpellDef.FromJson("{ \"name\": \"Sin id\" }"));
            var def = SpellDef.FromJson("{ \"id\": \"x\", \"name\": \"X\", \"book\": \"inicial\", \"strokes\": [ [ [0,0], [0.5,0.5] ] ] }");
            Assert.IsTrue(def.IsInitial);
            Assert.AreEqual(1, def.Strokes.Count);
            Assert.AreEqual(2, SpellStrokeBuilder.Build(def, _ => null)[0].Count);

            var errors = new List<string>();
            var catalog = SpellCatalog.FromJsons(new[] { "{ \"id\": \"a\", \"name\": \"A\" }", "{ \"id\": \"a\", \"name\": \"B\" }", "no es json" }, errors.Add);
            Assert.AreEqual(1, catalog.All.Count);
            Assert.AreEqual(2, errors.Count);
        }

        [Test]
        public void StampCompound_FromSpell_KeepsGapAndFitsRadius()
        {
            var def = new SpellDef { Id = "t", Name = "T", Sigil = "s", GapDeg = 34f };
            def.Signs.Add(new SpellSign("g", 270f, 0f));
            var square = new List<List<Vector2>> { new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 1f) } };
            var ring = SpellStrokeBuilder.Build(def, id => square);
            Assert.AreEqual(3, ring.Count, "sigilo + signo + anillo");

            var size = new Vector2(2f, 1f);
            var strokes = Casting.StampPattern.Compound(ring, new Vector2(0.5f, 0.5f), 0.25f, size, 0.01f);
            Assert.AreEqual(ring.Count, strokes.Count);
            var circle = strokes[strokes.Count - 1];
            foreach (var p in circle.Points)
                Assert.AreEqual(0.25f, Vector2.Distance(Vector2.Scale(p - new Vector2(0.5f, 0.5f), size), Vector2.zero), 1e-3f);
            // Hueco arriba: ningun punto del anillo cae en el centro del hueco.
            foreach (var p in circle.Points)
                Assert.Greater(Vector2.Angle(Vector2.Scale(p - new Vector2(0.5f, 0.5f), size), Vector2.up), 15f);
        }

        [Test]
        public void StampPatternDiameter_IsScaledAndClampedToCanvas()
        {
            Assert.AreEqual(0.2f * World.Stamp.PatternScale, World.Stamp.PatternDiameter(0.2f, new Vector2(5f, 5f)), 1e-4f);
            Assert.AreEqual(World.Stamp.MaxCanvasFill * 0.3f, World.Stamp.PatternDiameter(0.2f, new Vector2(5f, 0.3f)), 1e-4f);
        }
    }
}
