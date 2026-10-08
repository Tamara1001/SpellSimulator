using NUnit.Framework;
using SpellSimulator.UI;

namespace SpellSimulator.Tests
{
    /// <summary>Textos de la HUD (UI Toolkit): pistas de teclas, pistas de la mano secundaria y avisos de diagnostico.</summary>
    public class HudTextTests
    {
        [Test]
        public void ParseHints_SplitsKeysAndActions_AndMapsNumberWidthsToWheel()
        {
            var hints = HudText.ParseHints("Clic: dibujar | Clic der.: deshacer | 5-0: grosor | R: tinta");
            Assert.AreEqual(4, hints.Count);
            Assert.AreEqual("Clic", hints[0].Key);
            Assert.AreEqual("Dibujar", hints[0].Action);
            Assert.AreEqual("Clic der.", hints[1].Key);
            Assert.AreEqual("Rueda", hints[2].Key);
            Assert.AreEqual("Tinta", hints[3].Action);
        }

        [Test]
        public void ParseHints_EmptyAndKeylessParts()
        {
            Assert.AreEqual(0, HudText.ParseHints(null).Count);
            var hints = HudText.ParseHints("Vacia | Q: arrojar");
            Assert.AreEqual("", hints[0].Key);
            Assert.AreEqual("Vacia", hints[0].Action);
            Assert.AreEqual("Q", hints[1].Key);
        }

        [Test]
        public void SecondaryHints_OnlyRelevantKeys_AndCached()
        {
            string box = HudText.SecondaryHints(false, false, false, true);
            StringAssert.DoesNotContain("F:", box);
            StringAssert.DoesNotContain("V:", box);
            StringAssert.Contains("Rueda: distancia", box);
            StringAssert.Contains("E: soltar", box);

            string notebook = HudText.SecondaryHints(true, true, true, true);
            StringAssert.Contains("Z/X: página", notebook);
            StringAssert.Contains("F: dar vuelta", notebook);
            StringAssert.Contains("V: a pantalla", notebook);
            Assert.AreSame(notebook, HudText.SecondaryHints(true, true, true, true));
        }

        [Test]
        public void CleanDiagnostic_StripsRichText_AndRejectsSpellReport()
        {
            Assert.AreEqual("[Libro] Tratado · Aprendiste: Fuego.", HudText.CleanDiagnostic("<b>[Libro] Tratado</b>\nAprendiste: Fuego."));
            Assert.IsNull(HudText.CleanDiagnostic("<b>[Hechizo]</b> informe"));
            Assert.IsNull(HudText.CleanDiagnostic(new string('x', 250)));
            Assert.IsNull(HudText.CleanDiagnostic("<i></i>"));
        }

        [Test]
        public void PrettyName_CleansSceneNames()
        {
            Assert.AreEqual("Caja Madera", HudText.PrettyName("Caja_Madera (1)"));
            Assert.AreEqual("Objeto", HudText.PrettyName(""));
        }
    }
}
