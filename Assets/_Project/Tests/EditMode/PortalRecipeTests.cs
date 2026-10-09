using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Casting;

namespace SpellSimulator.Tests
{
    /// <summary>Portales: carga de portals.json (v2) y coincidencia de lo leido con las identidades.</summary>
    public class PortalRecipeTests
    {
        private const float Min = 0.2f;

        private const string Json = @"{
  ""version"": 2,
  ""portals"": [
    { ""id"": ""a"", ""displayName"": ""Portal A"", ""identity"": { ""sigil"": ""light"", ""signs"": [""levitation"", ""convergence""] } },
    { ""id"": ""b"", ""displayName"": ""Portal B"", ""identity"": { ""sigil"": ""earth"", ""signs"": [""crush"", ""pull""] } },
    { ""id"": ""c"", ""displayName"": ""Portal C"", ""identity"": { ""sigil"": ""light"", ""signs"": [""levitation"", ""convergence"", ""weave""] } }
  ]
}";

        private static PortalRecipes Recipes() => PortalRecipes.Load(Json);

        private static PortalReading Flat(string sigil, params string[] signs)
        {
            var reading = new PortalReading { SigilId = sigil };
            foreach (string s in signs) reading.Signs.Add(new PortalGlyph(s, 0.9f));
            return reading;
        }

        [Test]
        public void Load_ReadsPortalsAndIdentities()
        {
            var recipes = Recipes();
            Assert.AreEqual(3, recipes.Portals.Count);
            Assert.AreEqual(3, recipes.Identities.Count);
            var a = recipes.Find("a");
            Assert.AreEqual("light", a.Identity.Sigil);
            CollectionAssert.AreEqual(new[] { "convergence", "levitation" }, a.Identity.Signs); // ordenados
            Assert.IsTrue(a.Identity.IsValid);
            Assert.IsNull(recipes.Find("zzz"));
        }

