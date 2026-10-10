using NUnit.Framework;
using SpellSimulator.Net.Bots;

namespace SpellSimulator.Tests
{
    public class BotOptionsTests
    {
        [Test]
        public void Parse_WithoutBotFlag_IsDisabled()
        {
            var o = BotOptions.Parse(new[] { "game.exe", "-batchmode" });
            Assert.IsFalse(o.Enabled);
            Assert.IsFalse(o.HasTarget);
        }

        [Test]
        public void Parse_RelayBot_ReadsEverything()
        {
            var o = BotOptions.Parse(new[] { "game.exe", "-bot", "-botjoin", "ab12cd", "-botname", "Bot7", "-botscript", "churn", "-botseconds", "12.5", "-botexpect", "2", "-botcycles", "4", "-botlog", @"C:\x\b.txt" });
            Assert.IsTrue(o.Enabled);
            Assert.AreEqual("AB12CD", o.JoinCode);
            Assert.AreEqual("Bot7", o.Name);
            Assert.AreEqual(BotScript.Churn, o.Script);
            Assert.AreEqual(12.5f, o.Seconds, 0.001f);
            Assert.AreEqual(2, o.ExpectRemotes);
            Assert.AreEqual(4, o.Cycles);
            Assert.AreEqual(@"C:\x\b.txt", o.LogPath);
            Assert.IsTrue(o.HasTarget);
        }

        [Test]
        public void Parse_DirectEndpoint_SplitsIpAndPort()
        {
            var o = BotOptions.Parse(new[] { "-bot", "-botdirect", "192.168.0.5:9000" });
            Assert.AreEqual("192.168.0.5", o.DirectIp);
            Assert.AreEqual(9000, o.DirectPort);
        }

        [Test]
        public void Parse_DirectWithoutPort_KeepsDefaultPort()
        {
            var o = BotOptions.Parse(new[] { "-bot", "-botdirect", "127.0.0.1" });
            Assert.AreEqual("127.0.0.1", o.DirectIp);
            Assert.AreEqual(7788, o.DirectPort);
        }

        [Test]
        public void Parse_MissingValueOrGarbage_UsesDefaults()
        {
            var o = BotOptions.Parse(new[] { "-bot", "-botseconds", "abc", "-botname" });
            Assert.AreEqual(30f, o.Seconds, 0.001f);
            Assert.AreEqual("Bot", o.Name);
            Assert.IsFalse(o.HasTarget);
        }

        [Test]
        public void Parse_GrabScriptStartTimeAndLatency()
        {
            var o = BotOptions.Parse(new[] { "-bot", "-botscript", "grab", "-botat", "1790000000123", "-botlatency", "75" });
            Assert.AreEqual(BotScript.Grab, o.Script);
            Assert.AreEqual(1790000000123L, o.StartAtMs);
            Assert.AreEqual(75, o.LatencyMs);
        }

        [Test]
        public void IndexOfName_ReadsTrailingNumber()
        {
            Assert.AreEqual(3, BotOptions.IndexOfName("Bot3"));
            Assert.AreEqual(12, BotOptions.IndexOfName("Bot12"));
            Assert.AreEqual(0, BotOptions.IndexOfName("Bot"));
            Assert.AreEqual(0, BotOptions.IndexOfName(null));
        }

        [Test]
        public void Parse_NullAndFlagsAreCaseInsensitive()
        {
            Assert.IsFalse(BotOptions.Parse(null).Enabled);
            Assert.IsTrue(BotOptions.Parse(new[] { "-BOT" }).Enabled);
        }
    }
}
