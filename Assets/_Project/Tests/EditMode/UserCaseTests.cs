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
            classifier.CalibratePose(TestAssets.Dictionary.TemplateSessions());
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

        private static void AddSign(DrawingSession session, string id, Vector2 at, float rotationDeg)
        {
            foreach (var s in PoseTests.Rotated(id, rotationDeg).Strokes)
            {
                var stroke = new Stroke();
                foreach (var p in s.Points) stroke.AddPoint((p - new Vector2(0.5f, 0.5f)) * 0.6f + at);
                session.AddStroke(stroke);
            }
        }

        [Test]
        public void InwardColumnsTopAndBottom_JetStraightOut()
        {
            // Plantilla de signo = abajo apuntando arriba. Arriba hay que rotarla 180 para que apunte adentro.
            var session = Template("fire", C, 0.22f);
            session.AddStroke(TestStrokes.Circle(C, 0.4f, 64));
            AddSign(session, "column", C + new Vector2(0f, -0.3f), 0f);
            AddSign(session, "column", C + new Vector2(0f, 0.3f), 180f);

            var plan = Read(session).Spell.plan;

            Assert.Greater(plan.Aim.z, 0.5f, "Columnas opuestas hacia adentro chocan y salen del papel.");
            Assert.Less(new Vector2(plan.Aim.x, plan.Aim.y).magnitude, 0.3f * plan.Aim.z);
        }

        [Test]
        public void BothColumnsPointingUp_PushUpAlongThePaper()
        {
            var session = Template("fire", C, 0.22f);
            session.AddStroke(TestStrokes.Circle(C, 0.4f, 64));
            AddSign(session, "column", C + new Vector2(0f, -0.3f), 0f);
            AddSign(session, "column", C + new Vector2(0f, 0.3f), 0f);

            var plan = Read(session).Spell.plan;

            Assert.Greater(plan.Aim.y, 0.5f, "Ambas apuntan hacia arriba: empuje lateral hacia arriba.");
            Assert.That(plan.Aim.z, Is.LessThan(0.2f));
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
