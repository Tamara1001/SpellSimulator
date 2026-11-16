using System.Collections.Generic;
using NUnit.Framework;
using SpellSimulator.Core;
using SpellSimulator.Lab;
using UnityEngine;

namespace SpellSimulator.Tests
{
    /// <summary>Logica pura del Laboratorio v5: consola frontal (alcance, alturas, todo hacia adelante), pantalla y vista previa del atril.</summary>
    public class LabV5Tests
    {
        private static readonly int[] Sides = { LabBenchLayout.Left, LabBenchLayout.Right };

        /// <summary>Todos los puntos que se tocan en los laterales: filas × columnas, botones, grillas, casillas y pestañas.</summary>
        private static IEnumerable<Vector2> SideTargets()
        {
            float cell = LabBenchLayout.CellPitch, gridHalf = (LabBenchLayout.GridColumns - 1) * 0.5f * cell;
            foreach (float s in LabBenchLayout.RowS)
            {
                foreach (float x in LabBenchLayout.ColumnX) yield return new Vector2(x, s);
                yield return new Vector2(0f, s);
                yield return new Vector2(-LabBenchLayout.ButtonPitch, s);
                yield return new Vector2(LabBenchLayout.ButtonPitch, s);
            }
            // Grilla de sigilos (2 filas), de signos (3 filas) y casillas del campo.
            foreach (float x in new[] { -gridHalf, gridHalf })
            {
                yield return new Vector2(x, LabBenchLayout.SigilGridS + cell * 0.5f);
                yield return new Vector2(x, LabBenchLayout.SignGridS + cell);
                yield return new Vector2(x, LabBenchLayout.SlotS);
            }
            for (int i = 0; i < LabBenchLayout.PagesPerPanel; i++) yield return new Vector2(LabBenchLayout.TabX(i), LabBenchLayout.TabS);
        }

        [Test]
        public void Consola_TodoControlAlAlcance_YHaciaAdelante()
        {
            foreach (int panel in Sides)
                foreach (Vector2 t in SideTargets())
                {
                    Vector3 p = LabBenchLayout.PanelPoint(panel, t.x, t.y);
                    Assert.LessOrEqual(LabBenchLayout.Reach(p), LabBenchLayout.ReachMax + 1e-4f, $"panel {panel}, x {t.x}, s {t.y}");
                    Assert.LessOrEqual(Mathf.Abs(LabBenchLayout.Bearing(p)), 90f, "como mucho al costado: nunca hay que girar hacia atras");
                    Assert.That(p.y, Is.InRange(LabBenchLayout.PanelBottom, LabBenchLayout.TopMax));
                }

            // Centro: botones y esquinas lejanas del atril.
            float half = LabBenchLayout.PadSize * 0.5f;
            var center = new[]
            {
                new Vector2(0f, LabBenchLayout.ButtonsS), new Vector2(-LabBenchLayout.FrontButtonX, LabBenchLayout.ButtonsS),
                new Vector2(LabBenchLayout.FrontButtonX, LabBenchLayout.ButtonsS),
                new Vector2(-half, LabBenchLayout.PadS + half), new Vector2(half, LabBenchLayout.PadS + half)
            };
            foreach (Vector2 t in center)
                Assert.LessOrEqual(LabBenchLayout.Reach(LabBenchLayout.PanelPoint(LabBenchLayout.Center, t.x, t.y)), LabBenchLayout.ReachMax + 1e-4f, $"centro {t}");
        }

        [Test]
        public void Consola_BajaYAbierta_BordesYPendiente()
        {
            Assert.AreEqual(40f, LabBenchLayout.Tilt, 5f, "paneles inclinados ~40°");
            Assert.AreEqual(0.8f, LabBenchLayout.PanelBottom, 0.05f);
            for (int panel = 0; panel < LabBenchLayout.PanelCount; panel++)
                Assert.LessOrEqual(LabBenchLayout.TopHeight(LabBenchLayout.PanelLength(panel)), LabBenchLayout.TopMax, $"borde de arriba del panel {panel}");
            // La pantalla (con su marco) tampoco pasa el tope.
            Assert.LessOrEqual(LabBenchLayout.TopHeight(LabBenchLayout.ScreenTo) + 0.02f, LabBenchLayout.TopMax + 1e-4f);
            Assert.LessOrEqual(LabBenchLayout.ScreenTo, LabBenchLayout.CenterLength);
            Assert.LessOrEqual(LabBenchLayout.RowS[0] + LabBenchLayout.RowPitch * 0.5f, LabBenchLayout.CrestFrom + 1e-4f, "los controles no pisan el copete");
        }

