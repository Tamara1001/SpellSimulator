using NUnit.Framework;
using SpellSimulator.Casting;

namespace SpellSimulator.Tests
{
    /// <summary>Portales: carga de portals.json y coincidencia de lo leido con la receta.</summary>
    public class PortalRecipeTests
    {
        private const string Json = @"{
  ""version"": 1,
  ""portals"": [
    { ""id"": ""a"", ""pair"": ""b"", ""displayName"": ""Portal A"",
      ""recipe"": { ""sigil"": ""light"", ""signs"": [""levitation"", ""convergence"", ""repetition""], ""nested"": false } },
    { ""id"": ""b"", ""pair"": ""a"", ""displayName"": ""Portal B"",
      ""recipe"": { ""sigil"": ""earth"", ""signs"": [""crush"", ""pull""], ""nested"": false } },
    { ""id"": ""n"", ""pair"": ""b"", ""displayName"": ""Anidado"",
      ""recipe"": { ""sigil"": """", ""signs"": [], ""nested"": true, ""inner"": [
        { ""sigil"": ""wind-underfoot"", ""signs"": [""levitation"", ""billowing""] },
        { ""sigil"": ""crystal"", ""signs"": [""collection""] } ] } }
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
        public void Load_ReadsPortalsPairsAndRecipes()
        {
            var recipes = Recipes();
            Assert.AreEqual(3, recipes.Portals.Count);
            var a = recipes.Find("a");
            Assert.AreEqual("b", a.PairId);
            Assert.AreEqual("light", a.Recipe.Sigil);
            Assert.AreEqual(3, a.Recipe.Signs.Count);
            Assert.IsFalse(a.Recipe.Nested);
            var n = recipes.Find("n");
            Assert.IsTrue(n.Recipe.Nested);
            Assert.AreEqual(2, n.Recipe.Inner.Count);
            Assert.AreEqual("wind-underfoot", n.Recipe.Inner[0].Sigil);
            Assert.IsNull(recipes.Find("zzz"));
        }

        [Test]
        public void Load_RejectsUnknownVersionAndGarbage()
        {
            Assert.AreEqual(0, PortalRecipes.Load("").Portals.Count);
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("version 9"));
            Assert.AreEqual(0, PortalRecipes.Load(@"{ ""version"": 9, ""portals"": [] }").Portals.Count);
        }

        [Test]
        public void Matches_SameRecipe()
        {
            var recipe = Recipes().Find("a").Recipe;
            Assert.IsTrue(PortalMath.Matches(recipe, Flat("light", "levitation", "convergence", "repetition"), 0.35f));
            Assert.IsTrue(PortalMath.Matches(recipe, "light", new[] { "levitation", "convergence", "repetition" }, 0.35f));
        }

        [Test]
        public void Matches_IgnoresSignOrder()
        {
            var recipe = Recipes().Find("a").Recipe;
            Assert.IsTrue(PortalMath.Matches(recipe, "light", new[] { "repetition", "levitation", "convergence" }, 0.35f));
        }

        [Test]
        public void Matches_ExtraOrMissingSignsFail()
        {
            var recipe = Recipes().Find("a").Recipe;
            Assert.IsFalse(PortalMath.Matches(recipe, "light", new[] { "levitation", "convergence" }, 0.35f));
            Assert.IsFalse(PortalMath.Matches(recipe, "light", new[] { "levitation", "convergence", "repetition", "weave" }, 0.35f));
            // Mismo tamano pero con un signo repetido en vez de otro: no es el mismo multiconjunto.
            Assert.IsFalse(PortalMath.Matches(recipe, "light", new[] { "levitation", "levitation", "repetition" }, 0.35f));
        }

        [Test]
        public void Matches_DifferentSigilFails()
        {
            var recipe = Recipes().Find("a").Recipe;
            Assert.IsFalse(PortalMath.Matches(recipe, "fire", new[] { "levitation", "convergence", "repetition" }, 0.35f));
            Assert.IsFalse(PortalMath.Matches(recipe, null, new[] { "levitation", "convergence", "repetition" }, 0.35f));
        }

        [Test]
        public void Matches_LowConfidenceFails()
        {
            var recipe = Recipes().Find("b").Recipe;
            var reading = Flat("earth", "crush", "pull");
            Assert.IsTrue(PortalMath.Matches(recipe, reading, 0.35f));
            reading.Signs[1] = new PortalGlyph("pull", 0.2f);
            Assert.IsFalse(PortalMath.Matches(recipe, reading, 0.35f));
        }

        [Test]
        public void Matches_NestedAcceptsInnersInAnyOrder()
        {
            var recipe = Recipes().Find("n").Recipe;
            var reading = new PortalReading();
            reading.Inners = new System.Collections.Generic.List<PortalReading>
            {
                Flat("crystal", "collection"),
                Flat("wind-underfoot", "billowing", "levitation"),
            };
            Assert.IsTrue(PortalMath.Matches(recipe, reading, 0.35f));
        }

        [Test]
        public void Matches_NestedFailsWithRepeatedOrWrongInner()
        {
            var recipe = Recipes().Find("n").Recipe;
            var repeated = new PortalReading { Inners = new System.Collections.Generic.List<PortalReading>
                { Flat("crystal", "collection"), Flat("crystal", "collection") } };
            Assert.IsFalse(PortalMath.Matches(recipe, repeated, 0.35f));

            var wrong = new PortalReading { Inners = new System.Collections.Generic.List<PortalReading>
                { Flat("wind-underfoot", "billowing", "levitation"), Flat("crystal", "collection", "weave") } };
            Assert.IsFalse(PortalMath.Matches(recipe, wrong, 0.35f));

            var oneInner = new PortalReading { Inners = new System.Collections.Generic.List<PortalReading>
                { Flat("wind-underfoot", "billowing", "levitation") } };
            Assert.IsFalse(PortalMath.Matches(recipe, oneInner, 0.35f));
        }

        [Test]
        public void Matches_NestedAndFlatDoNotMix()
        {
            var flatRecipe = Recipes().Find("a").Recipe;
            var nestedReading = new PortalReading { Inners = new System.Collections.Generic.List<PortalReading>
                { Flat("light", "levitation", "convergence", "repetition") } };
            Assert.IsFalse(PortalMath.Matches(flatRecipe, nestedReading, 0.35f));
            Assert.IsFalse(PortalMath.Matches(Recipes().Find("n").Recipe, Flat("light"), 0.35f));
        }

        [Test]
        public void InRange_UsesRadius()
        {
            Assert.IsTrue(PortalMath.InRange(UnityEngine.Vector3.zero, new UnityEngine.Vector3(2f, 0f, 0f), 2.5f));
            Assert.IsFalse(PortalMath.InRange(UnityEngine.Vector3.zero, new UnityEngine.Vector3(3f, 0f, 0f), 2.5f));
        }
    }
}
