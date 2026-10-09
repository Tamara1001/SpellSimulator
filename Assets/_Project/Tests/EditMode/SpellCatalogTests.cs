using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using SpellSimulator.Compiler;
using SpellSimulator.Core;
using SpellSimulator.Glyphs;
using SpellSimulator.Learning;
using SpellSimulator.Parsing;
using SpellSimulator.Recognition;

namespace SpellSimulator.Tests
{
    /// <summary>Catalogo de hechizos: busqueda por composicion (MatchCast), pistas, unicidad y validez de cada hechizo prearmado.</summary>
    public class SpellCatalogTests
    {
        private const string SpellDir = "Assets/_Project/Data/Spells";
        private static readonly Vector2 C = new Vector2(0.5f, 0.5f);

        private static List<string> SpellPaths() =>
            AssetDatabase.FindAssets("t:TextAsset", new[] { SpellDir }).Select(AssetDatabase.GUIDToAssetPath).ToList();

        private static SpellCatalog Catalog() =>
            SpellCatalog.FromJsons(SpellPaths().Select(p => AssetDatabase.LoadAssetAtPath<TextAsset>(p).text));

        private static SpellCatalog Mini()
        {
            return SpellCatalog.FromJsons(new[]
            {
                "{ \"id\": \"a\", \"name\": \"A\", \"sigil\": \"fire\", \"hint\": \"Se dice que A.\", \"signs\": [ {\"id\":\"column\",\"angle\":0}, {\"id\":\"pull\",\"angle\":180} ] }",
                "{ \"id\": \"b\", \"name\": \"B\", \"sigil\": \"wind\", \"difficulty\": 1, \"signs\": [ {\"id\":\"column\",\"angle\":0,\"turn\":0}, {\"id\":\"column\",\"angle\":180,\"turn\":0} ] }",
                "{ \"id\": \"c\", \"name\": \"C\", \"sigil\": \"wind\", \"difficulty\": 2, \"signs\": [ {\"id\":\"column\",\"angle\":0,\"turn\":180}, {\"id\":\"column\",\"angle\":180,\"turn\":180} ] }",
            });
        }

        // ------------------------------------------------------------------ MatchCast

        [Test]
        public void MatchCast_IgnoresSignOrder_ButNotMultiplicityNorSigil()
        {
            var catalog = Mini();
            Assert.AreEqual("a", catalog.MatchCast("fire", new[] { "pull", "column" }).Id);
            Assert.AreEqual("a", catalog.MatchCast("fire", new[] { "column", "pull" }).Id);
            Assert.IsNull(catalog.MatchCast("water", new[] { "column", "pull" }), "otro sigilo");
            Assert.IsNull(catalog.MatchCast("fire", new[] { "column", "column" }), "otro multiconjunto");
            Assert.IsNull(catalog.MatchCast("fire", new[] { "column" }), "faltan signos");
            Assert.IsNull(catalog.MatchCast("fire", new[] { "column", "pull", "pull" }), "sobran signos");
            Assert.IsNull(catalog.MatchCast(null, new[] { "column" }));
        }

        [Test]
        public void MatchCast_SameCompositionIsDisambiguatedByTwist()
        {
            var catalog = Mini();
            var signs = new[] { "column", "column" };
            Assert.AreEqual("b", catalog.MatchCast("wind", signs).Id, "sin giros gana el primero del catalogo");
            Assert.AreEqual("b", catalog.MatchCast("wind", signs, new[] { 3f, -5f }).Id);
            Assert.AreEqual("c", catalog.MatchCast("wind", signs, new[] { 178f, -175f }).Id);
        }

        [Test]
        public void RealCatalog_EverySpell_MatchesItself()
        {
            var catalog = Catalog();
            foreach (var spell in catalog.All)
            {
                var ids = spell.Signs.Select(s => s.Id).Reverse().ToList();
                var twists = spell.Signs.Select(s => s.TurnDeg).Reverse().ToList();
                var match = catalog.MatchCast(spell.Sigil, ids, twists);
                Assert.IsNotNull(match, spell.Id);
                Assert.AreEqual(spell.Id, match.Id, $"{spell.Id} se confunde con {match.Id}.");
            }
        }

        [Test]
        public void Hint_ReturnsTheSpellHint_OrEmpty()
        {
            var catalog = Mini();
            Assert.AreEqual("Se dice que A.", catalog.Hint("a"));
            Assert.AreEqual("", catalog.Hint("b"));
            Assert.AreEqual("", catalog.Hint("no-existe"));
        }

        [Test]
        public void IsUnlocked_CountsDiscoveredSpells()
        {
            var catalog = Mini();
            var a = catalog.Get("a");
            a.Book = "libro";
            Assert.IsFalse(SpellCatalog.IsUnlocked(a, _ => false, _ => false));
            Assert.IsTrue(SpellCatalog.IsUnlocked(a, _ => false, id => id == "a"));
            Assert.IsTrue(SpellCatalog.IsUnlocked(a, b => b == "libro", null));
        }

        // ------------------------------------------------------------------ catalogo real

        [Test]
        public void RealCatalog_HasAtLeast19Spells_WithHints_UniqueIdsNamesAndFiles()
        {
            var errors = new List<string>();
            var catalog = SpellCatalog.FromJsons(SpellPaths().Select(p => AssetDatabase.LoadAssetAtPath<TextAsset>(p).text), errors.Add);
            CollectionAssert.IsEmpty(errors, string.Join("; ", errors));
            Assert.GreaterOrEqual(catalog.All.Count, 19);
            Assert.AreEqual(SpellPaths().Count, catalog.All.Count, "Hay un JSON que no se cargo (id repetido?).");
            Assert.AreEqual(catalog.All.Count, catalog.All.Select(s => s.Name).Distinct().Count(), "Nombres repetidos.");
            foreach (var path in SpellPaths())
            {
                var def = SpellDef.FromJson(AssetDatabase.LoadAssetAtPath<TextAsset>(path).text);
                Assert.AreEqual(System.IO.Path.GetFileNameWithoutExtension(path), def.Id, "El archivo debe llamarse como el id.");
            }
            foreach (var spell in catalog.All)
                StringAssert.StartsWith("Se dice que", catalog.Hint(spell.Id), $"{spell.Id}: pista.");
        }

