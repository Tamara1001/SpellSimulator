using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Output;

namespace SpellSimulator.Tests
{
    /// <summary>Matematica del viento: arrastre cuadratico, corriente vertical y sesgo de propagacion del fuego.</summary>
    public class WindMathTests
    {
        private const float Big = 1000f;

        [Test]
        public void Drag_PushesTowardWind()
        {
            Vector3 a = WindMath.Drag(new Vector3(5f, 0f, 0f), Vector3.zero, 1f, 0.5f, 1f, Big);
            Assert.Greater(a.x, 0f);
            Assert.AreEqual(0f, a.y, 1e-5f);
            Assert.AreEqual(0f, a.z, 1e-5f);
        }

        [Test]
        public void Drag_IsQuadraticInRelativeSpeed()
        {
            float a1 = WindMath.Drag(Vector3.right * 2f, Vector3.zero, 1f, 1f, 1f, Big).magnitude;
            float a2 = WindMath.Drag(Vector3.right * 4f, Vector3.zero, 1f, 1f, 1f, Big).magnitude;
            Assert.AreEqual(4f, a2 / a1, 1e-3f);
        }

        [Test]
        public void Drag_UsesRelativeVelocity()
        {
            Vector3 a = WindMath.Drag(Vector3.right * 5f, Vector3.right * 5f, 1f, 1f, 1f, Big);
            Assert.AreEqual(0f, a.magnitude, 1e-5f);
        }

        [Test]
        public void Drag_LightBodyAcceleratesMoreThanHeavy()
        {
            float paper = WindMath.Drag(Vector3.right * 6f, Vector3.zero, 0.05f, 0.1f, 1f, Big).magnitude;
            float rock = WindMath.Drag(Vector3.right * 6f, Vector3.zero, 50f, 0.1f, 1f, Big).magnitude;
            Assert.Greater(paper, rock * 100f);
        }

        [Test]
        public void Drag_IsCappedAtMaxAcceleration()
        {
            Vector3 a = WindMath.Drag(Vector3.right * 30f, Vector3.zero, 0.01f, 2f, 1f, 45f);
            Assert.AreEqual(45f, a.magnitude, 1e-3f);
        }

        [Test]
        public void Drag_ZeroAreaOrNoRelativeSpeed_IsZero()
        {
            Assert.AreEqual(Vector3.zero, WindMath.Drag(Vector3.right, Vector3.zero, 1f, 0f, 1f, Big));
            Assert.AreEqual(Vector3.zero, WindMath.Drag(Vector3.zero, Vector3.zero, 1f, 1f, 1f, Big));
        }

        [Test]
        public void ProjectedArea_IsLargestFace()
        {
            Assert.AreEqual(6f, WindMath.ProjectedArea(new Vector3(2f, 3f, 1f)), 1e-5f);
        }

        [Test]
        public void Updraft_StrongerAtCenterThanEdge()
        {
            float center = WindMath.UpdraftAcceleration(0f, 0f, 0.5f);
            float edge = WindMath.UpdraftAcceleration(1f, 0f, 0.5f);
            Assert.Greater(center, edge);
            Assert.Greater(WindMath.UpdraftTarget(0f), WindMath.UpdraftTarget(1f));
        }

        [Test]
        public void Updraft_StopsAtMaxSpeed()
        {
            Assert.AreEqual(0f, WindMath.UpdraftAcceleration(0f, WindMath.MaxUpdraftSpeed, 0.5f), 1e-5f);
            Assert.AreEqual(0f, WindMath.UpdraftAcceleration(0f, WindMath.MaxUpdraftSpeed + 3f, 0.5f), 1e-5f);
            Assert.LessOrEqual(WindMath.UpdraftAcceleration(0f, -20f, 0.1f), WindMath.MaxUpdraftAcceleration);
        }

        [Test]
        public void Updraft_LightLiftsMoreThanHeavy_AndHeavyDoesNotBeatGravity()
        {
            float feather = WindMath.UpdraftAcceleration(0f, 0f, 0.1f);
            float rock = WindMath.UpdraftAcceleration(0f, 0f, 50f);
            Assert.Greater(feather, rock);
            Assert.Greater(feather, 9.81f);
            Assert.Less(rock, 9.81f);
        }

        [Test]
        public void SpreadBias_FavorsDownwindAndDampsUpwind()
        {
            float with = WindMath.SpreadBias(1, 0, 1f, 0f, 1f);
            float side = WindMath.SpreadBias(0, 1, 1f, 0f, 1f);
            float against = WindMath.SpreadBias(-1, 0, 1f, 0f, 1f);
            Assert.Greater(with, 1f);
            Assert.AreEqual(1f, side, 1e-5f);
            Assert.Less(against, 1f);
            Assert.GreaterOrEqual(against, WindMath.MinSpreadFactor);
        }

        [Test]
        public void SpreadBias_NoWindStrength_IsNeutral()
        {
            Assert.AreEqual(1f, WindMath.SpreadBias(1, 1, 1f, 0f, 0f), 1e-5f);
        }
    }
}
