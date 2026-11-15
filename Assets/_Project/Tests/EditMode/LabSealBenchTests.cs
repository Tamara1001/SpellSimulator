using System.Collections.Generic;
using System.Text;
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

        // ---------------------------------------------------------------- posicion del papel (Laboratorio v3)

        private static readonly Vector3[] ExpectedEmission =
        {
            Vector3.forward, Vector3.back, Vector3.up, Vector3.up, Vector3.left, Vector3.right, Vector3.back, Vector3.down, Vector3.forward
        };

        [Test]
        public void PaperPose_EmisionYArribaPerpendicularesYUnitarios()
        {
            for (int preset = 0; preset < LabSealSpec.PaperPoseCount; preset++)
                for (int face = -1; face <= 1; face += 2)
                {
                    LabPaperPose p = LabSealBenchMath.PaperPose(preset, face, 30f, 0f);
                    Assert.That(p.Emission.magnitude, Is.EqualTo(1f).Within(1e-4f), "emision unitaria, preset " + preset);
                    Assert.That(p.Up.magnitude, Is.EqualTo(1f).Within(1e-4f), "up unitario, preset " + preset);
                    Assert.That(Vector3.Dot(p.Emission, p.Up), Is.EqualTo(0f).Within(1e-4f), "perpendicular, preset " + preset + " cara " + face);
                    Assert.That(Mathf.Abs(Vector3.Dot(p.ViewNormal, p.Emission)), Is.EqualTo(1f).Within(1e-4f), "cara visible paralela a la emision, preset " + preset);
                    Assert.That(Vector3.Dot(p.ViewNormal, p.Up), Is.EqualTo(0f).Within(1e-4f), "up en el plano del papel, preset " + preset);
                }
        }

        [Test]
        public void PaperPose_SentidoPorPreset()
        {
            for (int preset = 0; preset < 8; preset++)
            {
                LabPaperPose p = LabSealBenchMath.PaperPose(preset, 1, 0f, 0f);
                Assert.That(Vector3.Distance(p.Emission, ExpectedEmission[preset]), Is.LessThan(1e-4f), "preset " + preset);
            }
            LabPaperPose free = LabSealBenchMath.PaperPose(LabSealSpec.FreePaperPose, 1, 0f, 0f);
            Assert.That(Vector3.Distance(free.Emission, Vector3.forward), Is.LessThan(1e-4f));
        }

        [Test]
        public void PaperPose_CaraMenosUnoInvierteLaEmision_YSeIgnoraLaInclinacionEn0a7()
        {
            for (int preset = 0; preset < 8; preset++)
            {
                LabPaperPose up = LabSealBenchMath.PaperPose(preset, 1, 0f, 1f);
                LabPaperPose down = LabSealBenchMath.PaperPose(preset, -1, 0f, 1f);
                Assert.That(Vector3.Distance(down.Emission, -up.Emission), Is.LessThan(1e-4f), "invierte, preset " + preset);
                Assert.That(Vector3.Distance(down.ViewNormal, up.ViewNormal), Is.LessThan(1e-4f), "la cara visible no cambia");
                Assert.AreEqual(-up.InkSign, down.InkSign, "la tinta pasa al otro lado");
                LabPaperPose tilted = LabSealBenchMath.PaperPose(preset, 1, 60f, 1f);
                Assert.That(Vector3.Distance(tilted.Emission, up.Emission), Is.LessThan(1e-4f), "inclinacion ignorada, preset " + preset);
                Assert.That(Vector3.Distance(tilted.Position, up.Position), Is.LessThan(1e-4f));
            }
        }

        [Test]
        public void PaperPose_PosicionesDeLaPista()
        {
            const float start = 4f;
            foreach (int atEasel in new[] { 0, 1, 2, 8 })
                Assert.IsTrue(LabSealBenchMath.PaperPose(atEasel, 1, 0f, start).AtEasel, "atril, preset " + atEasel);
            Assert.That(LabSealBenchMath.PaperPose(3, 1, 0f, start).Position, Is.EqualTo(new Vector3(0f, LabSealBenchMath.FloorHeight, start + 2f)));
            Assert.That(LabSealBenchMath.PaperPose(4, 1, 0f, start).Position, Is.EqualTo(new Vector3(0f, LabSealBenchMath.StandHeight, start + 3f)));
            Assert.That(LabSealBenchMath.PaperPose(5, 1, 0f, start).Position, Is.EqualTo(new Vector3(0f, LabSealBenchMath.StandHeight, start + 3f)));
            Assert.That(LabSealBenchMath.PaperPose(6, 1, 0f, start).Position, Is.EqualTo(new Vector3(0f, LabSealBenchMath.StandHeight, start + 6f)));
            Assert.That(LabSealBenchMath.PaperPose(7, 1, 0f, start).Position.y, Is.EqualTo(1.5f));
            for (int preset = 3; preset < 8; preset++) Assert.IsFalse(LabSealBenchMath.PaperPose(preset, 1, 0f, start).AtEasel);
        }

        [Test]
        public void PaperPose_LibreIgualQueAntes()
        {
            LabPaperPose p = LabSealBenchMath.PaperPose(LabSealSpec.FreePaperPose, 1, 30f, 0f);
            Vector3 expected = LabSealBenchMath.EmissionDirection(Vector3.zero, Vector3.forward, 30f, Vector3.forward, out Vector3 expectedUp);
            Assert.That(Vector3.Distance(p.Emission, expected), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(p.Up, expectedUp), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(p.ViewNormal, -expected), Is.LessThan(1e-4f), "la cara mira al jugador, como antes");
            Assert.AreEqual(-1, p.InkSign, "cara +1 -> SetInkSign(-1), como antes");
            LabPaperPose flipped = LabSealBenchMath.PaperPose(LabSealSpec.FreePaperPose, -1, 30f, 0f);
            Assert.AreEqual(1, flipped.InkSign, "cara -1 -> SetInkSign(+1), como antes");
            Assert.That(Vector3.Distance(flipped.Up, expectedUp), Is.LessThan(1e-4f), "la cara no cambia la rotacion");
        }

        [Test]
        public void PaperPose_AtrilPistaIgualQueElV2()
        {
            LabPaperPose p = LabSealBenchMath.PaperPose(0, 1, 0f, 0f);
            Assert.AreEqual(-1, p.InkSign);
            Assert.That(Vector3.Distance(p.ViewNormal, Vector3.back), Is.LessThan(1e-4f));
            Assert.AreEqual(1, LabSealBenchMath.PaperPose(1, 1, 0f, 0f).InkSign, "emite hacia el jugador por la cara visible");
        }

        [Test]
        public void PaperPose_GetYWith_ConRecorte()
        {
            LabSealSpec s = LabSealBenchMath.With(LabSealSpec.Default(), LabSealVarId.PaperPose, 6f, Recipes, Sigils, Signs);
            Assert.AreEqual(6, s.paperPose);
            Assert.AreEqual(6f, LabSealBenchMath.Get(s, LabSealVarId.PaperPose, Recipes, Sigils, Signs));
            s = LabSealBenchMath.With(s, LabSealVarId.PaperPose, 99f, Recipes, Sigils, Signs);
            Assert.AreEqual(LabSealSpec.PaperPoseCount - 1, s.paperPose);
            Assert.AreEqual(0, LabSealSpec.Default().paperPose);
            Assert.AreEqual("Piso", LabSealBenchMath.FormatNumeric(LabSealVarId.PaperPose, 3f));
            StringAssert.Contains("pose=8", LabSealBenchMath.Serialize(s));
        }

        // ---------------------------------------------------------------- resumen del sello

        [Test]
        public void Summary_SelloPorDefecto()
        {
            var sb = new StringBuilder();
            LabSealSummary.Build(LabSealSpec.Default(), sb);
            string text = sb.ToString();
            StringAssert.Contains("Receta: gust", text);
            StringAssert.Contains("Elemento: sin sigilo", text);
            StringAssert.Contains("Signos: ninguno", text);
            StringAssert.Contains("Tinta: Normal", text);
            StringAssert.Contains("Papel: 0,45 m, Atril → pista", text);
            StringAssert.EndsWith("Estado previsto: cerrado", text);
        }

        [Test]
        public void Summary_SignosElementoYEstado()
        {
            LabSealSpec s = LabSealSpec.Default();
            s.sigilId = "fire";
            s.signId = "column";
            s.signCount = 2;
            s.turnDeg = LabSealSpec.TurnOutDeg;
            s.ink = LabInkKind.Powerful;
            s.ringGapDeg = 34f;
            s.paperPose = 3;
            s.paperFace = -1;
            var sb = new StringBuilder();
            LabSealSummary.Build(s, sb);
            string text = sb.ToString();
            StringAssert.Contains("Elemento: fuego", text);
            StringAssert.Contains("Signos: 2 × column (afuera)", text);
            StringAssert.Contains("Tinta: Poderosa", text);
            StringAssert.Contains("Piso (cara abajo)", text);
            StringAssert.EndsWith("Estado previsto: preparado", text);

            s.ringGapDeg = 33.9f;
            s.turnDeg = 0f;
            LabSealSummary.Build(s, sb);
            StringAssert.Contains("(adentro)", sb.ToString());
            StringAssert.EndsWith("Estado previsto: cerrado", sb.ToString());
        }

        [Test]
        public void Summary_ReutilizaElStringBuilder_YUsaNombres()
        {
            var sb = new StringBuilder("basura anterior");
            LabSealSpec s = LabSealSpec.Default();
            s.signId = "column";
            s.signCount = 1;
            LabSealSummary.Build(s, sb, id => id == "gust" ? "Ráfaga" : id == "column" ? "Columna" : null);
            string first = sb.ToString();
            StringAssert.DoesNotContain("basura", first);
            StringAssert.Contains("Receta: Ráfaga", first);
            StringAssert.Contains("1 × Columna", first);
            LabSealSummary.Build(s, sb, id => id == "gust" ? "Ráfaga" : id == "column" ? "Columna" : null);
            Assert.AreEqual(first, sb.ToString(), "no acumula entre llamadas");
        }

        // ---------------------------------------------------------------- catalogo y reset por grupo

        [Test]
        public void Catalogo_AyudaNoVaciaYCorta()
        {
            Assert.AreEqual(19, LabSealCatalog.Count);
            for (int i = 0; i < LabSealCatalog.Count; i++)
            {
                LabSealVarDef def = LabSealCatalog.Defs[i];
                Assert.AreEqual((LabSealVarId)i, def.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(def.Help), "ayuda de " + def.Id);
                Assert.LessOrEqual(def.Help.Length, LabSealCatalog.MaxHelpLength, "ayuda de " + def.Id + ": " + def.Help);
            }
            LabSealVarDef pose = LabSealCatalog.Get(LabSealVarId.PaperPose);
            Assert.AreEqual(LabSealVarKind.List, pose.Kind);
            Assert.AreEqual(0f, pose.Default);
            Assert.AreEqual(8f, pose.Max);
        }

        [Test]
        public void Catalogo_GruposCubrenTodasLasVariablesUnaVez()
        {
            var seen = new HashSet<LabSealVarId>();
            for (int group = 1; group <= LabSealCatalog.GroupCount; group++)
            {
                IReadOnlyList<LabSealVarId> ids = LabSealCatalog.IdsOfGroup(group);
                Assert.Greater(ids.Count, 0, "grupo " + group);
                Assert.IsFalse(string.IsNullOrEmpty(LabSealCatalog.GroupName(group)));
                for (int i = 0; i < ids.Count; i++)
                {
                    Assert.IsTrue(seen.Add(ids[i]), "repetida: " + ids[i]);
                    Assert.AreEqual(group, LabSealCatalog.GroupOf(ids[i]));
                }
            }
            Assert.AreEqual(LabSealCatalog.Count, seen.Count);
            Assert.AreEqual(0, LabSealCatalog.IdsOfGroup(0).Count);
            Assert.AreEqual(0, LabSealCatalog.IdsOfGroup(5).Count);
            Assert.AreEqual(3, LabSealCatalog.GroupOf(LabSealVarId.PaperPose));
        }

        [Test]
        public void ResetVars_SoloRestauraLasVariablesDelGrupo()
        {
            LabSealSpec baseline = LabSealSpec.Default();
            LabSealSpec s = baseline;
            s = LabSealBenchMath.With(s, LabSealVarId.PaperSize, 3f, Recipes, Sigils, Signs);
            s = LabSealBenchMath.With(s, LabSealVarId.PaperPose, 5f, Recipes, Sigils, Signs);
            s = LabSealBenchMath.With(s, LabSealVarId.PaperFace, -1f, Recipes, Sigils, Signs);
            s = LabSealBenchMath.With(s, LabSealVarId.PaperTilt, 45f, Recipes, Sigils, Signs);
            s = LabSealBenchMath.With(s, LabSealVarId.Ink, 2f, Recipes, Sigils, Signs);
            s = LabSealBenchMath.With(s, LabSealVarId.SignCount, 3f, Recipes, Sigils, Signs);

            LabSealSpec r = LabSealBenchMath.ResetVars(s, baseline, LabSealCatalog.IdsOfGroup(3), Recipes, Sigils, Signs);
            Assert.AreEqual(baseline.paperSize, r.paperSize);
            Assert.AreEqual(0, r.paperPose);
            Assert.AreEqual(1, r.paperFace);
            Assert.AreEqual(0f, r.paperTiltDeg);
            Assert.AreEqual(LabInkKind.Resistant, r.ink, "otro grupo: no cambia");
            Assert.AreEqual(3, r.signCount, "otro grupo: no cambia");
        }

        [Test]
        public void ResetVars_ListaVaciaONull_NoCambiaNada()
        {
            LabSealSpec s = LabSealBenchMath.With(LabSealSpec.Default(), LabSealVarId.PaperPose, 4f, Recipes, Sigils, Signs);
            Assert.AreEqual(4, LabSealBenchMath.ResetVars(s, LabSealSpec.Default(), null, Recipes, Sigils, Signs).paperPose);
            Assert.AreEqual(4, LabSealBenchMath.ResetVars(s, LabSealSpec.Default(), LabSealCatalog.IdsOfGroup(0), Recipes, Sigils, Signs).paperPose);
        }
    }
}
