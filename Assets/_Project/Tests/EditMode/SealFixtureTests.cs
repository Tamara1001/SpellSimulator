using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Unity.InferenceEngine;
using SpellSimulator.Casting;
using SpellSimulator.Compiler;
using SpellSimulator.Core;
using SpellSimulator.Output;
using SpellSimulator.Recognition;

namespace SpellSimulator.Tests
{
    /// <summary>
    /// Sellos reales grabados en pruebas manuales (SealRecord), reproducidos con el modelo (CPU).
    /// Para sumar uno: copiar el .json de persistentDataPath/Sellos a Tests/Fixtures y agregar "expect".
    /// </summary>
    public class SealFixtureTests
    {
        public const string FixturesFolder = "Assets/_Project/Tests/Fixtures";
        private const string NoFixtures = "(sin fixtures)";
        private GlyphClassifier classifier;

        public static IEnumerable<string> Fixtures()
        {
            string[] files = Directory.Exists(FixturesFolder) ? Directory.GetFiles(FixturesFolder, "*.json") : new string[0];
            // NUnit falla si la fuente esta vacia: sin fixtures se reporta un caso ignorado.
            if (files.Length == 0) yield return NoFixtures;
            foreach (string path in files) yield return Path.GetFileName(path);
        }

        [OneTimeSetUp]
        public void LoadModel()
        {
            var model = AssetDatabase.LoadAssetAtPath<ModelAsset>(TestAssets.ModelPath);
            classifier = new GlyphClassifier(model, BackendType.CPU, useRotationTta: true);
            classifier.CalibratePose(TestAssets.Dictionary.TemplateSessions());
        }

        [OneTimeTearDown]
        public void DisposeModel() => classifier.Dispose();

        [TestCaseSource(nameof(Fixtures))]
        public void Fixture_MatchesExpectation(string file)
        {
            if (file == NoFixtures) Assert.Ignore("Todavia no hay sellos grabados en " + FixturesFolder);
            var record = SealRecord.FromJson(File.ReadAllText(Path.Combine(FixturesFolder, file)));
            var cleaned = StrokeCleaner.CleanSession(record.IsotropicSession(), StrokeCleaner.DefaultTolerance, StrokeCleaner.DefaultSplineResolution);
            var reading = SealReader.Read(cleaned, classifier, TestAssets.Dictionary, 0f);
            var spell = reading.Spell;

            var signs = new List<string>();
            if (spell.plan != null) foreach (var s in spell.plan.Signs) signs.Add(PlanResolver.DescribeSign(s));
            string got = $"{file}: {spell.status} sigilo={reading.Ast.PrimarySigil?.Id} plan={spell.plan?.Primitives} signos=[{string.Join("; ", signs)}]";
            Debug.Log("[Fixture] " + got);

            if (record.ExpectSigil != null) Assert.AreEqual(record.ExpectSigil, reading.Ast.PrimarySigil?.Id, got);
            if (record.ExpectPrimitives != null) Assert.AreEqual(record.ExpectPrimitives, spell.plan?.Primitives.ToString(), got);
        }

        [Test]
        public void Annotations_MarkSigilSignsAndWarnings()
        {
            var record = SealRecord.FromJson(File.ReadAllText(Path.Combine(FixturesFolder, "fire_columnas_bien.json")));
            var cleaned = StrokeCleaner.CleanSession(record.IsotropicSession(), StrokeCleaner.DefaultTolerance, StrokeCleaner.DefaultSplineResolution);
            var reading = SealReader.Read(cleaned, classifier, TestAssets.Dictionary, 0f);

            var items = SealAnnotationLayout.Build(reading.Ast, reading.Spell);

            Assert.AreEqual(AnnotationKind.Sigil, items[0].Kind);
            StringAssert.StartsWith("fire", items[0].Label);
            Assert.AreEqual(3, items.Count, "sigilo + 2 signos");
            foreach (var item in items)
                Assert.Less((item.Position - reading.Ast.Ring.Center).magnitude, reading.Ast.Ring.Radius, $"{item.Label} dentro del anillo");
            // La columna de la derecha apunta hacia afuera (barra del lado del sigilo).
            var right = items.Find(i => i.Label.StartsWith("column"));
            Assert.Greater(right.Facing.x, 0.7f);
            CollectionAssert.IsNotEmpty(SealAnnotationLayout.Warnings(reading.Spell));
        }

        [Test]
        public void Record_RoundTrips()
        {
            var record = new SealRecord { Iso = new Vector2(1f, 0.75f), Summary = "a \"b\"\nc" };
            record.Session.AddStroke(new Stroke(new List<Vector2> { new Vector2(0.1f, 0.2f), new Vector2(0.30001f, 0.4f) }));
            record.Session.AddStroke(new Stroke(new List<Vector2> { new Vector2(0.5f, 0.5f) }));

            var back = SealRecord.FromJson(record.ToJson());

            Assert.AreEqual(record.Iso, back.Iso);
            Assert.AreEqual(record.Summary, back.Summary);
            Assert.AreEqual(2, back.Session.Strokes.Count);
            Assert.AreEqual(0.30001f, back.Session.Strokes[0].Points[1].x, 1e-5f);
            Assert.IsNull(back.ExpectSigil);
        }
    }
}
