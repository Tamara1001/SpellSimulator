using NUnit.Framework;
using SpellSimulator.Learning;

namespace SpellSimulator.Tests
{
    public class GrimoireMathTests
    {
        [Test]
        public void LejosSueltoYConTiempo_Vuelve()
            => Assert.IsTrue(GrimoireMath.ShouldReturn(13f, false, false, 2.5f));

        [Test]
        public void Cerca_NoVuelve()
            => Assert.IsFalse(GrimoireMath.ShouldReturn(5f, false, false, 10f));

        [Test]
        public void SostenidoOAbiertoEnMano_NoVuelve()
        {
            Assert.IsFalse(GrimoireMath.ShouldReturn(30f, true, false, 10f));
            Assert.IsFalse(GrimoireMath.ShouldReturn(30f, false, true, 10f));
        }

        [Test]
        public void PocoTiempo_NoVuelve()
            => Assert.IsFalse(GrimoireMath.ShouldReturn(30f, false, false, 1f));
    }
}