        [Test]
        public void Load_RejectsOldVersionAndGarbage()
        {
            Assert.AreEqual(0, PortalRecipes.Load("").Portals.Count);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("version 1"));
            Assert.AreEqual(0, PortalRecipes.Load(@"{ ""version"": 1, ""portals"": [] }").Portals.Count);
        }

        [Test]
        public void Matches_SameIdentityAnyOrder()
        {
            var id = Recipes().Find("b").Identity;
            Assert.IsTrue(PortalMath.Matches(id, Flat("earth", "crush", "pull"), Min, out int excess));
            Assert.AreEqual(0, excess);
            Assert.IsTrue(PortalMath.Matches(id, Flat("earth", "pull", "crush"), Min, out _));
        }

        [Test]
        public void Matches_ToleratesOneExtraSignButNotTwo()
        {
            var id = Recipes().Find("b").Identity;
            Assert.IsTrue(PortalMath.Matches(id, Flat("earth", "crush", "pull", "weave"), Min, out int excess));
            Assert.AreEqual(1, excess);
            Assert.IsFalse(PortalMath.Matches(id, Flat("earth", "crush", "pull", "weave", "column"), Min, out _));
        }

        [Test]
        public void Matches_MissingSignOrWrongSigilFails()
        {
            var id = Recipes().Find("b").Identity;
            Assert.IsFalse(PortalMath.Matches(id, Flat("earth", "crush"), Min, out _));
            Assert.IsFalse(PortalMath.Matches(id, Flat("fire", "crush", "pull"), Min, out _));
            Assert.IsFalse(PortalMath.Matches(id, Flat(null, "crush", "pull"), Min, out _));
            // Un glifo repetido no cubre dos signos distintos.
            Assert.IsFalse(PortalMath.Matches(id, Flat("earth", "crush", "crush"), Min, out _));
        }

        [Test]
        public void Matches_LowConfidenceGlyphsDoNotCount()
        {
            var id = Recipes().Find("b").Identity;
            var reading = Flat("earth", "crush", "pull");
            reading.Signs[1] = new PortalGlyph("pull", 0.1f);
            Assert.IsFalse(PortalMath.Matches(id, reading, Min, out _));

            // Un sobrante dudoso no cuenta como signo de mas.
            var noisy = Flat("earth", "crush", "pull", "weave", "column");
            noisy.Signs[3] = new PortalGlyph("weave", 0.05f);
            Assert.IsTrue(PortalMath.Matches(id, noisy, Min, out int excess));
            Assert.AreEqual(1, excess);
        }

        [Test]
        public void Matches_NestedReadingNeverMatches()
        {
            var id = Recipes().Find("b").Identity;
            var nested = new PortalReading { Inners = new List<PortalReading> { Flat("earth", "crush", "pull") } };
            Assert.IsFalse(PortalMath.Matches(id, nested, Min, out _));
        }

        [Test]
        public void BestMatch_PrefersHigherCoverageThenLowerExcess()
        {
            var recipes = Recipes();
            // a = light + {levitation, convergence}; c = light + {levitation, convergence, weave}.
            // Con los tres signos coinciden las dos (c exacta, a con 1 de mas): gana c.
            int full = PortalMath.BestMatch(recipes.Identities, Flat("light", "levitation", "convergence", "weave"), Min);
            Assert.AreEqual("c", recipes.Portals[full].Id);
            // Con dos signos solo coincide a.
            int two = PortalMath.BestMatch(recipes.Identities, Flat("light", "levitation", "convergence"), Min);
            Assert.AreEqual("a", recipes.Portals[two].Id);
            // Con un sobrante ajeno coincide a (con exceso 1).
            int extra = PortalMath.BestMatch(recipes.Identities, Flat("light", "levitation", "convergence", "crush"), Min);
            Assert.AreEqual("a", recipes.Portals[extra].Id);
        }

        [Test]
        public void BestMatch_NoneReturnsMinusOne()
        {
            var recipes = Recipes();
            Assert.AreEqual(-1, PortalMath.BestMatch(recipes.Identities, Flat("fire", "crush"), Min));
            Assert.AreEqual(-1, PortalMath.BestMatch(recipes.Identities, Flat("earth", "crush"), Min));
            Assert.AreEqual(-1, PortalMath.BestMatch(null, Flat("earth", "crush", "pull"), Min));
        }

        // ---- Datos reales (Data/Portals/portals.json) ----

        [Test]
        public void RealFile_FourDistinctWellFormedIdentities()
        {
            var recipes = PortalRecipes.LoadDefault();
            string[] ids = { "tower-study", "watchtower", "field-circle", "cave" };
            foreach (string id in ids)
            {
                var def = recipes.Find(id);
                Assert.IsNotNull(def, "falta el portal " + id);
                Assert.IsTrue(def.Identity.IsValid, id);
                Assert.AreEqual(2, def.Identity.Signs.Length, id + ": 1 sigilo + 2 signos");
                Assert.AreNotEqual(def.Identity.Signs[0], def.Identity.Signs[1], id + ": signos distintos");
            }
            AssertGlyphExists("sigils", recipes.Find(ids[0]).Identity.Sigil);
            foreach (string id in ids)
            {
                AssertGlyphExists("sigils", recipes.Find(id).Identity.Sigil);
                foreach (string s in recipes.Find(id).Identity.Signs) AssertGlyphExists("signs", s);
            }
        }

        [Test]
        public void RealFile_NoIdentityMatchesAnotherEvenWithTolerance()
        {
            var recipes = PortalRecipes.LoadDefault();
            Assert.GreaterOrEqual(recipes.Portals.Count, 4);
            for (int i = 0; i < recipes.Portals.Count; i++)
            {
                for (int j = 0; j < recipes.Portals.Count; j++)
                {
                    if (i == j) continue;
                    // Escribir la identidad j (y j con cualquier signo de mas) nunca debe leerse como la identidad i.
                    var exact = Flat(recipes.Portals[j].Identity.Sigil, recipes.Portals[j].Identity.Signs);
                    Assert.IsFalse(PortalMath.Matches(recipes.Portals[i].Identity, exact, Min, out _),
                        $"{recipes.Portals[i].Id} coincide con la identidad de {recipes.Portals[j].Id}");
                    foreach (string extra in recipes.Portals[i].Identity.Signs)
                    {
                        var withExtra = Flat(recipes.Portals[j].Identity.Sigil, Append(recipes.Portals[j].Identity.Signs, extra));
                        Assert.IsFalse(PortalMath.Matches(recipes.Portals[i].Identity, withExtra, Min, out _),
                            $"{recipes.Portals[i].Id} coincide con {recipes.Portals[j].Id} + {extra}");
                    }
                    Assert.AreEqual(j, PortalMath.BestMatch(recipes.Identities, exact, Min), "BestMatch debe devolver la propia identidad");
                }
            }
        }

        private static string[] Append(string[] signs, string extra)
        {
            var list = new List<string>(signs) { extra };
            return list.ToArray();
        }

        private static void AssertGlyphExists(string folder, string id)
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "_Project", "Data", "Dictionary", folder));
            string[] files = Directory.GetFiles(dir, "*-" + id + ".json");
            Assert.AreEqual(1, files.Length, $"{folder}/{id} no esta en el diccionario");
        }
    }
}
