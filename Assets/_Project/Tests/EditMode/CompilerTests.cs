using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Compiler;
using SpellSimulator.Core;
using SpellSimulator.Glyphs;
using SpellSimulator.Parsing;
using SpellSimulator.Recognition;

namespace SpellSimulator.Tests
{
    /// <summary>
    /// Pipeline sin red neuronal: geometria sintetica -> SealParser -> predicciones fabricadas -> GlyphAST -> SpellIR.
    /// </summary>
    public class CompilerTests
    {
        private static readonly Vector2 C = new Vector2(0.5f, 0.5f);
        private GlyphDictionary dictionary;

        [SetUp]
        public void SetUp() => dictionary = TestAssets.Dictionary;

        private class Seal
        {
            public readonly List<Stroke> Strokes = new List<Stroke>();
            // Ids que el "modelo" predice, en el orden de ParsedSeal.AllCandidates() (sigilo primero).
            public readonly List<(string id, float confidence)> Predictions = new List<(string, float)>();
        }

        private static Seal NewSeal(bool closedRing = true)
        {
            var seal = new Seal();
            seal.Strokes.Add(TestStrokes.Circle(C, 0.4f, 64, closedRing ? 0f : 40f));
            return seal;
        }

        private static void AddSigil(Seal seal, string id, float confidence = 0.9f, float size = 0.24f)
        {
            // Cuadrado chico en el centro: la forma no importa, la prediccion es fabricada.
            float h = size * 0.4f;
            var s = new Stroke();
            foreach (var p in new[] { new Vector2(-h, -h), new Vector2(h, -h), new Vector2(h, h), new Vector2(-h, h), new Vector2(-h, -h) })
                s.AddPoint(C + p);
            seal.Strokes.Add(s);
            seal.Predictions.Insert(0, (id, confidence));
        }

        private static void AddSign(Seal seal, string id, float angleDeg, float confidence = 0.9f)
        {
            Vector2 at = C + GeometryUtils.VectorFromAngleDeg(angleDeg) * 0.3f;
            Vector2 tangent = GeometryUtils.VectorFromAngleDeg(angleDeg + 90f) * 0.04f;
            seal.Strokes.Add(TestStrokes.Line(at - tangent, at + tangent));
            seal.Predictions.Add((id, confidence));
        }

        private SpellIR Compile(Seal seal, out GlyphAST ast)
        {
            var parsed = SealParser.Parse(TestStrokes.Session(seal.Strokes.ToArray()));
            var predictions = new List<GlyphPrediction>();
            foreach (var (id, confidence) in seal.Predictions) predictions.Add(Fake(id, confidence));
            Assert.AreEqual(predictions.Count, new List<SymbolCandidate>(parsed.AllCandidates()).Count, "Candidatos != predicciones");

            ast = GlyphAstBuilder.Build(parsed, predictions, dictionary);
            return SpellCompiler.Compile(ast, 10f);
        }

        private GlyphPrediction Fake(string id, float confidence)
        {
            var p = new float[dictionary.ClassCount];
            float rest = (1f - confidence) / (p.Length - 1);
            for (int i = 0; i < p.Length; i++) p[i] = rest;
            p[dictionary.IndexOf(id)] = confidence;
            return new GlyphPrediction(p);
        }

        [Test]
        public void ClosedRingWithFire_IsActiveAura()
        {
            var seal = NewSeal();
            AddSigil(seal, "fire");
            var spell = Compile(seal, out _);

            Assert.IsTrue(spell.valid, spell.status);
            Assert.IsTrue(spell.active);
            Assert.IsFalse(spell.prepared);
            Assert.AreEqual(ElementType.Fire, spell.element);
            Assert.AreEqual("fire", spell.sigilId);
            Assert.AreEqual(Manifestations.Aura, spell.primaryManifestation);
            Assert.AreEqual(1f, spell.gravity);
            Assert.AreEqual(10f, spell.activatedAt);
            Assert.That(spell.duration, Is.InRange(0.65f, 8.5f));
            Assert.AreEqual(Vector3.forward, spell.direction);
        }

        [Test]
        public void OpenRing_IsPreparedNotActive()
        {
            var seal = NewSeal(closedRing: false);
            AddSigil(seal, "water");
            var spell = Compile(seal, out _);

            Assert.IsTrue(spell.valid);
            Assert.IsFalse(spell.active);
            Assert.IsTrue(spell.prepared);
            Assert.Contains(GlyphWarnings.RingIncomplete, spell.warnings);
            Assert.AreEqual(-1f, spell.activatedAt);
        }

