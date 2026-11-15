using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Lab;

namespace SpellSimulator.Tests
{
    /// <summary>Parametros visuales del viento del laboratorio: monotonicos en la velocidad y acotados por los topes de Quest.</summary>
    public class WindVisualMathTests
    {
        private const float Step = 0.25f;
        private const float MaxSweep = 40f;

        [Test]
        public void Calm_HasNoParticlesAndLimpSock()
        {
            WindVisualParams p = WindVisualMath.Params(0f, 0f);
            Assert.AreEqual(0, p.ParticleCount);
            Assert.AreEqual(0, p.TrailCount);
            Assert.AreEqual(0f, p.Alpha);
            Assert.AreEqual(0f, p.SockTension);
            Assert.IsFalse(p.Dust);
        }

        [Test]
        public void NegativeOrNaNSpeed_IsCalm()
        {
            Assert.AreEqual(0, WindVisualMath.Params(-5f, 0f).ParticleCount);
            Assert.AreEqual(0, WindVisualMath.Params(float.NaN, 0f).ParticleCount);
        }

        [Test]
        public void Counts_AreMonotonicAndBounded()
        {
            WindVisualParams prev = WindVisualMath.Params(0f, 0f);
            for (float s = Step; s <= MaxSweep; s += Step)
            {
                WindVisualParams p = WindVisualMath.Params(s, 0f);
                Assert.GreaterOrEqual(p.ParticleCount, prev.ParticleCount, $"particulas a {s} m/s");
                Assert.GreaterOrEqual(p.TrailCount, prev.TrailCount, $"estelas a {s} m/s");
                Assert.GreaterOrEqual(p.LeafCount, prev.LeafCount, $"hojas a {s} m/s");
                Assert.GreaterOrEqual(p.DustCount, prev.DustCount, $"polvo a {s} m/s");
                Assert.LessOrEqual(p.ParticleCount, WindVisualMath.MaxParticles);
                Assert.LessOrEqual(p.TrailCount, WindVisualMath.MaxTrails);
                Assert.AreEqual(p.TrailCount + p.LeafCount + p.DustCount, p.ParticleCount);
                prev = p;
            }
        }

        [Test]
        public void Look_IsMonotonicAndBounded()
        {
            WindVisualParams prev = WindVisualMath.Params(Step, 0f);
            for (float s = 2f * Step; s <= MaxSweep; s += Step)
            {
                WindVisualParams p = WindVisualMath.Params(s, 0f);
                Assert.GreaterOrEqual(p.TrailLength, prev.TrailLength);
                Assert.GreaterOrEqual(p.TrailWidth, prev.TrailWidth);
                Assert.GreaterOrEqual(p.Alpha, prev.Alpha);
                Assert.GreaterOrEqual(p.Noise, prev.Noise);
                Assert.GreaterOrEqual(p.SockTension, prev.SockTension);
                Assert.GreaterOrEqual(p.ChevronSpeed, prev.ChevronSpeed);
                Assert.LessOrEqual(p.Lifetime, prev.Lifetime, "la vida baja con la velocidad");
                Assert.That(p.Alpha, Is.InRange(0f, 1f));
                Assert.That(p.SockTension, Is.InRange(0f, 1f));
                Assert.Greater(p.Lifetime, 0f);
                prev = p;
            }
        }

        [Test]
        public void ExtremeSpeed_StaysWithinCaps()
        {
            foreach (float s in new[] { 1000f, float.PositiveInfinity, float.MaxValue })
            {
                WindVisualParams p = WindVisualMath.Params(s, 0f);
                Assert.LessOrEqual(p.ParticleCount, WindVisualMath.MaxParticles);
                Assert.LessOrEqual(p.TrailCount, WindVisualMath.MaxTrails);
                Assert.That(p.SockTension, Is.InRange(0f, 1f));
                Assert.IsFalse(float.IsNaN(p.Lifetime) || float.IsInfinity(p.Lifetime));
                Assert.IsFalse(float.IsInfinity(p.ChevronSpeed));
            }
        }

        [Test]
        public void Dust_OnlyAboveTenMetersPerSecond()
        {
            Assert.IsFalse(WindVisualMath.Params(WindVisualMath.DustSpeed, 0f).Dust);
            Assert.AreEqual(0, WindVisualMath.Params(WindVisualMath.DustSpeed, 0f).DustCount);
            Assert.IsTrue(WindVisualMath.Params(WindVisualMath.DustSpeed + 0.5f, 0f).Dust);
            Assert.Greater(WindVisualMath.Params(20f, 0f).DustCount, 0);
        }

        [Test]
        public void SockIsFullyExtendedAtFullSockSpeed()
        {
            Assert.AreEqual(1f, WindVisualMath.Params(WindVisualMath.FullSockSpeed, 0f).SockTension, 1e-5f);
            Assert.Less(WindVisualMath.Params(WindVisualMath.FullSockSpeed * 0.5f, 0f).SockTension, 1f);
        }

        [Test]
        public void Yaw_IsNormalizedAndMatchesZoneDirection()
        {
            WindVisualParams p = WindVisualMath.Params(6f, -90f);
            Assert.AreEqual(270f, p.YawDeg, 1e-3f);
            Vector3 expected = LabWindZone.DirectionFromYaw(-90f);
            Assert.AreEqual(0f, Vector3.Distance(expected, p.Direction), 1e-4f);
            Assert.AreEqual(1f, WindVisualMath.Params(6f, 90f).Direction.x, 1e-4f); // 90° = hacia +X
        }
    }
}