        [Test]
        public void RealCatalog_CompositionsAreUnique_AndDoNotMatchAPortalIdentity()
        {
            var catalog = Catalog();
            var seen = new HashSet<string>();
            foreach (var s in catalog.All)
            {
                string key = s.Sigil + "|" + string.Join(",", s.Signs.Select(x => x.Id + ":" + Mathf.RoundToInt(Mathf.Abs(Mathf.DeltaAngle(0f, x.TurnDeg)))).OrderBy(x => x));
                Assert.IsTrue(seen.Add(key), $"{s.Id}: composicion repetida ({key}).");
            }

            var portals = JObject.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Portals/portals.json").text);
            foreach (var portal in portals["portals"])
            {
                string sigil = (string)portal["identity"]["sigil"];
                var signs = portal["identity"]["signs"].Select(t => (string)t).ToList();
                var clash = catalog.MatchCast(sigil, signs);
                Assert.IsNull(clash, $"El hechizo '{clash?.Id}' coincide con la identidad del portal '{(string)portal["id"]}'.");
            }
        }

        [Test]
        public void RealCatalog_BooksListEverySpellOnce()
        {
            var lore = GlyphLore.FromJson(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Lore/glifos.json").text);
            var listed = lore.Books.SelectMany(b => b.Spells).ToList();
            Assert.AreEqual(listed.Count, listed.Distinct().Count(), "Un hechizo esta en dos libros.");
            foreach (var spell in Catalog().All.Where(s => !s.IsInitial))
                CollectionAssert.Contains(listed, spell.Id, $"{spell.Id}: ningun libro lo ensena.");
        }

        // ------------------------------------------------------------------ validez con el compilador real (sin red neuronal)

        [Test]
        public void EverySpell_CompilesToAnActiveSpell_WithTheExpectedElement()
        {
            var dictionary = TestAssets.Dictionary;
            foreach (var spell in Catalog().All)
            {
                Assert.GreaterOrEqual(dictionary.IndexOf(spell.Sigil), 0, $"{spell.Id}: sigilo '{spell.Sigil}' inexistente.");
                var sigilEntry = dictionary.Get(spell.Sigil);
                Assert.AreEqual(GlyphKind.Sigil, sigilEntry.Kind, $"{spell.Id}: '{spell.Sigil}' no es un sigilo.");
                foreach (var sign in spell.Signs)
                {
                    Assert.GreaterOrEqual(dictionary.IndexOf(sign.Id), 0, $"{spell.Id}: signo '{sign.Id}' inexistente.");
                    Assert.AreEqual(GlyphKind.Sign, dictionary.Get(sign.Id).Kind, $"{spell.Id}: '{sign.Id}' no es un signo.");
                }
                Assert.AreEqual(sigilEntry.Element, spell.Element, $"{spell.Id}: el elemento no coincide con el del sigilo.");

                var strokes = new List<Stroke> { TestStrokes.Circle(C, 0.4f, 64) };
                var predictions = new List<GlyphPrediction> { Fake(dictionary, spell.Sigil) };
                var sigilStroke = new Stroke();
                foreach (var p in new[] { new Vector2(-0.05f, -0.05f), new Vector2(0.05f, -0.05f), new Vector2(0.05f, 0.05f), new Vector2(-0.05f, 0.05f), new Vector2(-0.05f, -0.05f) })
                    sigilStroke.AddPoint(C + p);
                strokes.Add(sigilStroke);
                foreach (var sign in spell.Signs)
                {
                    Vector2 at = C + GeometryUtils.VectorFromAngleDeg(sign.AngleDeg) * 0.3f;
                    Vector2 tangent = GeometryUtils.VectorFromAngleDeg(sign.AngleDeg + 90f) * 0.04f;
                    strokes.Add(TestStrokes.Line(at - tangent, at + tangent));
                    predictions.Add(Fake(dictionary, sign.Id));
                }

                var parsed = SealParser.Parse(TestStrokes.Session(strokes.ToArray()));
                Assert.AreEqual(predictions.Count, new List<SymbolCandidate>(parsed.AllCandidates()).Count, $"{spell.Id}: candidatos != predicciones.");
                var ast = GlyphAstBuilder.Build(parsed, predictions, dictionary);
                var ir = SpellCompiler.Compile(ast, 10f);

                Assert.IsTrue(ir.valid, $"{spell.Id}: {ir.status}");
                Assert.IsTrue(ir.active, $"{spell.Id}: no esta activo.");
                Assert.AreEqual(spell.Sigil, ir.sigilId, spell.Id);
                Assert.AreEqual(SpellIR.ParseElement(spell.Element), ir.element, spell.Id);
            }
        }

        private static GlyphPrediction Fake(GlyphDictionary dictionary, string id)
        {
            const float confidence = 0.9f;
            var p = new float[dictionary.ClassCount];
            float rest = (1f - confidence) / (p.Length - 1);
            for (int i = 0; i < p.Length; i++) p[i] = rest;
            p[dictionary.IndexOf(id)] = confidence;
            return new GlyphPrediction(p);
        }
    }
}
