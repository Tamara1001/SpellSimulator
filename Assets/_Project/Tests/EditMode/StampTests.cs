using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Unity.InferenceEngine;
using SpellSimulator.Casting;
using SpellSimulator.Compiler;
using SpellSimulator.Core;
using SpellSimulator.Parsing;
using SpellSimulator.Recognition;

namespace SpellSimulator.Tests
{
    /// <summary>Estampas: el sigilo se estampa con el anillo abierto (preparado) y al cerrarlo se lanza.</summary>
    public class StampTests
    {
        private GlyphClassifier classifier;
        private static readonly Vector2 CanvasSize = new Vector2(1f, 1f);

        [OneTimeSetUp]
        public void LoadModel()
        {
            var model = AssetDatabase.LoadAssetAtPath<ModelAsset>(TestAssets.ModelPath);
            classifier = new GlyphClassifier(model, BackendType.CPU, useRotationTta: true);
            classifier.CalibratePose(TestAssets.Dictionary.TemplateSessions());
        }

        [OneTimeTearDown]
        public void DisposeModel() => classifier.Dispose();

        private SealReading Read(DrawingSession session)
        {
            var cleaned = StrokeCleaner.CleanSession(session, StrokeCleaner.DefaultTolerance, StrokeCleaner.DefaultSplineResolution);
            return SealReader.Read(cleaned, classifier, TestAssets.Dictionary, 0f);
        }

        private static DrawingSession Stamp(string sigil, float diameter)
        {
            var session = new DrawingSession();
            foreach (var s in StampPattern.Sigil(TestAssets.Dictionary.Get(sigil).TemplateStrokes, new Vector2(0.5f, 0.5f), diameter, CanvasSize, 0.01f))
                session.AddStroke(s);
            return session;
        }

        [Test]
        public void StampedSigil_LeavesTheRingOpen_ButFound()
        {
            var ring = RingDetector.Detect(Stamp("fire", 0.6f));
            Assert.IsTrue(ring.Found, "El anillo estampado se detecta.");
            Assert.IsFalse(ring.Complete, "Canon: se estampa abierto.");
            Assert.AreEqual(0.3f, ring.Radius, 0.03f);
        }

        [Test]
        public void StampedSigil_ClosedByHand_CastsItsElement()
        {
            var session = Stamp("water", 0.6f);
            Assert.IsFalse(Read(session).Spell.active, "Abierto: preparado, no lanzado.");

            // Cerrar el hueco a mano (arco arriba).
            var close = new Stroke();
            for (int i = 0; i <= 10; i++)
            {
                float a = (70f + 40f * i / 10f) * Mathf.Deg2Rad;
                close.AddPoint(new Vector2(0.5f, 0.5f) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.3f);
            }
            session.AddStroke(close);
            var reading = Read(session);
            Assert.IsTrue(reading.Spell.valid && reading.Spell.active, reading.Spell.status);
            Assert.AreEqual(ElementType.Water, reading.Spell.element);
        }

        [Test]
        public void StampedSigns_PointInward_AndMakeAJet()
        {
            var session = Stamp("fire", 0.8f);
            var center = new Vector2(0.5f, 0.5f);
            var column = TestAssets.Dictionary.Get("column").TemplateStrokes;
            foreach (var at in new[] { center + new Vector2(0f, -0.28f), center + new Vector2(0f, 0.28f) })
                foreach (var s in StampPattern.Sign(column, at, 0.4f, center, CanvasSize, 0.01f)) session.AddStroke(s);
            // Cerrar el anillo.
            var close = new Stroke();
            for (int i = 0; i <= 10; i++)
            {
                float a = (70f + 40f * i / 10f) * Mathf.Deg2Rad;
                close.AddPoint(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.4f);
            }
            session.AddStroke(close);

            var reading = Read(session);
            Assert.IsTrue(reading.Spell.active, reading.Spell.status);
            Assert.IsTrue(reading.Spell.plan.Has(PlanPrimitive.Jet), $"Columnas estampadas hacia adentro = chorro. Plan: {reading.Spell.plan.Primitives}");
        }
    }
}