        [Test]
        public void NoRing_IsInvalid()
        {
            var seal = new Seal();
            AddSigil(seal, "fire");
            var spell = Compile(seal, out _);

            Assert.IsFalse(spell.valid);
            Assert.Contains(GlyphWarnings.NoRingDetected, spell.warnings);
        }

        [Test]
        public void LowConfidenceSigil_IsInvalid()
        {
            var seal = NewSeal();
            AddSigil(seal, "fire", confidence: 0.55f);
            var spell = Compile(seal, out _);

            Assert.IsFalse(spell.valid);
            Assert.Contains(GlyphWarnings.PrimarySigilConfidenceLow, spell.warnings);
        }

        [Test]
        public void MissingSigil_IsInvalid()
        {
            var seal = NewSeal();
            AddSign(seal, "column", 270f);
            var spell = Compile(seal, out _);

            Assert.IsFalse(spell.valid);
            Assert.Contains(GlyphWarnings.MissingPrimarySigil, spell.warnings);
        }

        [Test]
        public void TwoSigils_AreUnsupported()
        {
            var seal = NewSeal();
            AddSigil(seal, "fire");
            AddSign(seal, "water", 0f); // un sigilo dibujado afuera del centro
            var spell = Compile(seal, out var ast);

            Assert.AreEqual(1, ast.ExtraSigils.Count);
            Assert.IsFalse(spell.valid);
            Assert.Contains(GlyphWarnings.UnsupportedMultipleSigils, spell.warnings);
        }

        [Test]
        public void UnknownClass_IsIgnoredButReported()
        {
            var seal = NewSeal();
            AddSigil(seal, "fire");
            AddSign(seal, "orb", 90f);
            var spell = Compile(seal, out var ast);

            Assert.IsTrue(spell.valid);
            Assert.AreEqual(1, ast.Unknowns.Count);
            Assert.AreEqual("unknown_class", ast.Unknowns[0].RejectReason);
        }

        [Test]
        public void Levitation_ReducesGravity()
        {
            var seal = NewSeal();
            AddSigil(seal, "wind-directs-air");
            AddSign(seal, "levitation", 0f);
            AddSign(seal, "levitation", 180f);
            var spell = Compile(seal, out _);

            Assert.IsTrue(spell.valid, spell.status);
            Assert.AreEqual(ElementType.Wind, spell.element);
            Assert.AreEqual(Manifestations.Levitation, spell.primaryManifestation);
            Assert.Less(spell.gravity, 1f);
        }

        [Test]
        public void InwardColumnAtBottom_TiltsUp()
        {
            var seal = NewSeal();
            AddSigil(seal, "fire");
            AddSign(seal, "column", 270f);
            var spell = Compile(seal, out _);

            Assert.AreEqual(Manifestations.Column, spell.primaryManifestation);
            Assert.Greater(spell.direction.y, 0.1f, "Column es inward: un signo abajo empuja hacia arriba.");
            Assert.Greater(spell.direction.z, 0f);
            Assert.That(Mathf.Abs(spell.direction.x), Is.LessThan(0.05f));
            Assert.Greater(spell.directionCoherence, 0.9f);
        }

        [Test]
        public void OpposedColumns_CancelDirection()
        {
            var seal = NewSeal();
            AddSigil(seal, "fire");
            AddSign(seal, "column", 270f);
            AddSign(seal, "column", 90f);
            var spell = Compile(seal, out _);

            Assert.Less(spell.directionCoherence, 0.1f);
        }

        [Test]
        public void SignsIncreaseForce()
        {
            var bare = NewSeal();
            AddSigil(bare, "fire");
            var withSigns = NewSeal();
            AddSigil(withSigns, "fire");
            AddSign(withSigns, "crush", 0f);
            AddSign(withSigns, "crush", 180f);

            Assert.Greater(Compile(withSigns, out _).force, Compile(bare, out _).force);
        }

        [Test]
        public void BiggerSigil_HasBiggerEffect()
        {
            var small = NewSeal();
            AddSigil(small, "fire", size: 0.15f);
            var big = NewSeal();
            AddSigil(big, "fire", size: 0.3f);

            Assert.Greater(Compile(big, out _).effectScale, Compile(small, out _).effectScale);
        }
    }
}
