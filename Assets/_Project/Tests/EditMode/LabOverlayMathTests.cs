using NUnit.Framework;
using SpellSimulator.Lab;
using UnityEngine;

namespace SpellSimulator.Tests
{
    public class LabOverlayMathTests
    {
        // Distancias al origen: indice 0 -> 4, 1 -> 1, 2 -> 5, 3 -> 2, 4 -> 3.
        private static readonly Vector3[] Positions =
        {
            new Vector3(4f, 0f, 0f),
            new Vector3(0f, 1f, 0f),
            new Vector3(0f, 0f, -5f),
            new Vector3(-2f, 0f, 0f),
            new Vector3(0f, 0f, 3f)
        };

        [Test]
        public void EligeLosNMasCercanosOrdenados()
        {
            var indices = new int[2];
            var scratch = new float[2];
            int n = LabOverlayMath.SelectNearest(Vector3.zero, Positions, Positions.Length, 10f, indices, scratch);
            Assert.AreEqual(2, n);
            Assert.AreEqual(1, indices[0]);
            Assert.AreEqual(3, indices[1]);
            Assert.AreEqual(1f, scratch[0], 1e-5f);
            Assert.AreEqual(4f, scratch[1], 1e-5f);
        }

        [Test]
        public void RespetaElRadio()
        {
            var indices = new int[8];
            var scratch = new float[8];
            int n = LabOverlayMath.SelectNearest(Vector3.zero, Positions, Positions.Length, 3.5f, indices, scratch);
            Assert.AreEqual(3, n);
            Assert.AreEqual(1, indices[0]);
            Assert.AreEqual(3, indices[1]);
            Assert.AreEqual(4, indices[2]);
        }

        [Test]
        public void MenosCandidatosQueN()
        {
            var indices = new int[8];
            var scratch = new float[8];
            int n = LabOverlayMath.SelectNearest(Vector3.zero, Positions, Positions.Length, 100f, indices, scratch);
            Assert.AreEqual(5, n);
            CollectionAssert.AreEqual(new[] { 1, 3, 4, 0, 2 }, new[] { indices[0], indices[1], indices[2], indices[3], indices[4] });
        }

        [Test]
        public void SoloMiraLosPrimerosCount()
        {
            var indices = new int[4];
            var scratch = new float[4];
            int n = LabOverlayMath.SelectNearest(Vector3.zero, Positions, 3, 100f, indices, scratch);
            Assert.AreEqual(3, n);
            CollectionAssert.AreEqual(new[] { 1, 0, 2 }, new[] { indices[0], indices[1], indices[2] });
        }

        [Test]
        public void SinCandidatosOArraysVacios()
        {
            var indices = new int[4];
            var scratch = new float[4];
            Assert.AreEqual(0, LabOverlayMath.SelectNearest(Vector3.zero, Positions, Positions.Length, 0.5f, indices, scratch));
            Assert.AreEqual(0, LabOverlayMath.SelectNearest(Vector3.zero, Positions, 0, 10f, indices, scratch));
            Assert.AreEqual(0, LabOverlayMath.SelectNearest(Vector3.zero, Positions, 5, 10f, new int[0], scratch));
            Assert.AreEqual(0, LabOverlayMath.SelectNearest(Vector3.zero, null, 5, 10f, indices, scratch));
        }

        [Test]
        public void CentroDesplazado()
        {
            var indices = new int[1];
            var scratch = new float[1];
            int n = LabOverlayMath.SelectNearest(new Vector3(0f, 0f, -4f), Positions, Positions.Length, 10f, indices, scratch);
            Assert.AreEqual(1, n);
            Assert.AreEqual(2, indices[0]);
        }
    }
}
