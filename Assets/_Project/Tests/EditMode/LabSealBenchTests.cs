using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Lab;
using SpellSimulator.Learning;

namespace SpellSimulator.Tests
{
    public class LabSealBenchTests
    {
        private static readonly List<string> Recipes = new List<string> { "gust", "blaze", LabSealBenchMath.FreeRecipeId };
        private static readonly List<string> Sigils = new List<string> { "fire", "water", "wind-directs-air" };
        private static readonly List<string> Signs = new List<string> { "column", "dispersion" };

        private const string BlazeJson = "{\"id\":\"blaze\",\"name\":\"Llamarada\",\"element\":\"fire\",\"sigil\":\"fire\"," +
            "\"ring\":{\"gapDeg\":34},\"signs\":[{\"id\":\"column\",\"angle\":0,\"turn\":0},{\"id\":\"column\",\"angle\":180,\"turn\":0}]}";

        private const string MixedJson = "{\"id\":\"mix\",\"name\":\"Mezcla\",\"element\":\"fire\",\"sigil\":\"fire\"," +
            "\"signs\":[{\"id\":\"column\",\"angle\":90,\"turn\":180},{\"id\":\"dispersion\",\"angle\":200,\"turn\":0}]}";

        [Test]
        public void GetYWith_IdaYVuelta()
        {
            LabSealSpec s = LabSealSpec.Default();
            s = LabSealBenchMath.With(s, LabSealVarId.Sigil, 1f, Recipes, Sigils, Signs);
            s = LabSealBenchMath.With(s, LabSealVarId.SignType, 0f, Recipes, Sigils, Signs);
            s = LabSealBenchMath.With(s, LabSealVarId.SignCount, 3f, Recipes, Sigils, Signs);
            s = LabSealBenchMath.With(s, LabSealVarId.PaperSize, 3f, Recipes, Sigils, Signs);
            s = LabSealBenchMath.With(s, LabSealVarId.Ink, 2f, Recipes, Sigils, Signs);

            Assert.AreEqual("water", s.sigilId);
            Assert.AreEqual("column", s.signId);
            Assert.AreEqual(1f, s.paperSize);
            Assert.AreEqual(LabInkKind.Resistant, s.ink);
            Assert.AreEqual(1f, LabSealBenchMath.Get(s, LabSealVarId.Sigil, Recipes, Sigils, Signs));
            Assert.AreEqual(3f, LabSealBenchMath.Get(s, LabSealVarId.SignCount, Recipes, Sigils, Signs));
            Assert.AreEqual(3f, LabSealBenchMath.Get(s, LabSealVarId.PaperSize, Recipes, Sigils, Signs));
        }

        [Test]
        public void With_RecortaAlRango()
        {
            LabSealSpec s = LabSealSpec.Default();
            s = LabSealBenchMath.With(s, LabSealVarId.SignCount, 9f, Recipes, Sigils, Signs);
            s = LabSealBenchMath.With(s, LabSealVarId.BaseAngle, 50f, Recipes, Sigils, Signs);
            s = LabSealBenchMath.With(s, LabSealVarId.Tremor, 1f, Recipes, Sigils, Signs);
            s = LabSealBenchMath.With(s, LabSealVarId.PaperFace, -0.3f, Recipes, Sigils, Signs);
            Assert.AreEqual(LabSealSpec.MaxSignCount, s.signCount);
            Assert.AreEqual(45f, s.baseAngleDeg);
            Assert.AreEqual(LabSealSpec.MaxTremor, s.tremor);
            Assert.AreEqual(-1, s.paperFace);
        }

        [Test]
        public void With_IndiceNegativo_SinSigilo()
        {
            LabSealSpec s = LabSealBenchMath.With(LabSealSpec.Default(), LabSealVarId.Sigil, 0f, Recipes, Sigils, Signs);
            Assert.AreEqual("fire", s.sigilId);
            s = LabSealBenchMath.With(s, LabSealVarId.Sigil, -1f, Recipes, Sigils, Signs);
            Assert.IsNull(s.sigilId);
            Assert.AreEqual(-1f, LabSealBenchMath.Get(s, LabSealVarId.Sigil, Recipes, Sigils, Signs));
        }

        [Test]
        public void FromRecipe_Llamarada_EsExacta()
        {
            LabSealSpec current = LabSealSpec.Default();
            current.paperSize = 1f;
            current.ringGapDeg = 20f;
            current.signLength = 1.4f;
            LabSealSpec s = LabSealBenchMath.FromRecipe(current, SpellDef.FromJson(BlazeJson), out bool exact);

            Assert.IsTrue(exact);
            Assert.AreEqual("blaze", s.recipeId);
            Assert.AreEqual("fire", s.sigilId);
            Assert.AreEqual("column", s.signId);
            Assert.AreEqual(2, s.signCount);
            Assert.AreEqual(0f, s.baseAngleDeg);
            Assert.AreEqual(0f, s.turnDeg);
            // El anillo se cierra para que se lance; la geometria vuelve al canon; el papel se conserva.
            Assert.AreEqual(0f, s.ringGapDeg);
            Assert.AreEqual(1f, s.signLength);
            Assert.AreEqual(1f, s.paperSize);
        }

