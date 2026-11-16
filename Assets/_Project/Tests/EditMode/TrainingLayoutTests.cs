using NUnit.Framework;
using UnityEngine;
using SpellSimulator.World.Map;

namespace SpellSimulator.Tests
{
    /// <summary>Entrenamiento base: la distribucion relativa al atril se conserva al mudarla (Docs/LABORATORIO_V3_PLAN.md, d).</summary>
    public class TrainingLayoutTests
    {
        private static readonly Vector2 North = Vector2.up;

        private static void AssertAt(Vector2 expected, Vector2 actual, string what) =>
            Assert.AreEqual(0f, Vector2.Distance(expected, actual), 0.001f, $"{what}: esperado {expected}, real {actual}");

        [Test]
        public void ToWorld_WithOldFrame_ReproducesTheOldFieldPositions()
        {
            // El campo viejo: atril (28, -14) mirando a +Z. Estas son las posiciones que tenia cada objeto.
            Vector2 a = TrainingLayout.LegacyStation;
            AssertAt(new Vector2(28f, -16f), TrainingLayout.ToWorld(a, North, TrainingLayout.TestProps), "TestProps");
            AssertAt(new Vector2(28f, -5f), TrainingLayout.ToWorld(a, North, TrainingLayout.DummyA), "Dummy_A");
            AssertAt(new Vector2(24.5f, -11f), TrainingLayout.ToWorld(a, North, TrainingLayout.DummyB), "Dummy_B");
            AssertAt(new Vector2(25.8f, -16.5f), TrainingLayout.ToWorld(a, North, TrainingLayout.ToolTable), "ToolTable");
            AssertAt(new Vector2(30f, -16.5f), TrainingLayout.ToWorld(a, North, TrainingLayout.StampTable), "StampTable");
            AssertAt(new Vector2(25f, -16.5f), TrainingLayout.ToWorld(a, North, TrainingLayout.Contraptions), "artilugios");
            AssertAt(new Vector2(22f, -13.5f), TrainingLayout.ToWorld(a, North, TrainingLayout.GiantStand), "lienzo gigante");
            AssertAt(new Vector2(24f, -11.5f), TrainingLayout.ToWorld(a, North, TrainingLayout.GiantPaper), "papel gigante");
            AssertAt(new Vector2(21f, -18.5f), TrainingLayout.ToWorld(a, North, TrainingLayout.Easel), "caballete");
            AssertAt(TrainingLayout.LegacyFieldPoint, TrainingLayout.ToWorld(a, North, TrainingLayout.StampTable), "punto viejo del campo");
        }

        [Test]
        public void ToWorld_XIsRight_YIsForward()
        {
            var o = new Vector2(10f, 5f);
            AssertAt(new Vector2(10f, 9f), TrainingLayout.ToWorld(o, North, new Vector2(0f, 4f)), "adelante al norte");
            AssertAt(new Vector2(13f, 5f), TrainingLayout.ToWorld(o, North, new Vector2(3f, 0f)), "derecha al norte");
            // Mirando al este (+X), la derecha es el sur (-Z) y adelante es +X.
            AssertAt(new Vector2(14f, 5f), TrainingLayout.ToWorld(o, Vector2.right, new Vector2(0f, 4f)), "adelante al este");
            AssertAt(new Vector2(10f, 2f), TrainingLayout.ToWorld(o, Vector2.right, new Vector2(3f, 0f)), "derecha al este");
        }

        [Test]
        public void ToWorld_RotationKeepsDistancesBetweenObjects()
        {
            Vector2[] locals = { TrainingLayout.DummyA, TrainingLayout.DummyB, TrainingLayout.Easel, TrainingLayout.GiantPaper, TrainingLayout.StampTable };
            for (int i = 0; i < locals.Length; i++)
                for (int j = i + 1; j < locals.Length; j++)
                {
                    float local = Vector2.Distance(locals[i], locals[j]);
                    float world = Vector2.Distance(TrainingLayout.ToWorld(locals[i]), TrainingLayout.ToWorld(locals[j]));
                    Assert.AreEqual(local, world, 0.001f);
                }
            Assert.AreEqual(1f, ValleyShape.TrainingForward.magnitude, 0.001f, "el frente es unitario");
        }

        [Test]
        public void Offset_RotatesHorizontallyAndKeepsHeight()
        {
            Vector3 o = TrainingLayout.Offset(new Vector3(1f, 0.83f, 2f));
            Vector2 expected = TrainingLayout.ToWorld(Vector2.zero, ValleyShape.TrainingForward, new Vector2(1f, 2f));
            Assert.AreEqual(expected.x, o.x, 0.001f);
            Assert.AreEqual(0.83f, o.y, 0.0001f);
            Assert.AreEqual(expected.y, o.z, 0.001f);
        }

        [Test]
        public void YawDegrees_MatchesTheForwardVector()
        {
            Vector3 forward = Quaternion.Euler(0f, TrainingLayout.YawDegrees, 0f) * Vector3.forward;
            Assert.AreEqual(ValleyShape.TrainingForward.x, forward.x, 0.001f);
            Assert.AreEqual(ValleyShape.TrainingForward.y, forward.z, 0.001f);
        }

        [Test]
        public void TrainingSite_IsFarFromLabAndInsidePlayableArea()
        {
            Vector2 lab = SpellSimulator.EditorTools.Map.MapStructures.LaboratorySite;
            Assert.GreaterOrEqual(Vector2.Distance(ValleyShape.TrainingOrigin, lab), 12f);
            foreach (Vector2 local in new[] { TrainingLayout.DummyA, TrainingLayout.DummyB, TrainingLayout.Easel, TrainingLayout.GiantStand, TrainingLayout.TestProps })
            {
                Vector2 w = TrainingLayout.ToWorld(local);
                Assert.Less(w.magnitude, ValleyShape.PlayableRadius - 6f, $"{local} cae fuera del area jugable");
                Assert.Greater(Vector2.Distance(w, lab), 12f, $"{local} esta pegado al laboratorio");
            }
        }

        [Test]
        public void TrainingGround_IsFlat_AndFieldCenterDidNotMove()
        {
            Vector2 o = ValleyShape.TrainingOrigin;
            Assert.AreEqual(0f, ValleyShape.Height(o.x, o.y), 0.001f);
            for (int a = 0; a < 360; a += 45)
            {
                Vector2 p = o + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * 5f;
                Assert.AreEqual(0f, ValleyShape.Height(p.x, p.y), 0.001f, "plano hasta 5,6 m");
            }
            Assert.AreEqual(new Vector2(28f, -6f), ValleyShape.FieldCenter);
            Assert.AreEqual(0f, ValleyShape.Height(28f, -6f), 0.05f, "el campo viejo sigue plano");
        }
    }
}
