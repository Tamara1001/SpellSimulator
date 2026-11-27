using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Learning;
using SpellSimulator.World.Map;

namespace SpellSimulator.Tests
{
    /// <summary>Etapa sugerida del recorrido (paso 9, D32) y su latido en el tablero del mapa.</summary>
    public class LearningFlowTests
    {
        private static System.Func<string, bool> Read(params string[] books)
        {
            var set = new HashSet<string>(books);
            return set.Contains;
        }

        [Test]
        public void Stage_AdvancesWithTheBooksRead()
        {
            Assert.AreEqual(LearningFlow.Stage.Tower, LearningFlow.StageFor(Read()));
            Assert.AreEqual(LearningFlow.Stage.Tower, LearningFlow.StageFor(null));
            Assert.AreEqual(LearningFlow.Stage.TrainingAndArchive, LearningFlow.StageFor(Read("wind-primer")));
            Assert.AreEqual(LearningFlow.Stage.ElementFields, LearningFlow.StageFor(Read("wind-primer", "motion")));
            Assert.AreEqual(LearningFlow.Stage.LateGame, LearningFlow.StageFor(Read("wind-primer", "motion", "spells-fire")));
            // Gana la etapa más avanzada aunque se haya salteado alguna.
            Assert.AreEqual(LearningFlow.Stage.LateGame, LearningFlow.StageFor(Read("spells-water")));
            // Los libros de portales o del late game no cuentan como etapa.
            Assert.AreEqual(LearningFlow.Stage.Tower, LearningFlow.StageFor(Read("portals-valley", "spells-siege")));
        }

        [Test]
        public void SuggestedZones_ExistInTheRealCatalog()
        {
            WorldZoneCatalog catalog = WorldZoneCatalog.Parse(File.ReadAllText(WorldZoneCatalog.AssetPath));
            var ids = new HashSet<string>();
            foreach (WorldZoneCatalog.Zone z in catalog.zones) ids.Add(z.id);
            foreach (string id in LearningFlow.AllSuggestedZones())
                Assert.IsTrue(ids.Contains(id), $"la zona sugerida «{id}» no está en WorldZones.json");
            Assert.AreEqual(4, LearningFlow.ZonesFor(LearningFlow.Stage.ElementFields).Count);
        }

        [Test]
        public void StageBooks_ExistInTheLore()
        {
            GlyphLore lore = GlyphLore.FromJson(File.ReadAllText("Assets/_Project/Data/Lore/glifos.json"));
            foreach (string book in new[] { "wind-primer", "earth-primer", "variants", "motion", "shape", "rare",
                         "spells-water", "spells-earth", "spells-fire", "spells-wind" })
                Assert.IsNotNull(lore.Book(book), "falta el libro " + book);
        }

        [Test]
        public void TowerMap_SetSuggested_MarksOnlyThoseZones()
        {
            WorldZoneCatalog.Zone[] zones = WorldZoneCatalog.Parse(File.ReadAllText(WorldZoneCatalog.AssetPath)).zones;
            var go = new GameObject("TowerMapSuggestTest");
            try
            {
                var map = go.AddComponent<TowerMap>();
                map.Setup(zones, null, null);
                map.SetSuggested(LearningFlow.ZonesFor(LearningFlow.Stage.TrainingAndArchive));
                int marked = 0;
                for (int i = 0; i < map.ZoneCount; i++)
                {
                    if (!map.IsSuggested(i)) continue;
                    marked++;
                    string id = map.GetZone(i).id;
                    Assert.IsTrue(id == "training" || id == "archive", id);
                }
                Assert.AreEqual(2, marked);
                map.SetSuggested(null);
                for (int i = 0; i < map.ZoneCount; i++) Assert.IsFalse(map.IsSuggested(i));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
