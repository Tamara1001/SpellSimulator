using System.Collections.Generic;
using NUnit.Framework;
using SpellSimulator.Lab;
using UnityEngine;

namespace SpellSimulator.Tests
{
    /// <summary>Logica pura del Laboratorio v4: visibilidad con lentes, alcance del banco compacto e iconos de glifos.</summary>
    public class LabV4Tests
    {
        // ---------------------------------------------------------------- datos solo con lentes

        [Test]
        public void Lentes_SinLentesNuncaSeVe()
        {
            Assert.IsFalse(LabLensVisibility.IsVisible(false, 0.5f, 0f, false));
            Assert.IsFalse(LabLensVisibility.IsVisible(false, 0.5f, 0f, true));
        }

        [Test]
        public void Lentes_CercaYMirando_SeVe_LejosOAlCostado_No()
        {
            Assert.IsTrue(LabLensVisibility.IsVisible(true, 2.9f, 49f, false));
            Assert.IsFalse(LabLensVisibility.IsVisible(true, 3.1f, 10f, false), "mas de 3 m");
            Assert.IsFalse(LabLensVisibility.IsVisible(true, 1f, 51f, false), "mas de 50°");
            Assert.IsFalse(LabLensVisibility.IsVisible(true, float.NaN, 0f, false));
            Assert.IsFalse(LabLensVisibility.IsVisible(true, -1f, 0f, false));
        }

        [Test]
        public void Lentes_Histeresis_NoParpadeaEnElBorde()
        {
            // Visible: sigue visible un poco mas alla del umbral de muestra.
            Assert.IsTrue(LabLensVisibility.IsVisible(true, 3.2f, 10f, true));
            Assert.IsTrue(LabLensVisibility.IsVisible(true, 1f, 55f, true));
            // Pasado el umbral de ocultado, se oculta.
            Assert.IsFalse(LabLensVisibility.IsVisible(true, LabLensVisibility.HideDistance + 0.01f, 10f, true));
            Assert.IsFalse(LabLensVisibility.IsVisible(true, 1f, LabLensVisibility.HideAngleDeg + 0.5f, true));
            // Oculto: en la zona de histeresis no aparece.
            Assert.IsFalse(LabLensVisibility.IsVisible(true, 3.2f, 10f, false));
            Assert.Greater(LabLensVisibility.HideDistance, LabLensVisibility.ShowDistance);
            Assert.Greater(LabLensVisibility.HideAngleDeg, LabLensVisibility.ShowAngleDeg);
        }

        [Test]
        public void Lentes_DistanciaPropia_ParaElPizarron()
        {
            Assert.IsTrue(LabLensVisibility.IsVisible(true, 5.5f, 20f, false, 6f));
            Assert.IsFalse(LabLensVisibility.IsVisible(true, 5.5f, 20f, false));
        }

        // ---------------------------------------------------------------- banco compacto

        [Test]
        public void Banco_TodoControlAlAlcance()
        {
            float gridHalf = (LabBenchLayout.GridColumns - 1) * 0.5f * LabBenchLayout.CellPitch;
            float[] xs = { LabBenchLayout.ColumnX[0], LabBenchLayout.ColumnX[1], -gridHalf, gridHalf, -LabBenchLayout.ButtonPitch, LabBenchLayout.ButtonPitch, 0f };
            for (int facet = 0; facet < LabBenchLayout.FacetCount; facet++)
                foreach (float s in LabBenchLayout.RowS)
                    foreach (float x in xs)
                    {
                        Vector3 p = LabBenchLayout.FacetPoint(facet, x, s);
                        Assert.LessOrEqual(LabBenchLayout.Reach(p), LabBenchLayout.ReachMax + 1e-4f, $"panel {facet}, x {x}, s {s}");
                        Assert.That(p.y, Is.InRange(0.8f, 1.8f), "altura comoda");
                    }
            Assert.LessOrEqual(LabBenchLayout.Reach(LabBenchLayout.PadCenter), LabBenchLayout.ReachMax, "atril de dibujo");
            Vector3 side = LabBenchLayout.LaunchCenter + new Vector3(LabBenchLayout.FrontButtonX, 0f, 0f);
            Assert.LessOrEqual(LabBenchLayout.Reach(side), LabBenchLayout.ReachMax, "botones de adelante");
        }

