using NUnit.Framework;
using SpellSimulator.Net;

namespace SpellSimulator.Tests
{
    public class NetRateLimiterTests
    {
        [Test]
        public void DejaPasarLaRafagaYCortaElExceso()
        {
            var l = new NetRateLimiter();
            int passed = 0;
            for (int i = 0; i < 50; i++) if (l.Allow(1, NetChannel.Reset, 0f)) passed++;
            Assert.AreEqual(3, passed);
            Assert.AreEqual(47, l.Dropped);
        }

        [Test]
        public void SeRecargaConElTiempo()
        {
            var l = new NetRateLimiter();
            for (int i = 0; i < 10; i++) l.Allow(1, NetChannel.Reset, 0f);
            Assert.IsFalse(l.Allow(1, NetChannel.Reset, 0.5f));
            Assert.IsTrue(l.Allow(1, NetChannel.Reset, 1.1f));
        }

        [Test]
        public void ClientesYCanalesSonIndependientes()
        {
            var l = new NetRateLimiter();
            for (int i = 0; i < 10; i++) l.Allow(1, NetChannel.Reset, 0f);
            Assert.IsTrue(l.Allow(2, NetChannel.Reset, 0f));
            Assert.IsTrue(l.Allow(1, NetChannel.Grab, 0f));
        }

        [Test]
        public void OlvidarDevuelveLaRafaga()
        {
            var l = new NetRateLimiter();
            for (int i = 0; i < 10; i++) l.Allow(1, NetChannel.Reset, 0f);
            l.Forget(1);
            Assert.IsTrue(l.Allow(1, NetChannel.Reset, 0f));
        }
    }
}
