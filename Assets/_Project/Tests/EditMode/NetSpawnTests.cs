using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Net;

namespace SpellSimulator.Tests
{
    public class NetSpawnTests
    {
        [Test]
        public void Rock_RoundTrip()
        {
            var d = new NetSpawn.RockData { pos = new Vector3(1.5f, 2f, -3f), rot = Quaternion.Euler(10, 20, 30), variant = 3, radius = 0.8f, formSeconds = 1.4f, elapsed = 0.5f };
            Assert.IsTrue(NetSpawn.TryDecodeRock(NetSpawn.EncodeRock(d), out NetSpawn.RockData b));
            Assert.AreEqual(d.pos, b.pos);
            Assert.AreEqual(3, b.variant);
            Assert.AreEqual(0.8f, b.radius);
            Assert.AreEqual(1.4f, b.formSeconds);
            Assert.AreEqual(0.5f, b.elapsed);
            Assert.Less(Quaternion.Angle(d.rot, b.rot), 0.01f);
        }

        [Test]
        public void Rock_RejectsTruncatedAndAbsurdData()
        {
            byte[] good = NetSpawn.EncodeRock(new NetSpawn.RockData { pos = Vector3.zero, rot = Quaternion.identity, variant = 0, radius = 0.5f, formSeconds = 1f, elapsed = 0f });
            Assert.IsFalse(NetSpawn.TryDecodeRock(null, out _));
            Assert.IsFalse(NetSpawn.TryDecodeRock(new byte[10], out _));
            var cut = new byte[good.Length - 3];
            System.Array.Copy(good, cut, cut.Length);
            Assert.IsFalse(NetSpawn.TryDecodeRock(cut, out _));
            Assert.IsFalse(NetSpawn.TryDecodeRock(NetSpawn.EncodeRock(new NetSpawn.RockData { rot = Quaternion.identity, radius = 500f, formSeconds = 1f }), out _), "radio absurdo");
            Assert.IsFalse(NetSpawn.TryDecodeRock(NetSpawn.EncodeRock(new NetSpawn.RockData { pos = new Vector3(float.NaN, 0, 0), rot = Quaternion.identity, radius = 1f, formSeconds = 1f }), out _), "NaN");
        }

        [Test]
        public void Rock_BadRotationFallsBackToIdentity()
        {
            var d = new NetSpawn.RockData { pos = Vector3.zero, rot = new Quaternion(0, 0, 0, 0), radius = 1f, formSeconds = 1f };
            Assert.IsTrue(NetSpawn.TryDecodeRock(NetSpawn.EncodeRock(d), out NetSpawn.RockData b));
            Assert.AreEqual(Quaternion.identity, b.rot);
        }
    }
}
