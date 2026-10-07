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
    /// <summary>Casos reales reportados en pruebas manuales, end-to-end con el modelo (CPU).</summary>
    public class UserCaseTests
    {
        private static readonly Vector2 C = new Vector2(0.5f, 0.5f);
        private GlyphClassifier classifier;

        [OneTimeSetUp]
        public void LoadModel()
        {
            var model = AssetDatabase.LoadAssetAtPath<ModelAsset>(TestAssets.ModelPath);
            classifier = new GlyphClassifier(model, BackendType.CPU, useRotationTta: true);
        }

        [OneTimeTearDown]
        public void DisposeModel() => classifier.Dispose();

        private SealReading Read(DrawingSession session)
        {
            var cleaned = StrokeCleaner.CleanSession(session, StrokeCleaner.DefaultTolerance, StrokeCleaner.DefaultSplineResolution);
            return SealReader.Read(cleaned, classifier, TestAssets.Dictionary, 0f);
        }

        private static DrawingSession Template(string id, Vector2 center, float size) =>
            TestStrokes.FromTemplate(TestAssets.Dictionary.Get(id).TemplateStrokes, center, size);

        [TestCase("fire", ElementType.Fire)]
        [TestCase("water", ElementType.Water)]
        [TestCase("earth", ElementType.Earth)]
        [TestCase("light", ElementType.Light)]
        [TestCase("wind-directs-air", ElementType.Wind)]
        public void BigSigilInSmallRing_CastsItsElement(string sigil, ElementType element)
        {
            // 2026-10-07: el fuego ocupaba ~65% del anillo y sus lineas cruzadas se tomaron como dos signos "region".
            var session = Template(sigil, C, 0.36f);
            session.AddStroke(TestStrokes.Circle(C, 0.28f, 64));

            var reading = Read(session);

            Assert.IsTrue(reading.Spell.valid, $"{sigil}: {reading.Spell.status} {string.Join(",", reading.Spell.warnings)}");
            Assert.AreEqual(element, reading.Spell.element);
            Assert.IsEmpty(reading.Ast.Signs, $"{sigil}: el sigilo no debe partirse en signos.");
        }

        [TestCase("fire", ElementType.Fire)]
        [TestCase("water", ElementType.Water)]
        public void RegularSigil_WithTwoColumns(string sigil, ElementType element)
        {
            var session = Template(sigil, C, 0.24f);
            session.AddStroke(TestStrokes.Circle(C, 0.4f, 64));
            foreach (var s in Template("column", C + new Vector2(0f, -0.3f), 0.1f).Strokes) session.AddStroke(s);
            foreach (var s in Template("column", C + new Vector2(0f, 0.3f), 0.1f).Strokes) session.AddStroke(s);

            var reading = Read(session);

            Assert.IsTrue(reading.Spell.valid, reading.Spell.status);
            Assert.AreEqual(element, reading.Spell.element);
            Assert.AreEqual(2, reading.Ast.Signs.Count + reading.Ast.Unknowns.Count, "Los dos signos quedan separados del sigilo.");
        }

        [TestCase("fire")]
        [TestCase("water")]
        [TestCase("light")]
        public void SigilAlone_IsNotARing(string sigil)
        {
            // KI-032: las curvas de un glifo no deben leerse como anillo.
            Assert.IsFalse(SealParser.Parse(Template(sigil, C, 0.4f)).Ring.Found, $"{sigil} solo no es un anillo.");
        }
    }
}
