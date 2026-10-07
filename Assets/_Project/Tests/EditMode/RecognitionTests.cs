using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Unity.InferenceEngine;
using SpellSimulator.Core;
using SpellSimulator.Glyphs;
using SpellSimulator.Recognition;

namespace SpellSimulator.Tests
{
    public static class TestAssets
    {
        public const string DictionaryPath = "Assets/_Project/Data/GlyphDictionary.asset";
        public const string ModelPath = "Assets/_Project/Data/glyph-recognizer.onnx";

        public static GlyphDictionary Dictionary
        {
            get
            {
                var asset = AssetDatabase.LoadAssetAtPath<GlyphDictionaryAsset>(DictionaryPath);
                Assert.IsNotNull(asset, "Falta GlyphDictionary.asset: correr SpellSimulator/Dictionary/Sync From Reference.");
                return asset.Dictionary;
            }
        }
    }

    public class ClassificationMathTests
    {
        [Test]
        public void Softmax_SumsToOne_AndIsShiftInvariant()
        {
            var logits = new[] { 1f, 2f, 3f, 1001f, 1002f, 1003f };
            var a = new float[3];
            var b = new float[3];
            ClassificationMath.Softmax(logits, 0, 3, a);
            ClassificationMath.Softmax(logits, 3, 3, b);

            Assert.That(a[0] + a[1] + a[2], Is.EqualTo(1f).Within(1e-5f));
            for (int i = 0; i < 3; i++) Assert.That(b[i], Is.EqualTo(a[i]).Within(1e-5f));
            Assert.Greater(a[2], a[1]);
        }

        [Test]
        public void Logits_AreNotProbabilities()
        {
            // Regresion KI-001: un logit de 5 no es confianza 5; tras softmax queda en [0,1].
            var logits = new[] { 5f, 4.9f, -3f };
            var p = new float[3];
            ClassificationMath.Softmax(logits, 0, 3, p);
            Assert.Less(p[0], 0.6f);
        }

        [Test]
        public void AggregateTta_AveragesRotationsPerCandidate()
        {
            // 2 candidatos x 2 rotaciones x 2 clases.
            var logits = new[]
            {
                10f, 0f,   0f, 10f,   // candidato 0: una rotacion vota clase 0, otra clase 1
                0f, 10f,   0f, 10f    // candidato 1: siempre clase 1
            };
            var predictions = ClassificationMath.AggregateTta(logits, 2, 2, 2);

            Assert.That(predictions[0].Probabilities[0], Is.EqualTo(0.5f).Within(1e-3f));
            Assert.AreEqual(1, predictions[1].BestIndex);
            Assert.Greater(predictions[1].Confidence, 0.99f);
        }
    }

    public class TensorRasterizerTests
    {
        [Test]
        public void EmptySession_IsAllBackground()
        {
            float[] t = TensorRasterizer.Rasterize(new DrawingSession());
            Assert.AreEqual(TensorRasterizer.PixelCount, t.Length);
            foreach (float v in t) Assert.AreEqual(-1f, v);
        }

        [Test]
        public void Values_StayInRange_WithAntialiasLevels()
        {
            var s = TestStrokes.Session(TestStrokes.Circle(new Vector2(0.5f, 0.5f), 0.2f));
            bool hasFull = false;
            foreach (float v in TensorRasterizer.Rasterize(s))
            {
                Assert.That(v, Is.InRange(-1f, 1f));
                if (v == 1f) hasFull = true;
            }
            Assert.IsTrue(hasFull);
        }

        [Test]
        public void Output_IsScaleAndPositionInvariant()
        {
            var small = TestStrokes.Session(TestStrokes.Circle(new Vector2(0.5f, 0.5f), 0.05f));
            var big = TestStrokes.Session(TestStrokes.Circle(new Vector2(0.3f, 0.6f), 0.4f));
            float[] a = TensorRasterizer.Rasterize(small);
            float[] b = TensorRasterizer.Rasterize(big);

            int different = 0;
            for (int i = 0; i < a.Length; i++) if (Mathf.Abs(a[i] - b[i]) > 0.6f) different++;
            Assert.Less(different, 40);
        }

        [Test]
        public void TopOfDrawing_IsTopRowOfTensor()
        {
            // Linea horizontal arriba de un punto: la tinta de la linea debe caer en filas bajas (arriba).
            var line = TestStrokes.Line(new Vector2(0.2f, 0.9f), new Vector2(0.8f, 0.9f));
            var dot = TestStrokes.Line(new Vector2(0.5f, 0.1f), new Vector2(0.5f, 0.12f));
            float[] t = TensorRasterizer.Rasterize(TestStrokes.Session(line, dot));

            int margin = Mathf.RoundToInt(TensorRasterizer.Size * TensorRasterizer.Margin / 2f);
            Assert.Greater(t[margin * TensorRasterizer.Size + TensorRasterizer.Size / 2], -1f);
            Assert.AreEqual(-1f, t[(TensorRasterizer.Size - margin) * TensorRasterizer.Size + 2]);
        }

