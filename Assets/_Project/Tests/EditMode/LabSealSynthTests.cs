using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Lab;
using SpellSimulator.Learning;

namespace SpellSimulator.Tests
{
    public class LabSealSynthTests
    {
        // Plantilla falsa en [0,1]: sigilo = 2 trazos, signo = 1 trazo.
        private static List<List<Vector2>> Template(string id)
        {
            var t = new List<List<Vector2>>
            {
                new List<Vector2> { new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f) }
            };
            if (id == "sigil") t.Add(new List<Vector2> { new Vector2(0.1f, 0.9f), new Vector2(0.9f, 0.1f) });
            return t;
        }

        private static LabSealSpec Spec(int signs)
        {
            var s = LabSealSpec.Default();
            s.sigilId = "sigil";
            s.signId = "sign";
            s.signCount = signs;
            return s;
        }

        [Test]
        public void CantidadDeTrazos_SigiloMasSignosMasAnillo()
        {
            Assert.AreEqual(2 + 0 + 1, LabSealSynth.Build(Spec(0), Template).Count);
            Assert.AreEqual(2 + 3 + 1, LabSealSynth.Build(Spec(3), Template).Count);
            Assert.AreEqual(2 + 4 + 1, LabSealSynth.Build(Spec(9), Template).Count); // recortado a 4
        }

        [Test]
        public void MismaSemilla_MismosPuntos_OtraSemilla_Distintos()
        {
            var a = Spec(2); a.tremor = 0.03f; a.seed = 7;
            var b = a; var c = a; c.seed = 8;
            var ra = LabSealSynth.Build(a, Template);
            var rb = LabSealSynth.Build(b, Template);
            var rc = LabSealSynth.Build(c, Template);
            Assert.AreEqual(ra.Count, rb.Count);
            bool differs = false;
            for (int k = 0; k < ra.Count; k++)
                for (int i = 0; i < ra[k].Count; i++)
                {
                    Assert.AreEqual(ra[k][i], rb[k][i]);
                    if (ra[k][i] != rc[k][i]) differs = true;
                }
            Assert.IsTrue(differs);
        }

        [Test]
        public void SinTemblor_NoDependeDeLaSemilla()
        {
            var a = Spec(2); a.seed = 1;
            var b = a; b.seed = 99;
            var ra = LabSealSynth.Build(a, Template);
            var rb = LabSealSynth.Build(b, Template);
            for (int k = 0; k < ra.Count; k++)
                for (int i = 0; i < ra[k].Count; i++) Assert.AreEqual(ra[k][i], rb[k][i]);
        }

        [Test]
        public void HuecoDelAnillo_AcortaElArcoYElExtremoNoCierra()
        {
            var closed = Spec(0);
            var gap = Spec(0); gap.ringGapDeg = 40f;
            var ringClosed = Last(LabSealSynth.Build(closed, Template));
            var ringGap = Last(LabSealSynth.Build(gap, Template));
            Assert.Less(Vector2.Distance(ringClosed[0], ringClosed[ringClosed.Count - 1]), 1e-4f);
            Assert.Greater(Vector2.Distance(ringGap[0], ringGap[ringGap.Count - 1]), 0.02f);
        }

        [Test]
        public void Anillo_EstaEnUvConElRadioDelLlenado()
        {
            var s = Spec(0); s.ringFill = 0.6f;
            var ring = Last(LabSealSynth.Build(s, Template));
            foreach (var p in ring) Assert.AreEqual(0.3f, Vector2.Distance(p, new Vector2(0.5f, 0.5f)), 1e-4f);
        }

        [Test]
        public void Clamp_RecortaTodosLosCampos()
        {
            var s = LabSealSpec.Default();
            s.paperSize = 0.5f; s.ringFill = 5f; s.sigilScale = 0f; s.signCount = -2; s.baseAngleDeg = 100f;
            s.turnDeg = 150f; s.signLength = 9f; s.asymmetry = 0f; s.signDistance = 5f; s.ringGapDeg = 99f;
            s.tremor = 1f; s.paperFace = -5; s.paperTiltDeg = 200f; s.penWidthIndex = 9; s.ink = (LabInkKind)7;
            var c = LabSealSpec.Clamp(s);
            Assert.AreEqual(0.45f, c.paperSize, 1e-5f);
            Assert.AreEqual(LabSealSpec.MaxRingFill, c.ringFill);
            Assert.AreEqual(LabSealSpec.MinSigilScale, c.sigilScale);
            Assert.AreEqual(0, c.signCount);
            Assert.AreEqual(90f, c.baseAngleDeg);
            Assert.AreEqual(180f, c.turnDeg);
            Assert.AreEqual(LabSealSpec.MaxSignLength, c.signLength);
            Assert.AreEqual(LabSealSpec.MinAsymmetry, c.asymmetry);
            Assert.AreEqual(LabSealSpec.MaxSignDistance, c.signDistance);
            Assert.AreEqual(LabSealSpec.MaxRingGapDeg, c.ringGapDeg);
            Assert.AreEqual(LabSealSpec.MaxTremor, c.tremor);
            Assert.AreEqual(-1, c.paperFace);
            Assert.AreEqual(90f, c.paperTiltDeg);
            Assert.AreEqual(3, c.penWidthIndex);
            Assert.AreEqual(LabInkKind.Resistant, c.ink);
        }

        [Test]
        public void Default_UsaLasConstantesDelCanon()
        {
            var d = LabSealSpec.Default();
            Assert.AreEqual(SpellStrokeBuilder.SignDistance, d.signDistance);
            Assert.AreEqual(d, LabSealSpec.Clamp(d));
        }

        [Test]
        public void Catalogo_IndexadoPorId()
        {
            for (int i = 0; i < LabSealCatalog.Count; i++) Assert.AreEqual((LabSealVarId)i, LabSealCatalog.Defs[i].Id);
        }

        private static List<Vector2> Last(List<List<Vector2>> strokes) => strokes[strokes.Count - 1];
    }
}
