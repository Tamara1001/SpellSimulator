using NUnit.Framework;
using SpellSimulator.Lab;

namespace SpellSimulator.Tests
{
    public class LabParameterMathTests
    {
        private static LabVariableDef Def(LabVariableId id) => LabCatalog.Get(id);

        [Test]
        public void Catalogo_IndexadoPorIdYConLosDefaultsDeLaTabla()
        {
            Assert.AreEqual(8, LabCatalog.Count);
            for (int i = 0; i < LabCatalog.Count; i++) Assert.AreEqual((LabVariableId)i, LabCatalog.Defs[i].Id);

            AssertDef(LabVariableId.Mass, 1f, 600f, 10f);
            AssertDef(LabVariableId.WindYaw, 0f, 315f, 0f, 45f);
            AssertDef(LabVariableId.WindSpeed, 0f, 20f, 6f, 1f);
            AssertDef(LabVariableId.FireSeedRadius, 0.5f, 4f, 1f, 0.5f);
            AssertDef(LabVariableId.FireDecay, 0.5f, 0.95f, 0.78f, 0.05f);
            AssertDef(LabVariableId.FireMaxGenerations, 0f, 12f, 6f, 1f);
            AssertDef(LabVariableId.WaterFlow, 0.5f, 3f, 1f, 0.25f);
            AssertDef(LabVariableId.PowerScale, 0.5f, 2f, 1f, 0.25f);

            CollectionAssert.AreEqual(new[] { 1f, 2f, 5f, 10f, 25f, 50f, 100f, 200f, 400f, 600f }, Def(LabVariableId.Mass).Steps);
            Assert.IsTrue(Def(LabVariableId.Mass).HasSteps);
            Assert.IsFalse(Def(LabVariableId.WindSpeed).HasSteps);
        }

        [Test]
        public void Defaults_SonDefault()
        {
            foreach (var def in LabCatalog.Defs) Assert.IsTrue(LabParameterMath.IsDefault(def.Default, def), def.Label);
            Assert.IsFalse(LabParameterMath.IsDefault(25f, Def(LabVariableId.Mass)));
            Assert.IsFalse(LabParameterMath.IsDefault(0.8f, Def(LabVariableId.FireDecay)));
        }

        [Test]
        public void Masa_PasosDiscretosEnAmbosSentidos()
        {
            var mass = Def(LabVariableId.Mass);
            Assert.AreEqual(25f, LabParameterMath.Step(10f, mass, 1));
            Assert.AreEqual(5f, LabParameterMath.Step(10f, mass, -1));
            Assert.AreEqual(600f, LabParameterMath.Step(400f, mass, 1));
            Assert.AreEqual(2f, LabParameterMath.Step(5f, mass, -1));
            // Fuera de tabla: va al vecino en el sentido pedido.
            Assert.AreEqual(10f, LabParameterMath.Step(7f, mass, 1));
            Assert.AreEqual(5f, LabParameterMath.Step(7f, mass, -1));
        }

        [Test]
        public void Masa_TopesEnLosExtremos()
        {
            var mass = Def(LabVariableId.Mass);
            Assert.AreEqual(600f, LabParameterMath.Step(600f, mass, 1));
            Assert.AreEqual(1f, LabParameterMath.Step(1f, mass, -1));
            Assert.AreEqual(600f, LabParameterMath.Step(5000f, mass, 1));

            float v = 1f;
            for (int i = 0; i < 20; i++) v = LabParameterMath.Step(v, mass, 1);
            Assert.AreEqual(600f, v);
            for (int i = 0; i < 20; i++) v = LabParameterMath.Step(v, mass, -1);
            Assert.AreEqual(1f, v);
        }

        [Test]
        public void Continuo_PasoYTopes()
        {
            var speed = Def(LabVariableId.WindSpeed);
            Assert.AreEqual(7f, LabParameterMath.Step(6f, speed, 1));
            Assert.AreEqual(5f, LabParameterMath.Step(6f, speed, -1));
            Assert.AreEqual(20f, LabParameterMath.Step(20f, speed, 1));
            Assert.AreEqual(0f, LabParameterMath.Step(0f, speed, -1));

            var yaw = Def(LabVariableId.WindYaw);
            Assert.AreEqual(45f, LabParameterMath.Step(0f, yaw, 1));
            Assert.AreEqual(315f, LabParameterMath.Step(315f, yaw, 1));
        }

