using System.Collections.Generic;
using NUnit.Framework;
using SpellSimulator.Lab;
using UnityEngine;

namespace SpellSimulator.Tests
{
    /// <summary>Logica pura del Laboratorio v4: visibilidad con lentes e iconos de glifos (el alcance de la consola v5 esta en LabV5Tests).</summary>
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
