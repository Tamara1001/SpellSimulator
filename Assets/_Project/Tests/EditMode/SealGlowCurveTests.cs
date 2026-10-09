using NUnit.Framework;
using SpellSimulator.Output;

namespace SpellSimulator.Tests
{
    /// <summary>Brillo del anillo al cargar un sello: curva creciente, pulso que se acelera y destello que se apaga.</summary>
    public class SealGlowCurveTests
    {
        [Test]
        public void Intensity_ValoresEnLosExtremos()
        {
            Assert.AreEqual(0.35f, SealGlowCurve.Intensity(0f), 1e-5f);
            Assert.AreEqual(1.6f, SealGlowCurve.Intensity(1f), 1e-5f);
            // Fuera de rango se recorta.
            Assert.AreEqual(0.35f, SealGlowCurve.Intensity(-1f), 1e-5f);
            Assert.AreEqual(1.6f, SealGlowCurve.Intensity(3f), 1e-5f);
        }

        [Test]
        public void Intensity_EsMonotonaCreciente()
        {
            float previous = SealGlowCurve.Intensity(0f);
            for (int i = 1; i <= 100; i++)
            {
                float value = SealGlowCurve.Intensity(i / 100f);
                Assert.GreaterOrEqual(value, previous, $"Baja en t={i / 100f}");
                previous = value;
            }
            // Cuadratica: a mitad de carga todavia esta por debajo del punto medio lineal.
            Assert.Less(SealGlowCurve.Intensity(0.5f), (0.35f + 1.6f) * 0.5f);
        }

        [Test]
        public void PulseHz_SeAcelera()
        {
            Assert.AreEqual(2f, SealGlowCurve.PulseHz(0f), 1e-5f);
            Assert.AreEqual(9f, SealGlowCurve.PulseHz(1f), 1e-5f);
            Assert.Greater(SealGlowCurve.PulseHz(0.6f), SealGlowCurve.PulseHz(0.3f));
        }

        [Test]
        public void Flash_SeMantieneYDecaeACero()
        {
            Assert.AreEqual(3f, SealGlowCurve.FlashIntensity(0f), 1e-5f);
            Assert.AreEqual(3f, SealGlowCurve.FlashIntensity(0.15f), 1e-5f);
            Assert.Less(SealGlowCurve.FlashIntensity(0.25f), 3f);
            Assert.AreEqual(0f, SealGlowCurve.FlashIntensity(0.4f), 1e-5f);
            Assert.AreEqual(0f, SealGlowCurve.FlashIntensity(5f), 1e-5f);

            float previous = SealGlowCurve.FlashIntensity(0.15f);
            for (int i = 1; i <= 50; i++)
            {
                float value = SealGlowCurve.FlashIntensity(0.15f + 0.25f * i / 50f);
                Assert.LessOrEqual(value, previous);
                previous = value;
            }
        }

        [Test]
        public void FadeOut_LlegaACeroEnUnDecimo()
        {
            Assert.AreEqual(1.2f, SealGlowCurve.FadeOut(1.2f, 0f), 1e-5f);
            Assert.AreEqual(0.6f, SealGlowCurve.FadeOut(1.2f, 0.05f), 1e-5f);
            Assert.AreEqual(0f, SealGlowCurve.FadeOut(1.2f, 0.1f), 1e-5f);
            Assert.AreEqual(0f, SealGlowCurve.FadeOut(1.2f, 1f), 1e-5f);
        }
    }
}
