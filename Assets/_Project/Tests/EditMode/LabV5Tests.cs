using System.Collections.Generic;
using NUnit.Framework;
using SpellSimulator.Core;
using SpellSimulator.Lab;
using UnityEngine;

namespace SpellSimulator.Tests
{
    /// <summary>
    /// Logica pura del Laboratorio v6: dos alas grandes (alcance, alturas, giro hacia adentro, filas y grupos sin pisarse), atril
    /// (lienzo arriba, botones, pantalla abajo) y vista previa del atril.
    /// </summary>
    public class LabV5Tests
    {
        private static readonly int[] Sides = { LabBenchLayout.Left, LabBenchLayout.Right };
        private const float TitleHalf = 0.011f, ControlCollider = 0.12f;

        /// <summary>Todos los puntos extremos que se tocan en un ala: filas (columnas de las puntas), grillas, casillas y botones.</summary>
        private static IEnumerable<Vector2> SideTargets(int panel)
        {
            float reach = 2f * LabBenchLayout.ColumnPitch;
            foreach (float s in LabBenchLayout.RowS(panel))
                foreach (float sx in new[] { -1f, 1f })
                    foreach (float ds in new[] { -LabBenchLayout.ControlHalfHeight, LabBenchLayout.ControlHalfHeight })
                        yield return new Vector2(sx * reach, s + ds);
            float cell = LabBenchLayout.CellPitch, gridHalf = (LabBenchLayout.GridColumns - 1) * 0.5f * cell;
            if (panel == LabBenchLayout.Left)
            {
                foreach (float sx in new[] { -1f, 1f })
                {
                    yield return new Vector2(LabBenchLayout.SigilGridX + sx * gridHalf, LabBenchLayout.SigilGridS + cell * 0.5f);
                    yield return new Vector2(LabBenchLayout.SigilGridX + sx * gridHalf, LabBenchLayout.SigilGridS - cell * 0.5f);
                    yield return new Vector2(LabBenchLayout.SignGridX + sx * gridHalf, LabBenchLayout.SignGridS + cell);
                    yield return new Vector2(LabBenchLayout.SignGridX + sx * gridHalf, LabBenchLayout.SignGridS - cell);
                }
            }
            else
            {
                foreach (float sx in new[] { -1f, 1f })
                    yield return new Vector2(LabBenchLayout.SlotX + sx * gridHalf, LabBenchLayout.SlotS);
                foreach (float rowS in LabBenchLayout.ButtonRowS)
                    for (int i = 0; i < LabBenchLayout.ButtonColumns; i++) yield return new Vector2(LabBenchLayout.ButtonX(i), rowS);
            }
        }

        [Test]
        public void Alas_TodoControlAlAlcance_YHaciaAdelante()
        {
            foreach (int panel in Sides)
                foreach (Vector2 t in SideTargets(panel))
                {
                    Vector3 p = LabBenchLayout.PanelPoint(panel, t.x, t.y);
                    Assert.LessOrEqual(LabBenchLayout.Reach(p), LabBenchLayout.SideReachMax + 1e-4f, $"panel {panel}, x {t.x}, s {t.y}");
                    Assert.LessOrEqual(Mathf.Abs(LabBenchLayout.Bearing(p)), 90f, "como mucho al costado: nunca hay que girar hacia atras");
                    Assert.That(p.y, Is.InRange(LabBenchLayout.SideBottom, LabBenchLayout.TopMax), $"altura panel {panel}, s {t.y}");
                }
        }

