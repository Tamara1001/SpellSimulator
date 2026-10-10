using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Net;

namespace SpellSimulator.Tests
{
    public class NetBodyMathTests
    {
        [Test]
        public void Rotation_RoundTrip_IsCloseEnough()
        {
            var rnd = new System.Random(7);
            for (int i = 0; i < 200; i++)
            {
                var q = Random.rotationUniform;
                NetBodyMath.PackRotation(q, out short x, out short y, out short z, out short w);
                var back = NetBodyMath.UnpackRotation(x, y, z, w);
                Assert.Less(Quaternion.Angle(q, back), 0.05f, "rotacion " + i);
            }
        }

        [Test]
        public void Rotation_NegatedQuaternion_IsSameRotation()
        {
            var q = Quaternion.Euler(30f, 120f, -45f);
            var neg = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            NetBodyMath.PackRotation(neg, out short x, out short y, out short z, out short w);
            Assert.GreaterOrEqual(w, 0);
            Assert.Less(Quaternion.Angle(q, NetBodyMath.UnpackRotation(x, y, z, w)), 0.05f);
        }

        [Test]
        public void Rotation_ZeroQuaternion_FallsBackToIdentity()
        {
            NetBodyMath.PackRotation(new Quaternion(0, 0, 0, 0), out short x, out short y, out short z, out short w);
            Assert.Less(Quaternion.Angle(Quaternion.identity, NetBodyMath.UnpackRotation(x, y, z, w)), 0.01f);
        }

        [Test]
        public void NeedsSend_DetectsMoveAndTurn_IgnoresNoise()
        {
            var p = new Vector3(1, 2, 3);
            var r = Quaternion.identity;
            Assert.IsFalse(NetBodyMath.NeedsSend(p, r, p + new Vector3(0.001f, 0, 0), r));
            Assert.IsTrue(NetBodyMath.NeedsSend(p, r, p + new Vector3(0.01f, 0, 0), r));
            Assert.IsFalse(NetBodyMath.NeedsSend(p, r, p, Quaternion.Euler(0.1f, 0, 0)));
            Assert.IsTrue(NetBodyMath.NeedsSend(p, r, p, Quaternion.Euler(1f, 0, 0)));
        }

        [Test]
        public void ValidateGrab_Cases()
        {
            const int none = -1;
            Assert.AreEqual(GrabVerdict.UnknownBody, NetBodyMath.ValidateGrab(false, none, none, 1, 1f));
            Assert.AreEqual(GrabVerdict.Ok, NetBodyMath.ValidateGrab(true, none, none, 1, 5f));
            Assert.AreEqual(GrabVerdict.AlreadyHeld, NetBodyMath.ValidateGrab(true, 2, none, 1, 5f));
            Assert.AreEqual(GrabVerdict.Ok, NetBodyMath.ValidateGrab(true, 1, none, 1, 5f), "el mismo que ya lo tiene puede repetir el pedido");
            Assert.AreEqual(GrabVerdict.TooFar, NetBodyMath.ValidateGrab(true, none, none, 1, NetBodyMath.MaxGrabDistance + 1f));
        }

        [Test]
        public void FollowAlpha_IsMonotonicAndBounded()
        {
            Assert.AreEqual(0f, NetBodyMath.FollowAlpha(18f, 0f), 1e-6f);
            float a1 = NetBodyMath.FollowAlpha(18f, 0.01f), a2 = NetBodyMath.FollowAlpha(18f, 0.05f);
            Assert.Greater(a2, a1);
            Assert.Less(NetBodyMath.FollowAlpha(18f, 10f), 1.0001f);
            Assert.AreEqual(0f, NetBodyMath.FollowAlpha(-5f, 1f), 1e-6f);
        }

        [Test]
        public void ChunkCount_RoundsUp()
        {
            Assert.AreEqual(0, NetBodyMath.ChunkCount(0, 40));
            Assert.AreEqual(1, NetBodyMath.ChunkCount(40, 40));
            Assert.AreEqual(2, NetBodyMath.ChunkCount(41, 40));
            Assert.AreEqual(0, NetBodyMath.ChunkCount(5, 0));
        }

        [Test]
        public void Fingerprint_ChangesWithContentAndOrder()
        {
            var a = new List<string> { "a#0", "b#1" };
            var b = new List<string> { "a#0", "b#1" };
            var c = new List<string> { "b#1", "a#0" };
            var d = new List<string> { "a#0", "b#2" };
            Assert.AreEqual(NetBodyMath.Fingerprint(a), NetBodyMath.Fingerprint(b));
            Assert.AreNotEqual(NetBodyMath.Fingerprint(a), NetBodyMath.Fingerprint(c));
            Assert.AreNotEqual(NetBodyMath.Fingerprint(a), NetBodyMath.Fingerprint(d));
        }

        [Test]
        public void PathKey_RootUsesPositionChildrenUseSiblingIndex()
        {
            var root = new GameObject("Root").transform;
            root.position = new Vector3(1.234f, 0f, -5f);
            var first = new GameObject("Crate").transform; first.SetParent(root);
            var second = new GameObject("Crate").transform; second.SetParent(root);
            try
            {
                Assert.AreEqual("Root@1.23,0.00,-5.00/Crate#0", NetBodyMath.PathKey(first));
                Assert.AreNotEqual(NetBodyMath.PathKey(first), NetBodyMath.PathKey(second));
                Assert.AreEqual("Root@1.23,0.00,-5.00", NetBodyMath.PathKey(root));
                Assert.AreEqual("", NetBodyMath.PathKey(null));
            }
            finally { Object.DestroyImmediate(root.gameObject); }
        }

        [Test]
        public void PathKey_SameNameRootsAtDifferentPlacesAreDistinct()
        {
            var a = new GameObject("PaperTemplate").transform; a.position = new Vector3(0, 1, 0);
            var b = new GameObject("PaperTemplate").transform; b.position = new Vector3(3, 1, 0);
            try { Assert.AreNotEqual(NetBodyMath.PathKey(a), NetBodyMath.PathKey(b)); }
            finally { Object.DestroyImmediate(a.gameObject); Object.DestroyImmediate(b.gameObject); }
        }

        [Test]
        public void CompareKeys_IsOrdinal()
        {
            Assert.Less(NetBodyMath.CompareKeys("B", "a"), 0);
            Assert.AreEqual(0, NetBodyMath.CompareKeys("x", "x"));
        }
    }
}