        [Test]
        public void Banco_ProfundidadUtilYSeparacion()
        {
            Assert.LessOrEqual(LabBenchLayout.FacetDepth, LabBenchLayout.MaxUsefulDepth);
            Assert.LessOrEqual(LabBenchLayout.FrontDepth, LabBenchLayout.MaxUsefulDepth);
            Assert.GreaterOrEqual(LabBenchLayout.ColumnX[1] - LabBenchLayout.ColumnX[0], LabPanelLayout.MinControlPitch - 1e-5f);
            for (int i = 1; i < LabBenchLayout.RowS.Length; i++)
                Assert.GreaterOrEqual(LabBenchLayout.RowS[i - 1] - LabBenchLayout.RowS[i], LabBenchLayout.RowPitch - 1e-5f);
            Assert.GreaterOrEqual(LabBenchLayout.CellPitch, 0.085f - 1e-5f, "blancos de VR ≥ 8,5 cm");
            Assert.LessOrEqual(LabBenchLayout.RowS[0] + LabBenchLayout.RowPitch * 0.5f, LabBenchLayout.FacetSlope + 1e-5f);
            Assert.GreaterOrEqual(LabBenchLayout.RowS[LabBenchLayout.RowS.Length - 1] - LabBenchLayout.RowPitch * 0.5f, 0f);
        }

        [Test]
        public void Banco_PanelesEnArcoSinPisarseNiTaparLaPista()
        {
            float half = LabBenchLayout.FacetHalfAngle;
            float li = LabBenchLayout.FacetYaw(LabBenchLayout.LeftInner), lo = LabBenchLayout.FacetYaw(LabBenchLayout.LeftOuter);
            float ri = LabBenchLayout.FacetYaw(LabBenchLayout.RightInner), ro = LabBenchLayout.FacetYaw(LabBenchLayout.RightOuter);
            Assert.AreEqual(-ri, li, 1e-4f);
            Assert.AreEqual(-ro, lo, 1e-4f);
            Assert.GreaterOrEqual(ri - half, LabBenchLayout.FrontHalfAngle - 1e-3f, "la mesa de adelante queda libre: se ve la pista");
            Assert.GreaterOrEqual(ro - ri, 2f * half - 1e-3f, "los paneles no se pisan");
            Assert.Less(ro + half, 180f, "queda un hueco atras para entrar");
        }

        // ---------------------------------------------------------------- iconos de glifos

        [Test]
        public void Iconos_PintanElTrazoDentroDeLaCelda()
        {
            const int size = 32, width = 64;
            var pixels = new Color32[width * size];
            var paper = new Color32(255, 255, 255, 255);
            var ink = new Color32(0, 0, 0, 255);
            LabGlyphIcons.Fill(pixels, width, 32, 0, size, paper);
            var strokes = new List<List<Vector2>> { new List<Vector2> { new Vector2(0f, 0.5f), new Vector2(1f, 0.5f) } };
            int painted = LabGlyphIcons.Draw(strokes, pixels, width, 32, 0, size, ink, 3f);
            Assert.Greater(painted, 0);
            // Linea horizontal centrada: el centro de la celda tiene tinta; las esquinas y la otra celda no.
            Assert.AreEqual(0, pixels[(size / 2) * width + 32 + size / 2].r);
            Assert.AreEqual(255, pixels[0 * width + 32].r, "esquina de abajo sin tinta");
            Assert.AreEqual(255, pixels[(size - 1) * width + 63].r, "esquina de arriba sin tinta");
            for (int i = 0; i < pixels.Length; i++)
                if (i % width < 32) Assert.AreEqual(0, pixels[i].a, "no se sale de la celda");
        }

        [Test]
        public void Iconos_SinTrazos_NoPintanNada_YNingunoSiPinta()
        {
            var pixels = new Color32[16 * 16];
            Assert.AreEqual(0, LabGlyphIcons.Draw(null, pixels, 16, 0, 0, 16, new Color32(0, 0, 0, 255), 2f));
            Assert.AreEqual(0, LabGlyphIcons.Draw(new List<List<Vector2>>(), pixels, 16, 0, 0, 16, new Color32(0, 0, 0, 255), 2f));
            Assert.Greater(LabGlyphIcons.DrawNone(pixels, 16, 0, 0, 16, new Color32(9, 9, 9, 255), 2f), 0);
        }
    }
}
