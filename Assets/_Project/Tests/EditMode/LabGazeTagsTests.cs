using NUnit.Framework;
using SpellSimulator.Lab;
using UnityEngine;

namespace SpellSimulator.Tests
{
    public class LabGazeTagsTests
    {
        [Test]
        public void PuntuaDentroDelRangoYRechazaFuera()
        {
            Assert.AreEqual(10.002f, LabGazeTags.Score(10f, 2f), 0.0001f);
            Assert.IsTrue(float.IsPositiveInfinity(LabGazeTags.Score(26f, 2f)));
            Assert.IsTrue(float.IsPositiveInfinity(LabGazeTags.Score(10f, 4.01f)));
        }

        [Test]
        public void SeleccionaHastaCuatroMasCercanosAlCentro()
        {
            Vector3[] points = { P(5), P(2), P(20), P(12), P(8), P(1) };
            var selected = new int[4]; var scores = new float[4]; var visible = new bool[6];
            int n = LabGazeTags.Select(Vector3.zero, Vector3.forward, points, points.Length, visible, selected, scores);
            Assert.AreEqual(4, n);
            CollectionAssert.AreEqual(new[] { 5, 1, 0, 4 }, selected);
        }

        [Test]
        public void UsaHisteresisAlSalirDelCono()
        {
            Vector3[] points = { new Vector3(0.5f, 0f, 1f) };
            var selected = new int[4]; var scores = new float[4]; var visible = new[] { true };
            Assert.AreEqual(1, LabGazeTags.Select(Vector3.zero, Vector3.forward, points, 1, visible, selected, scores));
            points[0] = new Vector3(0.62f, 0f, 1f);
            Assert.AreEqual(1, LabGazeTags.Select(Vector3.zero, Vector3.forward, points, 1, visible, selected, scores));
            points[0] = new Vector3(0.64f, 0f, 1f);
            Assert.AreEqual(0, LabGazeTags.Select(Vector3.zero, Vector3.forward, points, 1, visible, selected, scores));
        }

        [Test]
        public void ManejaEntradasInvalidasYLimiteCero()
        {
            Assert.AreEqual(0, LabGazeTags.Select(Vector3.zero, Vector3.forward, null, 0, null, null, null));
            Assert.AreEqual(0, LabGazeTags.Select(Vector3.zero, Vector3.forward, new Vector3[1], 1,
                new bool[1], new int[0], new float[0]));
        }

        private static Vector3 P(float angle) => Quaternion.Euler(0f, angle, 0f) * (Vector3.forward * 2f);
    }
}