        [Test]
        public void Rotation_180_FlipsTensor()
        {
            var s = TestStrokes.Session(TestStrokes.Line(new Vector2(0.2f, 0.9f), new Vector2(0.8f, 0.9f)),
                                        TestStrokes.Line(new Vector2(0.5f, 0.1f), new Vector2(0.5f, 0.5f)));
            float[] up = TensorRasterizer.Rasterize(s, 0f);
            float[] down = TensorRasterizer.Rasterize(s, 180f);
            Assert.AreNotEqual(up, down);
        }
    }

    public class GlyphDictionaryTests
    {
        [Test]
        public void HasAllModelClasses_AndReferenceRoles()
        {
            var d = TestAssets.Dictionary;
            Assert.AreEqual(26, d.ClassCount);
            Assert.AreEqual(GlyphKind.Sigil, d.Get("fire").Kind);
            Assert.AreEqual("fire", d.Get("fire").Element);
            Assert.AreEqual(GlyphKind.Sign, d.Get("column").Kind);
            Assert.AreEqual("column", d.Get("column").Semantic.manifestation);
            Assert.AreEqual("inward", d.Get("column").Semantic.directionMode);
            Assert.AreEqual(GlyphKind.Unknown, d.Get("orb").Kind, "orb no tiene entrada en el diccionario.");
            Assert.AreEqual("fire", d.Get(d.IndexOf("fire")).Id);
        }

        [Test]
        public void Templates_AreParsedWithYUp()
        {
            var fire = TestAssets.Dictionary.Get("fire");
            Assert.Greater(fire.TemplateStrokes.Count, 0);
            foreach (var stroke in fire.TemplateStrokes)
                foreach (var p in stroke)
                    Assert.That(p.y, Is.InRange(-0.01f, 1.01f));
        }
    }

    /// <summary>
    /// End-to-end: las plantillas del diccionario, rasterizadas como lo hace el juego, deben reconocerse con el
    /// modelo real. Valida rasterizador + modelo + softmax + TTA + mapa de clases de una sola vez.
    /// </summary>
    public class ModelTemplateTests
    {
        [Test]
        public void DictionaryTemplates_AreRecognizedByModel()
        {
            var dictionary = TestAssets.Dictionary;
            var modelAsset = AssetDatabase.LoadAssetAtPath<ModelAsset>(TestAssets.ModelPath);
            Assert.IsNotNull(modelAsset);

            var entries = new List<GlyphEntry>();
            var sessions = new List<DrawingSession>();
            foreach (var entry in dictionary.Entries)
            {
                if (entry.TemplateStrokes.Count == 0) continue;
                entries.Add(entry);
                sessions.Add(TestStrokes.FromTemplate(entry.TemplateStrokes, new Vector2(0.5f, 0.5f), 0.2f));
            }

            float[] rotations = ClassificationMath.TtaRotationsDeg;
            int batch = sessions.Count * rotations.Length;
            var input = new float[batch * TensorRasterizer.PixelCount];
            for (int c = 0; c < sessions.Count; c++)
                for (int r = 0; r < rotations.Length; r++)
                    TensorRasterizer.Rasterize(sessions[c], rotations[r], input, (c * rotations.Length + r) * TensorRasterizer.PixelCount);

            GlyphPrediction[] predictions;
            using (var worker = new Worker(ModelLoader.Load(modelAsset), BackendType.CPU))
            using (var tensor = new Tensor<float>(new TensorShape(batch, 1, TensorRasterizer.Size, TensorRasterizer.Size), input))
            {
                worker.Schedule(tensor);
                float[] logits = (worker.PeekOutput("class_logits") as Tensor<float>).DownloadToArray();
                predictions = ClassificationMath.AggregateTta(logits, sessions.Count, rotations.Length, logits.Length / batch);
            }

            int hits = 0;
            var misses = new StringBuilder();
            for (int i = 0; i < entries.Count; i++)
            {
                string predicted = dictionary.Get(predictions[i].BestIndex).Id;
                if (predicted == entries[i].Id) hits++;
                else misses.Append($" {entries[i].Id}->{predicted}({predictions[i].Confidence:F2})");
            }

            Debug.Log($"[Test] Plantillas reconocidas: {hits}/{entries.Count}.{misses}");
            Assert.GreaterOrEqual(hits, Mathf.CeilToInt(entries.Count * 0.85f), $"Fallos:{misses}");
        }
    }
}