        [Test]
        public void Atril_TodoAlAlcance_LienzoALaAlturaDeDibujo()
        {
            float half = LabBenchLayout.PadSize * 0.5f;
            var targets = new[]
            {
                new Vector2(0f, LabBenchLayout.ButtonsS), new Vector2(-LabBenchLayout.FrontButtonX - LabBenchLayout.FrontButtonWidth * 0.5f, LabBenchLayout.ButtonsS),
                new Vector2(LabBenchLayout.FrontButtonX + LabBenchLayout.FrontButtonWidth * 0.5f, LabBenchLayout.ButtonsS),
                new Vector2(-half, LabBenchLayout.PadS + half), new Vector2(half, LabBenchLayout.PadS + half),
                new Vector2(-half, LabBenchLayout.PadS - half), new Vector2(half, LabBenchLayout.PadS - half)
            };
            foreach (Vector2 t in targets)
            {
                Vector3 p = LabBenchLayout.PanelPoint(LabBenchLayout.Center, t.x, t.y);
                Assert.LessOrEqual(LabBenchLayout.Reach(p), LabBenchLayout.ReachMax + 1e-4f, $"centro {t}");
                Assert.That(p.y, Is.InRange(LabBenchLayout.CenterBottom, LabBenchLayout.TopMax));
            }
            // Se dibuja parado: el centro del lienzo entre 1,3 y 1,5 m.
            float padHeight = LabBenchLayout.PanelPoint(LabBenchLayout.Center, 0f, LabBenchLayout.PadS).y;
            Assert.That(padHeight, Is.InRange(1.3f, 1.5f), "lienzo a la altura de dibujo");
        }

        [Test]
        public void Atril_OrdenDeArribaHaciaAbajo_LienzoBotonesPantalla()
        {
            float pad = LabBenchLayout.PadSize * 0.5f + 0.014f, buttons = LabBenchLayout.CenterButtonDepth * 0.5f;
            Assert.Less(LabBenchLayout.ScreenTo, LabBenchLayout.ButtonsS - buttons, "la pantalla queda debajo de los botones");
            Assert.Less(LabBenchLayout.ButtonsS + buttons, LabBenchLayout.PadS - pad, "los botones quedan debajo del lienzo");
            Assert.LessOrEqual(LabBenchLayout.PadS + pad, LabBenchLayout.CenterLength, "el lienzo entra en la losa");
            Assert.GreaterOrEqual(LabBenchLayout.ScreenFrom, 0f);
            // Los botones entran a lo ancho del atril.
            Assert.LessOrEqual(LabBenchLayout.FrontButtonX + LabBenchLayout.FrontButtonWidth * 0.5f, LabBenchLayout.CenterWidth * 0.5f);
        }

        [Test]
        public void Alas_GrandesCasiVerticales_BordesYPendiente()
        {
            Assert.AreEqual(65f, LabBenchLayout.SideTilt, 5f, "alas inclinadas ~65°");
            Assert.AreEqual(0.95f, LabBenchLayout.SideBottom, 0.05f, "borde de abajo ~0,95 m");
            Assert.AreEqual(0.96f, LabBenchLayout.SideWidth, 0.1f, "ala de ~0,9-1 m de ancho");
            foreach (int panel in new[] { LabBenchLayout.Left, LabBenchLayout.Center, LabBenchLayout.Right })
                Assert.LessOrEqual(LabBenchLayout.TopHeight(panel, LabBenchLayout.PanelLength(panel)), LabBenchLayout.TopMax, $"borde de arriba del panel {panel}");
            Assert.AreEqual(1.85f, LabBenchLayout.TopHeight(LabBenchLayout.Right, LabBenchLayout.SideLength), 0.05f, "borde de arriba ~1,85 m");
            // La pantalla (con su marco) tampoco pasa el tope.
            Assert.LessOrEqual(LabBenchLayout.TopHeight(LabBenchLayout.Center, LabBenchLayout.ScreenTo) + 0.02f, LabBenchLayout.TopMax);
            // Distancia al punto de pie del centro del borde de abajo: ~0,9-1 m.
            foreach (int panel in Sides)
                Assert.That(LabBenchLayout.Reach(LabBenchLayout.PanelBase(panel)), Is.InRange(0.85f, 1.05f));
        }

