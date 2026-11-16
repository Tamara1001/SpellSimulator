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

        // ---------------------------------------------------------------- papel del campo (Laboratorio v4)

        private static readonly Vector3 Viewer = LabSealBenchMath.DefaultViewer;

        /// <summary>Direccion de cada atajo con cara normal (0 Frente … 7 Diagonal).</summary>
        private static readonly Vector3[] ExpectedEmission =
        {
            Vector3.forward, Vector3.back, Vector3.up, Vector3.up, Vector3.left, Vector3.right, Vector3.down,
            new Vector3(0f, 0.70710677f, 0.70710677f)
        };

        private static LabPaperPose PoseOf(LabSealSpec s)
        {
            float h = LabSealBenchMath.PaperCenterHeight(s.paperHeight, s.paperTiltDeg, s.paperSize);
            return LabSealBenchMath.PaperPose(s, new Vector3(s.paperX, h, 5f + s.paperDistance), Viewer);
        }

        [Test]
        public void PaperPose_EmisionYArribaPerpendicularesYUnitarios()
        {
            for (int yaw = -180; yaw <= 180; yaw += 45)
                for (int tilt = -90; tilt <= 90; tilt += 30)
                    for (int face = -1; face <= 1; face += 2)
                    {
                        LabPaperPose p = LabSealBenchMath.PaperPose(new Vector3(1f, 0.5f, 8f), yaw, tilt, face, Viewer);
                        string at = $"giro {yaw}, incl. {tilt}, cara {face}";
                        Assert.That(p.Emission.magnitude, Is.EqualTo(1f).Within(1e-4f), "emision unitaria, " + at);
                        Assert.That(p.Up.magnitude, Is.EqualTo(1f).Within(1e-4f), "up unitario, " + at);
                        Assert.That(Vector3.Dot(p.Emission, p.Up), Is.EqualTo(0f).Within(1e-4f), "perpendicular, " + at);
                        Assert.That(Mathf.Abs(Vector3.Dot(p.ViewNormal, p.Emission)), Is.EqualTo(1f).Within(1e-4f), "cara paralela a la emision, " + at);
                    }
        }

        [Test]
        public void Atajos_SentidoPorPreset()
        {
            for (int preset = 0; preset < LabSealSpec.PaperPoseCount - 1; preset++)
            {
                LabSealSpec s = LabSealBenchMath.ApplyPaperPreset(LabSealSpec.Default(), preset);
                Assert.AreEqual(preset, s.paperPose);
                Assert.That(Vector3.Distance(PoseOf(s).Emission, ExpectedEmission[preset]), Is.LessThan(1e-4f), "atajo " + preset);
            }
            Assert.AreEqual("Frente", LabSealBenchMath.PaperPoseName(0));
            Assert.AreEqual("Piso", LabSealBenchMath.PaperPoseName(3));
            Assert.AreEqual("Personalizada", LabSealBenchMath.PaperPoseName(LabSealSpec.FreePaperPose));
        }

        [Test]
        public void Atajo_Personalizada_NoMueveNada()
        {
            LabSealSpec s = LabSealBenchMath.With(LabSealSpec.Default(), LabSealVarId.PaperX, 2f, Recipes, Sigils, Signs);
            LabSealSpec p = LabSealBenchMath.With(s, LabSealVarId.PaperPose, LabSealSpec.FreePaperPose, Recipes, Sigils, Signs);
            Assert.AreEqual(2f, p.paperX);
            Assert.AreEqual(LabSealSpec.FreePaperPose, p.paperPose);
            Assert.IsFalse(LabSealBenchMath.TryGetPreset(LabSealSpec.FreePaperPose, out _));
        }

        [Test]
        public void MoverElPapel_PasaAPersonalizada_YUnAtajoLoVuelveATraer()
        {
            LabSealSpec s = LabSealSpec.Default();
            Assert.AreEqual(LabSealSpec.DefaultPaperPose, s.paperPose);
            foreach (LabSealVarId id in new[] { LabSealVarId.PaperX, LabSealVarId.PaperDistance, LabSealVarId.PaperYaw, LabSealVarId.PaperHeight, LabSealVarId.PaperTilt })
            {
                LabSealSpec moved = LabSealBenchMath.With(s, id, 1f, Recipes, Sigils, Signs);
                Assert.AreEqual(LabSealSpec.FreePaperPose, moved.paperPose, "mover " + id);
            }
            LabSealSpec back = LabSealBenchMath.With(LabSealBenchMath.With(s, LabSealVarId.PaperX, 3f, Recipes, Sigils, Signs),
                LabSealVarId.PaperPose, 3f, Recipes, Sigils, Signs);
            Assert.AreEqual(3, back.paperPose);
            Assert.AreEqual(0f, back.paperX, "el atajo Piso vuelve al centro");
            Assert.AreEqual(90f, back.paperTiltDeg);
        }

        [Test]
        public void CaraMenosUno_InvierteLaEmision_SinGirarElPapel()
        {
            for (int preset = 0; preset < LabSealSpec.PaperPoseCount - 1; preset++)
            {
                LabSealSpec s = LabSealBenchMath.ApplyPaperPreset(LabSealSpec.Default(), preset);
                LabPaperPose up = PoseOf(s);
                s.paperFace = -1;
                LabPaperPose down = PoseOf(s);
                Assert.That(Vector3.Distance(down.Emission, -up.Emission), Is.LessThan(1e-4f), "invierte, atajo " + preset);
                Assert.That(Vector3.Distance(down.ViewNormal, up.ViewNormal), Is.LessThan(1e-4f), "la cara visible no cambia");
                Assert.That(Vector3.Distance(down.Up, up.Up), Is.LessThan(1e-4f), "la rotacion no cambia");
                Assert.AreEqual(-up.InkSign, down.InkSign, "la tinta pasa al otro lado");
            }
        }

        [Test]
        public void Frente_IgualQueElAtrilDelV2_YHaciaMi_PorLaCaraVisible()
        {
            LabPaperPose front = PoseOf(LabSealBenchMath.ApplyPaperPreset(LabSealSpec.Default(), 0));
            Assert.AreEqual(-1, front.InkSign, "como el atril → pista del v2: la tinta del lado de atras");
            Assert.That(Vector3.Distance(front.ViewNormal, Vector3.back), Is.LessThan(1e-4f), "el dibujo mira al banco");
            Assert.AreEqual(1, PoseOf(LabSealBenchMath.ApplyPaperPreset(LabSealSpec.Default(), 1)).InkSign, "hacia mí: sale por la cara visible");
            LabPaperPose floor = PoseOf(LabSealBenchMath.ApplyPaperPreset(LabSealSpec.Default(), 3));
            Assert.That(Vector3.Distance(floor.Up, Vector3.forward), Is.LessThan(1e-4f), "en el piso, el arriba del papel apunta al fondo (se lee desde el banco)");
        }

        [Test]
        public void PaperCenterHeight_NoEntierraElPapel()
        {
            Assert.AreEqual(1.2f, LabSealBenchMath.PaperCenterHeight(1.2f, 0f, 0.45f), 1e-5f);
            Assert.AreEqual(LabSealBenchMath.FloorClearance + 0.225f, LabSealBenchMath.PaperCenterHeight(0f, 0f, 0.45f), 1e-5f, "de pie en el piso");
            Assert.AreEqual(LabSealBenchMath.FloorClearance, LabSealBenchMath.PaperCenterHeight(0f, 90f, 2f), 1e-5f, "acostado: casi en el suelo");
            Assert.AreEqual(LabSealBenchMath.FloorClearance, LabSealBenchMath.PaperCenterHeight(0f, -90f, 1f), 1e-5f, "boca abajo");
            Assert.AreEqual(1.02f, LabSealBenchMath.PaperCenterHeight(0.5f, 0f, 2f), 1e-5f, "papel de 2 m de pie");
        }

        [Test]
        public void PosicionLibre_GetWithYRecorteALaPista()
        {
            LabSealSpec s = LabSealBenchMath.With(LabSealSpec.Default(), LabSealVarId.PaperX, 99f, Recipes, Sigils, Signs);
            Assert.AreEqual(LabSealSpec.MaxPaperX, s.paperX);
            s = LabSealBenchMath.With(s, LabSealVarId.PaperDistance, -5f, Recipes, Sigils, Signs);
            Assert.AreEqual(LabSealSpec.MinPaperDistance, s.paperDistance);
            s = LabSealBenchMath.With(s, LabSealVarId.PaperHeight, 9f, Recipes, Sigils, Signs);
            Assert.AreEqual(LabSealSpec.MaxPaperHeight, s.paperHeight);
            s = LabSealBenchMath.With(s, LabSealVarId.PaperTilt, -120f, Recipes, Sigils, Signs);
            Assert.AreEqual(LabSealSpec.MinPaperTiltDeg, s.paperTiltDeg);
            s = LabSealBenchMath.With(s, LabSealVarId.PaperYaw, 135f, Recipes, Sigils, Signs);
            Assert.AreEqual(135f, LabSealBenchMath.Get(s, LabSealVarId.PaperYaw, Recipes, Sigils, Signs));
            Assert.AreEqual("135°", LabSealBenchMath.FormatNumeric(LabSealVarId.PaperYaw, 135f));
            Assert.AreEqual("1,50 m", LabSealBenchMath.FormatNumeric(LabSealVarId.PaperHeight, 1.5f));
            Assert.AreEqual("invertido", LabSealBenchMath.FormatNumeric(LabSealVarId.PaperFace, -1f));
            string text = LabSealBenchMath.Serialize(s);
            StringAssert.Contains("papelX=4,5", text);
            StringAssert.Contains("papelGiro=135", text);
            StringAssert.Contains("pose=8", text);
        }

        // ---------------------------------------------------------------- cartel «Vas a lanzar»

        [Test]
        public void Summary_SelloPorDefecto()
        {
            var sb = new StringBuilder();
            LabSealSummary.Build(LabSealSpec.Default(), sb);
            string text = sb.ToString();
            StringAssert.Contains("Hechizo: gust", text);
            StringAssert.Contains("Sigilo: ninguno", text);
            StringAssert.Contains("Signos: ninguno", text);
            StringAssert.Contains("Tinta: Normal", text);
            StringAssert.Contains("Papel: 0,45 m · Frente · a 3 m", text);
            StringAssert.EndsWith("Se lanza: el sello de los controles", text);
            Assert.AreEqual("cerrado", LabSealSummary.Prediction(LabSealSpec.Default()));
        }

        [Test]
        public void Summary_SignosTintaYDibujo()
        {
            LabSealSpec s = LabSealSpec.Default();
            s.sigilId = "fire";
            s.signId = "column";
            s.signCount = 2;
            s.turnDeg = LabSealSpec.TurnOutDeg;
            s.ink = LabInkKind.Powerful;
            s.ringGapDeg = 34f;
            s = LabSealBenchMath.ApplyPaperPreset(s, 3);
            s.paperFace = -1;
            var sb = new StringBuilder();
            LabSealSummary.Build(s, sb, null, true);
            string text = sb.ToString();
            StringAssert.Contains("Sigilo: fire", text);
            StringAssert.Contains("Signos: 2 × column, hacia afuera", text);
            StringAssert.Contains("Tinta: Poderosa, disparo invertido", text);
            StringAssert.Contains("Piso", text);
            StringAssert.EndsWith("Se lanza: tu dibujo del atril", text);
            Assert.AreEqual("preparado", LabSealSummary.Prediction(s), "el estado previsto va solo con lentes");
            StringAssert.DoesNotContain("previsto", text, "el cartel no muestra datos de reconocimiento");

            s.ringGapDeg = 33.9f;
            Assert.AreEqual("cerrado", LabSealSummary.Prediction(s));
        }

        [Test]
        public void Summary_FilasReutilizanElStringBuilder_YUsanNombres()
        {
            var sb = new StringBuilder("basura anterior");
            LabSealSpec s = LabSealSpec.Default();
            s.signId = "column";
            s.signCount = 1;
            System.Func<string, string> names = id => id == "gust" ? "Ráfaga" : id == "column" ? "Columna" : null;
            LabSealSummary.BuildRow(s, LabSealSummary.RowRecipe, sb, names, false);
            Assert.AreEqual("Ráfaga", sb.ToString());
            LabSealSummary.BuildRow(s, LabSealSummary.RowSigns, sb, names, false);
            Assert.AreEqual("1 × Columna, hacia adentro", sb.ToString(), "no acumula entre llamadas");
            for (int i = 0; i < LabSealSummary.RowCount; i++)
            {
                Assert.IsFalse(string.IsNullOrEmpty(LabSealSummary.RowLabel(i)));
                Assert.That(LabSealSummary.RowGroup(i), Is.InRange(0, LabSealCatalog.GroupCount));
            }
        }

        // ---------------------------------------------------------------- catalogo y reset por grupo

        [Test]
        public void Catalogo_AyudaNoVaciaYCorta()
        {
            Assert.AreEqual(23, LabSealCatalog.Count);
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
            Assert.AreEqual(-90f, LabSealCatalog.Get(LabSealVarId.PaperTilt).Min);
        }

        [Test]
        public void Catalogo_GruposCubrenTodasLasVariablesUnaVez_YLoImportanteVaPrimero()
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
            Assert.AreEqual(0, LabSealCatalog.IdsOfGroup(LabSealCatalog.GroupCount + 1).Count);
            Assert.AreEqual(LabSealVarId.BaseRecipe, LabSealCatalog.IdsOfGroup(1)[0], "1: receta y sigilo arriba");
            Assert.AreEqual(LabSealVarId.SignType, LabSealCatalog.IdsOfGroup(2)[0], "2: signos");
            Assert.AreEqual(LabSealVarId.Ink, LabSealCatalog.IdsOfGroup(3)[0], "3: papel y tinta");
            Assert.AreEqual(4, LabSealCatalog.GroupOf(LabSealVarId.Tremor), "4: afinado y prolijidad al final");
            Assert.AreEqual(5, LabSealCatalog.GroupOf(LabSealVarId.PaperPose), "5: papel del campo (panel de objetos)");
            IReadOnlyList<LabSealVarId> paper = LabSealCatalog.IdsOfGroup(5);
            Assert.AreEqual(LabSealVarId.PaperPose, paper[paper.Count - 1], "el atajo se restaura despues de la posicion");
        }

        [Test]
        public void ResetVars_SoloRestauraLasVariablesDelGrupo()
        {
            LabSealSpec baseline = LabSealSpec.Default();
            LabSealSpec s = baseline;
            s = LabSealBenchMath.With(s, LabSealVarId.PaperSize, 3f, Recipes, Sigils, Signs);
            s = LabSealBenchMath.With(s, LabSealVarId.PaperPose, 5f, Recipes, Sigils, Signs);
            s = LabSealBenchMath.With(s, LabSealVarId.PaperX, 2f, Recipes, Sigils, Signs);
            s = LabSealBenchMath.With(s, LabSealVarId.PaperTilt, 45f, Recipes, Sigils, Signs);
            s = LabSealBenchMath.With(s, LabSealVarId.Ink, 2f, Recipes, Sigils, Signs);
            s = LabSealBenchMath.With(s, LabSealVarId.SignCount, 3f, Recipes, Sigils, Signs);

            LabSealSpec r = LabSealBenchMath.ResetVars(s, baseline, LabSealCatalog.IdsOfGroup(5), Recipes, Sigils, Signs);
            Assert.AreEqual(baseline.paperSize, r.paperSize);
            Assert.AreEqual(LabSealSpec.DefaultPaperPose, r.paperPose);
            Assert.AreEqual(0f, r.paperX);
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
