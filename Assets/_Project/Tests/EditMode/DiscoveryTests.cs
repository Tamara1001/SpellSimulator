using NUnit.Framework;
using SpellSimulator.Learning;

namespace SpellSimulator.Tests
{
    /// <summary>Descubrimiento de hechizos por experimentacion (logica pura) y su persistencia.</summary>
    public class DiscoveryTests
    {
        private static SpellCatalog Catalog() => SpellCatalog.FromJsons(new[]
        {
            "{ \"id\": \"gust\", \"name\": \"Rafaga\", \"book\": \"inicial\", \"sigil\": \"wind\", \"signs\": [ {\"id\":\"column\",\"angle\":0}, {\"id\":\"column\",\"angle\":180} ] }",
            "{ \"id\": \"blaze\", \"name\": \"Llamarada\", \"book\": \"spells-fire\", \"sigil\": \"fire\", \"signs\": [ {\"id\":\"column\",\"angle\":0}, {\"id\":\"column\",\"angle\":180} ] }",
            "{ \"id\": \"flame\", \"name\": \"Llama\", \"book\": \"spells-fire\", \"sigil\": \"fire\", \"signs\": [ {\"id\":\"column\",\"angle\":270} ] }",
        });

        [Test]
        public void OnCast_DiscoversAnUnknownMatchingSpell_OnlyOnce()
        {
            var state = new DiscoveryState();
            var catalog = Catalog();

            var found = Discovery.OnCast(state, catalog, "fire", new[] { "column", "column" });
            Assert.IsNotNull(found);
            Assert.AreEqual("blaze", found.Id);
            Assert.IsTrue(state.KnowsSpell("blaze"));
            Assert.IsNull(Discovery.OnCast(state, catalog, "fire", new[] { "column", "column" }), "ya descubierto");
        }

        [Test]
        public void OnCast_IgnoresKnownInitialAndBookSpells_AndNonMatches()
        {
            var state = new DiscoveryState();
            var catalog = Catalog();
            Assert.IsNull(Discovery.OnCast(state, catalog, "wind", new[] { "column", "column" }), "hechizo inicial");
            Assert.IsNull(Discovery.OnCast(state, catalog, "water", new[] { "column" }), "no esta en el catalogo");
            Assert.IsNull(Discovery.OnCast(null, catalog, "fire", new[] { "column" }));
            Assert.IsNull(Discovery.OnCast(state, null, "fire", new[] { "column" }));

            state.MarkRead("spells-fire");
            Assert.IsNull(Discovery.OnCast(state, catalog, "fire", new[] { "column" }), "su libro ya fue leido");
            Assert.IsFalse(state.KnowsSpell("flame"));
        }

        [Test]
        public void SpellDiscovery_IsPersisted_AndOldSavesStillLoad()
        {
            var state = DiscoveryState.Starting(new[] { "fire" });
            Assert.IsTrue(state.DiscoverSpell("blaze"));
            Assert.IsFalse(state.DiscoverSpell("blaze"));

            var back = DiscoveryState.FromJson(state.ToJson());
            Assert.IsTrue(back.KnowsSpell("blaze"));
            Assert.AreEqual(1, back.SpellCount);
            Assert.IsTrue(back.Knows("fire"));

            // Partida anterior (version 1, sin "spells").
            var old = DiscoveryState.FromJson("{ \"version\": 1, \"glyphs\": [ \"fire\" ], \"books\": [ \"elements\" ] }");
            Assert.AreEqual(0, old.SpellCount);
            Assert.IsTrue(old.Knows("fire"));
            Assert.IsTrue(old.HasRead("elements"));
        }

        [Test]
        public void DiscoveredSpell_CountsAsUnlockedInTheCatalog()
        {
            var catalog = Catalog();
            var state = new DiscoveryState();
            var list = new System.Collections.Generic.List<SpellDef>();
            try
            {
                SpellCatalog.DiscoveredCheck = state.KnowsSpell;
                catalog.Unlocked(_ => false, list);
                CollectionAssert.AreEquivalent(new[] { "gust" }, list.ConvertAll(s => s.Id));

                state.DiscoverSpell("flame");
                catalog.Unlocked(_ => false, list);
                CollectionAssert.AreEquivalent(new[] { "gust", "flame" }, list.ConvertAll(s => s.Id));
            }
            finally { SpellCatalog.DiscoveredCheck = null; }
        }
    }
}