        [Test]
        public void Alas_GiradasHaciaAdentro_YSinPisarElAtril()
        {
            Assert.AreEqual(0f, LabBenchLayout.PanelYaw(LabBenchLayout.Center));
            Assert.AreEqual(-LabBenchLayout.PanelYaw(LabBenchLayout.Right), LabBenchLayout.PanelYaw(LabBenchLayout.Left), 1e-4f);
            Assert.That(LabBenchLayout.PanelYaw(LabBenchLayout.Right), Is.InRange(25f, 35f), "giradas 25-35° hacia adentro");
            float edge = LabBenchLayout.SideWidth * 0.5f;
            foreach (float s in new[] { 0f, LabBenchLayout.SideLength })
            {
                Assert.Greater(LabBenchLayout.PanelPoint(LabBenchLayout.Right, -edge, s).x, LabBenchLayout.ScreenWidth * 0.5f + 0.02f, "el ala derecha no pisa el atril ni la pantalla");
                Assert.Less(LabBenchLayout.PanelPoint(LabBenchLayout.Left, edge, s).x, -LabBenchLayout.ScreenWidth * 0.5f - 0.02f, "el ala izquierda no pisa el atril ni la pantalla");
            }
            Assert.Less(Mathf.Abs(LabBenchLayout.Bearing(LabBenchLayout.PanelPoint(LabBenchLayout.Right, edge, 0f))), 100f, "el borde de afuera queda al costado, no atras");
        }

        [Test]
        public void Alas_FilasYGruposSinPisarse()
        {
            float half = LabBenchLayout.SideWidth * 0.5f, cell = LabBenchLayout.CellPitch, gridHalf = LabBenchLayout.GridColumns * cell * 0.5f;
            Assert.GreaterOrEqual(LabBenchLayout.ColumnPitch, LabPanelLayout.MinControlPitch - 1e-5f);
            Assert.GreaterOrEqual(LabBenchLayout.ColumnPitch - ControlCollider, 0.05f, "colliders de 12 cm con ≥ 5 cm de aire");
            Assert.Greater(LabBenchLayout.Columns, 2);
            // 5 columnas con su plaquita entran en el ala (con marco).
            Assert.LessOrEqual((LabBenchLayout.Columns - 1) * 0.5f * LabBenchLayout.ColumnPitch + LabPanelLayout.PlaqueWidth * 0.5f + 0.015f, half - 0.005f);
            foreach (int panel in Sides)
            {
                float[] rows = LabBenchLayout.RowS(panel);
                for (int i = 1; i < rows.Length; i++)
                    Assert.GreaterOrEqual(rows[i - 1] - rows[i], LabBenchLayout.RowPitch - 1e-5f, $"filas del panel {panel}");
                Assert.GreaterOrEqual(rows[rows.Length - 1] - LabBenchLayout.ControlHalfHeight, 0f, "la ultima fila no se sale por abajo");
                // El titulo de la fila de arriba cabe antes del copete.
                Assert.LessOrEqual(rows[0] + LabBenchLayout.RowTitleDS + TitleHalf, LabBenchLayout.CrestFrom);
            }
            Assert.LessOrEqual(LabBenchLayout.GridTitleS + TitleHalf, LabBenchLayout.CrestFrom, "titulos de las grillas antes del copete");
            Assert.LessOrEqual(LabBenchLayout.SideLength - 0.07f, LabBenchLayout.CrestFrom + 1e-4f, "copete de 7 cm");

            // Izquierda: grillas lado a lado (sin pisarse, dentro del ala) y arriba de la primera fila.
            Assert.GreaterOrEqual(LabBenchLayout.CellPitch, 0.085f - 1e-5f, "blancos de VR ≥ 8,5 cm");
            Assert.LessOrEqual(LabBenchLayout.SigilGridX + gridHalf, LabBenchLayout.SignGridX - gridHalf, "las grillas no se pisan");
            Assert.LessOrEqual(LabBenchLayout.SignGridX + gridHalf, half);
            Assert.GreaterOrEqual(LabBenchLayout.SigilGridX - gridHalf, -half);
            Assert.GreaterOrEqual(LabBenchLayout.SignGridS - 1.5f * cell, LabBenchLayout.LeftRowS[0] + LabBenchLayout.RowTitleDS + TitleHalf + 0.005f, "las grillas no pisan el titulo de la primera fila");
            Assert.LessOrEqual(LabBenchLayout.SignGridS + 1.5f * cell, LabBenchLayout.GridTitleS - TitleHalf + 1e-4f, "las grillas quedan bajo su titulo");

            // Derecha: casillas a la izquierda, botones a la derecha (dos filas de tres), todo dentro del ala y arriba de la primera fila.
            Assert.LessOrEqual(LabBenchLayout.SlotX + gridHalf, LabBenchLayout.ButtonX(0) - LabBenchLayout.ButtonWidth * 0.5f, "casillas y botones no se pisan");
            Assert.LessOrEqual(LabBenchLayout.ButtonX(LabBenchLayout.ButtonColumns - 1) + LabBenchLayout.ButtonWidth * 0.5f, half);
            Assert.GreaterOrEqual(LabBenchLayout.ButtonPitch, LabBenchLayout.ButtonWidth, "los botones no se pisan");
            Assert.GreaterOrEqual(LabBenchLayout.ButtonRowS[0] - LabBenchLayout.ButtonRowS[1], LabBenchLayout.ButtonDepth, "las filas de botones no se pisan");
            Assert.GreaterOrEqual(LabBenchLayout.ButtonRowS[1] - LabBenchLayout.ButtonDepth * 0.5f,
                LabBenchLayout.RightRowS[0] + LabBenchLayout.RowTitleDS + TitleHalf, "los botones no pisan el titulo de la primera fila");
            Assert.LessOrEqual(LabBenchLayout.ButtonRowS[0] + LabBenchLayout.ButtonDepth * 0.5f, LabBenchLayout.GridTitleS - TitleHalf + 1e-4f);
        }

