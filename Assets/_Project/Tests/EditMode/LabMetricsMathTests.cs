using NUnit.Framework;
using SpellSimulator.Lab;
using UnityEngine;

namespace SpellSimulator.Tests
{
    public class LabMetricsMathTests
    {
        [Test]
        public void AreaQuemada_CuatroMetrosPorCelda()
        {
            Assert.AreEqual(0f, LabMetricsMath.AreaBurnedM2(0));
            Assert.AreEqual(0f, LabMetricsMath.AreaBurnedM2(-3));
            Assert.AreEqual(4f, LabMetricsMath.AreaBurnedM2(1));
            Assert.AreEqual(100f, LabMetricsMath.AreaBurnedM2(25));
        }

        [Test]
        public void Desplazamiento_IgnoraLaAltura()
        {
            Assert.AreEqual(5f, LabMetricsMath.HorizontalDisplacement(new Vector3(0f, 0f, 0f), new Vector3(3f, 10f, 4f)), 1e-5f);
            Assert.AreEqual(0f, LabMetricsMath.HorizontalDisplacement(new Vector3(1f, 0f, 1f), new Vector3(1f, -2f, 1f)), 1e-5f);
        }

        [Test]
        public void Reposo_EmpiezaSoloSobreElUmbral()
        {
            var state = default(SettleState);
            Assert.IsFalse(LabMetricsMath.SettleStep(ref state, 0.2f, 0.1f));
            Assert.IsFalse(state.Started);
            Assert.IsFalse(LabMetricsMath.SettleStep(ref state, 1f, 0.1f));
            Assert.IsTrue(state.Started);
            Assert.IsTrue(state.Moving);
            Assert.IsTrue(state.Active);
            Assert.AreEqual(0f, state.Elapsed);
        }

        [Test]
        public void Reposo_TerminaTrasUnSegundoQuieto()
        {
            var state = default(SettleState);
            LabMetricsMath.SettleStep(ref state, 2f, 0.1f);
            for (int i = 0; i < 9; i++) Assert.IsFalse(LabMetricsMath.SettleStep(ref state, 0.1f, 0.1f), "paso " + i);
            Assert.IsFalse(state.Moving);
            Assert.IsTrue(LabMetricsMath.SettleStep(ref state, 0.1f, 0.1f));
            Assert.IsTrue(state.Finished);
            Assert.IsFalse(state.TimedOut);
            Assert.IsFalse(state.Active);
            Assert.AreEqual(1f, state.Elapsed, 1e-3f);
            // Terminado: no vuelve a disparar.
            Assert.IsFalse(LabMetricsMath.SettleStep(ref state, 0.1f, 0.1f));
        }

        [Test]
        public void Reposo_SiVuelveAMoverseReiniciaLaQuietud()
        {
            var state = default(SettleState);
            LabMetricsMath.SettleStep(ref state, 2f, 0.1f);
            for (int i = 0; i < 8; i++) LabMetricsMath.SettleStep(ref state, 0f, 0.1f);
            LabMetricsMath.SettleStep(ref state, 1f, 0.1f);
            Assert.AreEqual(0f, state.StillTime);
            for (int i = 0; i < 9; i++) Assert.IsFalse(LabMetricsMath.SettleStep(ref state, 0f, 0.1f));
            Assert.IsTrue(LabMetricsMath.SettleStep(ref state, 0f, 0.1f));
            Assert.IsFalse(state.TimedOut);
        }

        [Test]
        public void Reposo_TerminaPorElTopeDeQuinceSegundos()
        {
            var state = default(SettleState);
            LabMetricsMath.SettleStep(ref state, 1f, 0.1f);
            int steps = 0;
            bool finished = false;
            while (!finished && steps < 1000)
            {
                finished = LabMetricsMath.SettleStep(ref state, 1f, 0.1f);
                steps++;
            }
            Assert.IsTrue(finished);
            Assert.IsTrue(state.TimedOut);
            Assert.AreEqual(LabMetricsMath.MaxSegmentSeconds, state.Elapsed, 0.11f);
            Assert.AreEqual(150, steps, 1);
        }
    }
}
