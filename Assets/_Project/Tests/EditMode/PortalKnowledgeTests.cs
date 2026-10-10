using System.Collections.Generic;
using NUnit.Framework;
using SpellSimulator.Casting;
using SpellSimulator.Learning;

namespace SpellSimulator.Tests
{
    public class PortalKnowledgeTests
    {
        private static PortalRecipes Recipes() => PortalRecipes.Load(
            "{\"version\":2,\"portals\":[" +
            "{\"id\":\"a\",\"displayName\":\"A\",\"identity\":{\"sigil\":\"fire\",\"signs\":[\"column\"]}}," +
            "{\"id\":\"b\",\"displayName\":\"B\",\"identity\":{\"sigil\":\"earth\",\"signs\":[\"crush\"]}}]}");

        private static List<BookInfo> Books()
        {
            var one = new BookInfo { Id = "p1" };
            one.Portals.Add("a");
            one.Portals.Add("no-existe");
            var two = new BookInfo { Id = "p2" };
            two.Portals.Add("a");
            two.Portals.Add("b");
            return new List<BookInfo> { one, two, new BookInfo { Id = "glifos" } };
        }

        [Test]
        public void SinLibrosLeidosNoSeConoceNada()
        {
            Assert.AreEqual(0, PortalKnowledge.Known(Recipes(), Books(), _ => false).Count);
        }

        [Test]
        public void UnLibroLeidoAnotaSusPortalesSinRepetirNiInventar()
        {
            var known = PortalKnowledge.Known(Recipes(), Books(), id => id == "p1");
            Assert.AreEqual(1, known.Count);
            Assert.AreEqual("a", known[0].Id);
            var all = PortalKnowledge.Known(Recipes(), Books(), _ => true);
            Assert.AreEqual(2, all.Count);
            Assert.AreEqual(2, PortalKnowledge.Total(Recipes(), Books()));
        }

        [Test]
        public void EntradasNulasNoRompen()
        {
            Assert.AreEqual(0, PortalKnowledge.Known(null, Books(), _ => true).Count);
            Assert.AreEqual(0, PortalKnowledge.Known(Recipes(), null, _ => true).Count);
        }
    }
}
