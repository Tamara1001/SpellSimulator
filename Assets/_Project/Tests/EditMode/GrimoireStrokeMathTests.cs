using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Learning;

namespace SpellSimulator.Tests
{
    /// <summary>Remuestreo por longitud de arco del trazo animado del grimorio (velocidad pareja).</summary>
    public class GrimoireStrokeMathTests
    {
        [Test]
        public void Resample_IsEvenlySpaced_AndKeepsEndpoints()
        {
            var line = new List<Vector2> { new Vector2(0f, 0f), new Vector2(0.01f, 0f), new Vector2(0.1f, 0f) };
            var output = new List<Vector2>();
            GrimoireStrokeMath.Resample(line, 0.005f, output);
            Assert.AreEqual(line[0], output[0]);
            Assert.AreEqual(0.1f, output[output.Count - 1].x, 1e-5f);
            float gap = output[1].x - output[0].x;
            for (int i = 2; i < output.Count; i++) Assert.AreEqual(gap, output[i].x - output[i - 1].x, 1e-4f);
        }

        [Test]
        public void Smooth_KeepsEndpoints_AndCount()
        {
            var zigzag = new List<Vector2>();
            for (int i = 0; i < 20; i++) zigzag.Add(new Vector2(i, i % 2));
            var output = new List<Vector2>();
            GrimoireStrokeMath.Smooth(zigzag, 3, output);
            Assert.AreEqual(zigzag.Count, output.Count);
            Assert.AreEqual(zigzag[0], output[0]);
            Assert.AreEqual(zigzag[19], output[19]);
        }
    }
}
