using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Unity.InferenceEngine;
using SpellSimulator.Core;
using SpellSimulator.Recognition;

namespace SpellSimulator.Tests
{
    /// <summary>La cabeza de pose recupera la rotacion de un glifo respecto de su plantilla.</summary>
    public class PoseTests
    {
        private GlyphClassifier classifier;

        [OneTimeSetUp]
        public void LoadModel()
        {
            classifier = new GlyphClassifier(AssetDatabase.LoadAssetAtPath<ModelAsset>(TestAssets.ModelPath), BackendType.CPU, true);
            classifier.CalibratePose(TestAssets.Dictionary.TemplateSessions());
        }

        [OneTimeTearDown]
        public void DisposeModel() => classifier.Dispose();

        public static DrawingSession Rotated(string id, float degrees)
        {
            var session = TestStrokes.FromTemplate(TestAssets.Dictionary.Get(id).TemplateStrokes, new Vector2(0.5f, 0.5f), 0.2f);
            var rotated = new DrawingSession();
            foreach (var s in session.Strokes)
            {
                var stroke = new Stroke();
                foreach (var p in s.Points) stroke.AddPoint(GeometryUtils.RotatePointAroundPivot(p, new Vector2(0.5f, 0.5f), degrees));
                rotated.AddStroke(stroke);
            }
            return rotated;
        }

        [Test]
        public void IsCalibrated() => Assert.IsTrue(classifier.IsPoseCalibrated);

        [TestCase("column", 0f)]
        [TestCase("column", 90f)]
        [TestCase("column", 180f)]
        [TestCase("column", 270f)]
        [TestCase("column", 45f)]
        [TestCase("pull", 90f)]
        [TestCase("levitation", 180f)]
        public void RecoversRotation(string id, float degrees)
        {
            var prediction = classifier.Classify(new List<DrawingSession> { Rotated(id, degrees) })[0];
            Assert.AreEqual(id, TestAssets.Dictionary.Get(prediction.BestIndex).Id);
            Assert.IsTrue(prediction.HasRotation);
            float error = Mathf.Abs(PoseMath.SignedDelta(degrees, prediction.RotationDeg));
            Assert.Less(error, 25f, $"{id} rotado {degrees}: pose {prediction.RotationDeg:F1}");
        }
    }
}
