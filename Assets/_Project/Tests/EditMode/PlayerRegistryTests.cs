using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Core;
using SpellSimulator.World;

namespace SpellSimulator.Tests
{
    public class PlayerRegistryTests
    {
        private sealed class FakePlayer : IPlayerContext
        {
            private readonly GameObject go;
            public FakePlayer(int id, bool local, Vector3 position)
            {
                OwnerId = id;
                IsLocal = local;
                go = new GameObject("FakePlayer" + id);
                go.transform.position = position;
            }
            public int OwnerId { get; }
            public bool IsLocal { get; }
            public Transform Root => go != null ? go.transform : null;
            public Transform Head => go != null ? go.transform : null;
            public Transform MainHand => null;
            public Transform SecondaryHand => null;
            public bool IsAlive => go != null;
            public void Kill() => Object.DestroyImmediate(go);
        }

        [SetUp] public void SetUp() => PlayerRegistry.Clear();
        [TearDown] public void TearDown() => PlayerRegistry.Clear();

        [Test]
        public void Empty_HasNoLocal()
        {
            Assert.IsNull(PlayerRegistry.Local);
            Assert.AreEqual(0, PlayerRegistry.Count);
            Assert.IsFalse(PlayerRegistry.AnyNear(Vector3.zero, 100f));
        }

        [Test]
        public void Register_IsIdempotent_AndLocalPrefersIsLocal()
        {
            var remote = new FakePlayer(1, false, Vector3.zero);
            var local = new FakePlayer(2, true, Vector3.one);
            PlayerRegistry.Register(remote);
            PlayerRegistry.Register(remote);
            PlayerRegistry.Register(local);
            Assert.AreEqual(2, PlayerRegistry.Count);
            Assert.AreSame(local, PlayerRegistry.Local);
            PlayerRegistry.Unregister(local);
            Assert.AreSame(remote, PlayerRegistry.Local, "sin local marcado, el primero");
            remote.Kill(); local.Kill();
        }

        [Test]
        public void ById_FindsAndIgnoresMissing()
        {
            var a = new FakePlayer(3, true, Vector3.zero);
            PlayerRegistry.Register(a);
            Assert.AreSame(a, PlayerRegistry.ById(3));
            Assert.IsNull(PlayerRegistry.ById(9));
            a.Kill();
        }

        [Test]
        public void Closest_AndAnyNear_UseAllPlayers()
        {
            var a = new FakePlayer(0, true, new Vector3(0, 0, 0));
            var b = new FakePlayer(1, false, new Vector3(50, 0, 0));
            PlayerRegistry.Register(a);
            PlayerRegistry.Register(b);
            var c = PlayerRegistry.Closest(new Vector3(48, 0, 0), out float sqr);
            Assert.AreSame(b, c);
            Assert.AreEqual(4f, sqr, 1e-3f);
            Assert.IsTrue(PlayerRegistry.AnyNear(new Vector3(48, 0, 0), 3f), "cerca del segundo jugador");
            Assert.IsFalse(PlayerRegistry.AnyNear(new Vector3(25, 0, 0), 3f));
            a.Kill(); b.Kill();
        }

        [Test]
        public void Destroyed_IsSkipped()
        {
            var a = new FakePlayer(0, true, Vector3.zero);
            var b = new FakePlayer(1, false, new Vector3(5, 0, 0));
            PlayerRegistry.Register(a);
            PlayerRegistry.Register(b);
            a.Kill();
            Assert.IsFalse(a.IsAlive);
            Assert.AreSame(b, PlayerRegistry.Local);
            Assert.IsNull(PlayerRegistry.ById(0));
            Assert.IsFalse(PlayerRegistry.AnyNear(Vector3.zero, 1f));
            b.Kill();
        }

        [Test]
        public void Unregister_RaisesChanged()
        {
            int changes = 0;
            void OnChanged() => changes++;
            PlayerRegistry.Changed += OnChanged;
            var a = new FakePlayer(0, true, Vector3.zero);
            PlayerRegistry.Register(a);
            PlayerRegistry.Unregister(a);
            PlayerRegistry.Unregister(a); // ya no esta: no emite
            PlayerRegistry.Changed -= OnChanged;
            Assert.AreEqual(2, changes);
            a.Kill();
        }

        [Test]
        public void PortalTravelerCooldowns_ArePerPlayer()
        {
            PortalTravelerCooldowns.Clear();
            Assert.IsTrue(PortalTravelerCooldowns.Ready(0, 0f));
            PortalTravelerCooldowns.Mark(0, 10f, 2f);
            Assert.IsFalse(PortalTravelerCooldowns.Ready(0, 11f));
            Assert.IsTrue(PortalTravelerCooldowns.Ready(1, 11f), "otro jugador no queda en enfriamiento");
            Assert.IsTrue(PortalTravelerCooldowns.Ready(0, 12f));
            PortalTravelerCooldowns.Clear();
        }
    }
}