        [Test]
        public void Consola_PanelesContiguosSinPisarse_YSimetricos()
        {
            float center = LabBenchLayout.HalfAngle(LabBenchLayout.CenterWidth), side = LabBenchLayout.HalfAngle(LabBenchLayout.SideWidth);
            Assert.AreEqual(0f, LabBenchLayout.PanelYaw(LabBenchLayout.Center));
            Assert.AreEqual(-LabBenchLayout.PanelYaw(LabBenchLayout.Right), LabBenchLayout.PanelYaw(LabBenchLayout.Left), 1e-4f);
            Assert.GreaterOrEqual(LabBenchLayout.PanelYaw(LabBenchLayout.Right) - side, center - 1e-3f, "el lateral no pisa el centro");
            Assert.Less(LabBenchLayout.PanelYaw(LabBenchLayout.Right) + side, 100f, "el borde de afuera queda al costado, no atras");
        }

        [Test]
        public void Consola_SeparacionDeControlesYPestanas()
        {
            Assert.GreaterOrEqual(LabBenchLayout.ColumnX[1] - LabBenchLayout.ColumnX[0], LabPanelLayout.MinControlPitch - 1e-5f);
            for (int i = 1; i < LabBenchLayout.RowS.Length; i++)
                Assert.GreaterOrEqual(LabBenchLayout.RowS[i - 1] - LabBenchLayout.RowS[i], LabBenchLayout.RowPitch - 1e-5f);
            Assert.GreaterOrEqual(LabBenchLayout.CellPitch, 0.085f - 1e-5f, "blancos de VR ≥ 8,5 cm");
            Assert.GreaterOrEqual(LabBenchLayout.TabPitch, LabBenchLayout.TabWidth, "las pestañas no se pisan");
            float tabsHalf = LabBenchLayout.TabX(LabBenchLayout.PagesPerPanel - 1) + LabBenchLayout.TabWidth * 0.5f;
            Assert.LessOrEqual(tabsHalf, LabBenchLayout.SideWidth * 0.5f, "las pestañas entran en el panel");
            // La plaquita de la fila de abajo no baja hasta las pestañas (lectura 6,6 cm abajo, ventanita de 2,8 cm).
            float lowest = LabBenchLayout.RowS[LabBenchLayout.RowS.Length - 1] - 0.066f - 0.014f;
            Assert.GreaterOrEqual(lowest, LabBenchLayout.TabS + LabBenchLayout.TabDepth * 0.5f);
            // Grilla de signos (3 filas) arriba de la fila de cantidad/angulo.
            Assert.GreaterOrEqual(LabBenchLayout.SignGridS - 1.5f * LabBenchLayout.CellPitch, LabBenchLayout.SignRowS + 0.075f);
        }

        [Test]
        public void Pantalla_LegibleDesdeElPuntoDePie()
        {
            Vector3 middle = LabBenchLayout.PanelPoint(LabBenchLayout.Center, 0f, (LabBenchLayout.ScreenFrom + LabBenchLayout.ScreenTo) * 0.5f);
            Assert.LessOrEqual(LabBenchLayout.EyeDistance(middle), LabBenchLayout.ScreenReadMax, "a ≤ 0,9 m del ojo");
            Assert.GreaterOrEqual(LabPanelLayout.TextHeight(0.22f), LabBenchLayout.ScreenTextMin - 1e-5f, "letra ≥ 2,2 cm");
            Assert.GreaterOrEqual(LabBenchLayout.ScreenFrom, LabBenchLayout.PadS + LabBenchLayout.PadSize * 0.5f, "la pantalla queda arriba del atril");
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
