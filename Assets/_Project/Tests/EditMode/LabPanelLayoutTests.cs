using NUnit.Framework;
using SpellSimulator.Lab;

namespace SpellSimulator.Tests
{
    public class LabPanelLayoutTests
    {
        [Test]
        public void TextY_QuedaAlMenos5mmSobreCadaSuperficie()
        {
            // Losa del panel, franja de color, plaquita/ventanita, tapa de boton y frente del tablero.
            float[] surfaces = { 0f, LabPanelLayout.SurfaceTop, LabPanelLayout.PlaqueTop, 0.06f, 1.11f };
            foreach (float s in surfaces)
                Assert.GreaterOrEqual(LabPanelLayout.TextY(s) - s, LabPanelLayout.MinClearance - 1e-6f, $"superficie {s}");
            Assert.GreaterOrEqual(LabPanelLayout.MinClearance, 0.005f - 1e-6f);
            Assert.GreaterOrEqual(LabPanelLayout.TextLift, 0.006f - 1e-6f);
        }

        [Test]
        public void Plaquita_ElevadaSobreLaFranja()
        {
            Assert.AreEqual(LabPanelLayout.SurfaceTop + 0.003f, LabPanelLayout.PlaqueTop, 1e-6f);
            Assert.Greater(LabPanelLayout.TextY(LabPanelLayout.PlaqueTop), LabPanelLayout.PlaqueTop);
        }

        [Test]
        public void Fuentes_FijasYLegibles()
        {
            Assert.GreaterOrEqual(LabPanelLayout.TextHeight(LabPanelLayout.LabelFontSize), LabPanelLayout.TextHeightMin);
            Assert.Greater(LabPanelLayout.ReadoutFontSize, LabPanelLayout.LabelFontSize);
            Assert.Greater(LabPanelLayout.TitleFontSize, LabPanelLayout.ReadoutFontSize);
        }

        [Test]
        public void Rotulos_HastaDiezCaracteres()
        {
            Assert.IsTrue(LabPanelLayout.LabelFits("Inclinac."));
            Assert.IsTrue(LabPanelLayout.LabelFits("Velocidad"));
            Assert.IsFalse(LabPanelLayout.LabelFits("Adentro/afuera"));
            Assert.IsFalse(LabPanelLayout.LabelFits(null));
        }

        [Test]
        public void Filas_NoQuitanControlesYRespetanElPaso()
        {
            Assert.AreEqual(4, LabPanelLayout.MaxPerRow(1.2f));
            Assert.AreEqual(2, LabPanelLayout.RowsNeeded(5, 1.2f)); // 5 no entran: dos filas
            Assert.AreEqual(1, LabPanelLayout.RowsNeeded(4, 1.2f));
            Assert.AreEqual(0, LabPanelLayout.RowsNeeded(0, 1.2f));
            Assert.AreEqual(1, LabPanelLayout.MaxPerRow(0.1f));
            Assert.AreEqual(35f, LabPanelLayout.PanelTilt);
        }
    }
}