        [Test]
        public void FromRecipe_Mezclada_SeAproxima()
        {
            LabSealSpec s = LabSealBenchMath.FromRecipe(LabSealSpec.Default(), SpellDef.FromJson(MixedJson), out bool exact);
            Assert.IsFalse(exact);
            Assert.AreEqual("column", s.signId);
            Assert.AreEqual(90f, s.baseAngleDeg);
            Assert.AreEqual(LabSealSpec.TurnOutDeg, s.turnDeg);
        }

        [Test]
        public void FromRecipe_Libre_SelloVacio()
        {
            LabSealSpec s = LabSealBenchMath.FromRecipe(LabSealSpec.Default(), null, out bool exact);
            Assert.IsTrue(exact);
            Assert.AreEqual(LabSealBenchMath.FreeRecipeId, s.recipeId);
            Assert.IsNull(s.sigilId);
            Assert.AreEqual(0, s.signCount);
        }

        [Test]
        public void FormatNumeric_EnEspanolConUnidad()
        {
            Assert.AreEqual("45°", LabSealBenchMath.FormatNumeric(LabSealVarId.BaseAngle, 45f));
            Assert.AreEqual("0,45 m", LabSealBenchMath.FormatNumeric(LabSealVarId.PaperSize, 1f));
            Assert.AreEqual("×1,2", LabSealBenchMath.FormatNumeric(LabSealVarId.SignLength, 1.2f));
            Assert.AreEqual("hacia afuera", LabSealBenchMath.FormatNumeric(LabSealVarId.TurnDeg, 180f));
            Assert.AreEqual("Poderosa", LabSealBenchMath.FormatNumeric(LabSealVarId.Ink, 1f));
            StringAssert.Contains("preparado", LabSealBenchMath.FormatNumeric(LabSealVarId.RingGap, 34f));
            Assert.IsNull(LabSealBenchMath.FormatNumeric(LabSealVarId.Sigil, 0f));
        }

        [Test]
        public void Serialize_SinSeparadorDelCsvYConSemilla()
        {
            LabSealSpec s = LabSealSpec.Default();
            s.sigilId = "fire";
            s.signId = "column";
            s.signCount = 2;
            s.seed = 42;
            string text = LabSealBenchMath.Serialize(s);
            Assert.AreEqual(-1, text.IndexOf(LabCsv.Separator));
            StringAssert.StartsWith("sigilo=fire signo=column×2", text);
            StringAssert.Contains("dist=0,714", text);
            StringAssert.EndsWith("semilla=42", text);
            Assert.AreEqual(text, LabSealBenchMath.Serialize(s), "determinista");
        }

        [Test]
        public void EmissionDirection_InclinacionLevantaElDisparo()
        {
            Vector3 flat = LabSealBenchMath.EmissionDirection(Vector3.zero, new Vector3(0f, 3f, 10f), 0f, Vector3.forward, out Vector3 up0);
            Assert.That(Vector3.Angle(flat, Vector3.forward), Is.LessThan(0.01f), "sin inclinacion: horizontal hacia el blanco");
            Assert.That(Vector3.Dot(flat, up0), Is.EqualTo(0f).Within(1e-4f));

            Vector3 tilted = LabSealBenchMath.EmissionDirection(Vector3.zero, new Vector3(0f, 0f, 10f), 30f, Vector3.forward, out Vector3 up30);
            Assert.That(Vector3.Angle(tilted, Vector3.forward), Is.EqualTo(30f).Within(0.01f));
            Assert.That(tilted.y, Is.GreaterThan(0f));
            Assert.That(Vector3.Dot(tilted, up30), Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void FaceRotation_LlevaLaNormalLocalALaPedida()
        {
            Vector3 target = new Vector3(1f, 0.5f, -2f).normalized;
            Quaternion q = LabSealBenchMath.FaceRotation(Vector3.back, target, Vector3.up);
            Assert.That(Vector3.Angle(q * Vector3.back, target), Is.LessThan(0.01f));
        }

        [Test]
        public void SquareToCanvasUv_CentroFijoYCirculoEnLienzoAncho()
        {
            Vector2 iso = new Vector2(1f, 0.5f);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), LabSealBenchMath.SquareToCanvasUv(new Vector2(0.5f, 0.5f), iso));
            // En un lienzo el doble de ancho, medio cuadrado en x ocupa un cuarto del UV.
            Vector2 right = LabSealBenchMath.SquareToCanvasUv(new Vector2(1f, 0.5f), iso);
            Assert.That(right.x, Is.EqualTo(0.75f).Within(1e-5f));
        }
    }
}