        [Test]
        public void Continuo_DesdeElDefaultFueraDeGrilla()
        {
            var decay = Def(LabVariableId.FireDecay);
            Assert.AreEqual(0.8f, LabParameterMath.Step(0.78f, decay, 1));
            Assert.AreEqual(0.75f, LabParameterMath.Step(0.78f, decay, -1));
        }

        [Test]
        public void Continuo_SinDerivaDeFloats()
        {
            var decay = Def(LabVariableId.FireDecay);
            float v = decay.Min;
            for (int i = 0; i < 6; i++) v = LabParameterMath.Step(v, decay, 1);
            Assert.AreEqual(0.8f, v);
            for (int i = 0; i < 10; i++) v = LabParameterMath.Step(v, decay, 1);
            Assert.AreEqual(0.95f, v);
            for (int i = 0; i < 9; i++) v = LabParameterMath.Step(v, decay, -1);
            Assert.AreEqual(0.5f, v);

            var flow = Def(LabVariableId.WaterFlow);
            float f = flow.Min;
            for (int i = 0; i < 10; i++) f = LabParameterMath.Step(f, flow, 1);
            Assert.AreEqual(3f, f);
        }

        [Test]
        public void SnapYClamp()
        {
            var radius = Def(LabVariableId.FireSeedRadius);
            Assert.AreEqual(1.5f, LabParameterMath.Snap(1.4f, radius));
            Assert.AreEqual(4f, LabParameterMath.Snap(9f, radius));
            Assert.AreEqual(0.5f, LabParameterMath.Snap(-1f, radius));
            Assert.AreEqual(0.5f, LabParameterMath.Clamp(0.1f, radius));
            Assert.AreEqual(4f, LabParameterMath.Clamp(7f, radius));
            Assert.AreEqual(10f, LabParameterMath.Snap(11f, Def(LabVariableId.Mass)));
            Assert.AreEqual(0.8f, LabParameterMath.Step(0.8f, Def(LabVariableId.FireDecay), 0));
        }

        [Test]
        public void Normalized_LinealYLogaritmico()
        {
            var speed = Def(LabVariableId.WindSpeed);
            Assert.AreEqual(0f, LabParameterMath.Normalized(0f, speed), 1e-5f);
            Assert.AreEqual(0.5f, LabParameterMath.Normalized(10f, speed), 1e-5f);
            Assert.AreEqual(1f, LabParameterMath.Normalized(40f, speed), 1e-5f);

            var mass = Def(LabVariableId.Mass);
            Assert.AreEqual(0f, LabParameterMath.Normalized(1f, mass), 1e-5f);
            Assert.AreEqual(3f / 9f, LabParameterMath.Normalized(10f, mass), 1e-5f);
            Assert.AreEqual(1f, LabParameterMath.Normalized(600f, mass), 1e-5f);
            float between = LabParameterMath.Normalized(15f, mass);
            Assert.Greater(between, 3f / 9f);
            Assert.Less(between, 4f / 9f);
        }

        [Test]
        public void Format_ComaDecimalYUnidad()
        {
            Assert.AreEqual("10 kg", LabParameterMath.Format(10f, Def(LabVariableId.Mass)));
            Assert.AreEqual("45°", LabParameterMath.Format(45f, Def(LabVariableId.WindYaw)));
            Assert.AreEqual("6 m/s", LabParameterMath.Format(6f, Def(LabVariableId.WindSpeed)));
            Assert.AreEqual("1,5 m", LabParameterMath.Format(1.5f, Def(LabVariableId.FireSeedRadius)));
            Assert.AreEqual("0,78", LabParameterMath.Format(0.78f, Def(LabVariableId.FireDecay)));
            Assert.AreEqual("6", LabParameterMath.Format(6f, Def(LabVariableId.FireMaxGenerations)));
            Assert.AreEqual("1,25x", LabParameterMath.Format(1.25f, Def(LabVariableId.WaterFlow)));
        }

        private static void AssertDef(LabVariableId id, float min, float max, float defaultValue, float step = -1f)
        {
            var def = Def(id);
            Assert.AreEqual(min, def.Min, 1e-6f, id.ToString());
            Assert.AreEqual(max, def.Max, 1e-6f, id.ToString());
            Assert.AreEqual(defaultValue, def.Default, 1e-6f, id.ToString());
            if (step >= 0f) Assert.AreEqual(step, def.Step, 1e-6f, id.ToString());
            Assert.IsFalse(string.IsNullOrEmpty(def.Label));
        }
    }
}