        [Test]
        public void Pantalla_LegibleDesdeElPuntoDePie()
        {
            Vector3 middle = LabBenchLayout.PanelPoint(LabBenchLayout.Center, 0f, (LabBenchLayout.ScreenFrom + LabBenchLayout.ScreenTo) * 0.5f);
            Assert.LessOrEqual(LabBenchLayout.EyeDistance(middle), LabBenchLayout.ScreenReadMax, "a ≤ 0,9 m del ojo");
            Assert.GreaterOrEqual(LabPanelLayout.TextHeight(0.22f), LabBenchLayout.ScreenTextMin - 1e-5f, "letra ≥ 2,2 cm");
            // Hechizo grande y 4 datos, todos con rotulo; el papel no esta (se ve en la pista).
            Assert.AreEqual(4, LabSealSummary.ScreenRows.Length);
            CollectionAssert.DoesNotContain(LabSealSummary.ScreenRows, LabSealSummary.RowRecipe);
            CollectionAssert.DoesNotContain(LabSealSummary.ScreenRows, LabSealSummary.RowPaper);
            foreach (int row in LabSealSummary.ScreenRows) Assert.IsFalse(string.IsNullOrEmpty(LabSealSummary.RowLabel(row)));
        }

        // ---------------------------------------------------------------- vista previa del atril

        private static Stroke Preview() => new Stroke(new List<Vector2> { Vector2.zero, Vector2.one }) { OwnerId = LabSketchPreview.PreviewOwner };

        [Test]
        public void VistaPrevia_SoloTrazosDelBanco_NoEsDibujoAMano()
        {
            var strokes = new List<Stroke> { Preview(), Preview() };
            Assert.IsFalse(LabSketchPreview.HasHandStroke(strokes));
            Assert.IsFalse(LabSketchPreview.HasHandStroke(new List<Stroke>()));
            Assert.IsFalse(LabSketchPreview.HasHandStroke(null));
        }

        [Test]
        public void VistaPrevia_ElPrimerTrazoDelJugadorLaLimpia()
        {
            var hand = new Stroke(new List<Vector2> { new Vector2(0.2f, 0.2f) }); // dueño 0 (jugador local) o el del jugador en red
            var strokes = new List<Stroke> { Preview(), Preview(), Preview(), hand };
            Assert.IsTrue(LabSketchPreview.HasHandStroke(strokes));
            Assert.AreEqual(3, LabSketchPreview.RemovePreview(strokes));
            Assert.AreEqual(1, strokes.Count);
            Assert.AreSame(hand, strokes[0], "el trazo en curso sigue (PenTool guarda su referencia)");

            var remote = new Stroke { OwnerId = 5 };
            Assert.IsTrue(LabSketchPreview.HasHandStroke(new List<Stroke> { Preview(), remote }));
            Assert.IsFalse(LabSketchPreview.IsPreview(remote));
        }
    }
}
