using NUnit.Framework;
using UnityEngine;
using SpellSimulator.EditorTools.Map;
using SpellSimulator.World.Map;

namespace SpellSimulator.Tests
{
    /// <summary>Reglas de la torre-tutorial (Docs/MAPA_FLUJO_PLAN.md, §17): niveles pasados, puerta y estado de la pasada.</summary>
    public class TutorialRulesTests
    {
        // Pivote del jugador de pie sobre un piso (entre 1 y 1,7 m segun el estado).
        private static Vector3 OnFloor(int floor, float lift = 1.4f) => new Vector3(1.5f, floor * TowerBuilder.FloorHeight + lift, -1.5f);
        private static Vector3 Doorway => new Vector3(0f, 1.4f, -(TowerBuilder.InnerRadius + 0.5f));

        [Test]
        public void Measures_MatchTheTower()
        {
            Assert.AreEqual(TowerBuilder.FloorHeight, TutorialRules.FloorHeight, 0.001f);
            Assert.AreEqual(TowerBuilder.InnerRadius, TutorialRules.InnerRadius, 0.001f);
            Assert.AreEqual(TowerBuilder.OuterRadius, TutorialRules.OuterRadius, 0.001f);
            Assert.AreEqual(TowerBuilder.Floors + 1, TutorialRules.Levels, "un nivel por piso y la terraza");
        }

        [Test]
        public void PassedAt_CountsTheFloorsBelowTheTerrace()
        {
            foreach (float lift in new[] { 1f, 1.7f })
            {
                Assert.AreEqual(0, TutorialRules.PassedAt(OnFloor(3, lift)), "en la terraza no paso nada");
                Assert.AreEqual(1, TutorialRules.PassedAt(OnFloor(2, lift)));
                Assert.AreEqual(2, TutorialRules.PassedAt(OnFloor(1, lift)));
                Assert.AreEqual(3, TutorialRules.PassedAt(OnFloor(0, lift)));
            }
            // Un salto en la terraza o en un piso no cambia de nivel.
            Assert.AreEqual(1, TutorialRules.PassedAt(OnFloor(2, 1.7f) + Vector3.up * 0.85f));
            Assert.AreEqual(4, TutorialRules.PassedAt(Doorway));
        }

        [Test]
        public void Doorway_IsOnlyTheSouthOpeningOnTheGroundFloor()
        {
            Assert.IsTrue(TutorialRules.InDoorway(Doorway));
            Assert.IsFalse(TutorialRules.InDoorway(new Vector3(0f, 1.4f, -(TowerBuilder.InnerRadius - 0.3f))), "todavia adentro");
            Assert.IsFalse(TutorialRules.InDoorway(new Vector3(0f, 1.4f, TowerBuilder.InnerRadius + 0.5f)), "lado norte");
            Assert.IsFalse(TutorialRules.InDoorway(Doorway + Vector3.up * TowerBuilder.FloorHeight), "un piso mas arriba");
            Assert.Less(TowerBuilder.InnerRadius + 0.5f, TutorialRules.BarrierRadius - 0.5f, "el vano tiene que quedar antes de la barrera");
            Assert.Greater(TutorialRules.BarrierRadius, TowerBuilder.TerraceOuter + 0.3f, "la barrera va por fuera del balcon");
            Assert.Greater(TutorialRules.BarrierRadius, TowerBuilder.BaseRadius + 0.3f, "y por fuera del talud");
        }

        [Test]
        public void Progress_AdvancesOnlyWhileActive_AndEndsAtTheDoor()
        {
            var p = new TutorialProgress();
            Assert.AreEqual(0, p.Advance(OnFloor(0)), "sin empezar no cuenta");
            p.Begin();
            Assert.IsTrue(p.Active);
            Assert.AreEqual(0, p.Advance(OnFloor(3)));
            Assert.AreEqual(1, p.Advance(OnFloor(2)));
            Assert.AreEqual(0, p.Advance(OnFloor(3)), "volver a subir no descuenta");
            Assert.AreEqual(1, p.Passed);
            Assert.AreEqual(2, p.Advance(OnFloor(0)), "bajar dos pisos de golpe cuenta los dos");
            Assert.IsFalse(p.Completed);
            Assert.AreEqual(1, p.Advance(Doorway));
            Assert.IsTrue(p.Completed);
            Assert.IsFalse(p.Active, "al salir se apaga la barrera");
        }

        [Test]
        public void Progress_SkipAndRestart()
        {
            var p = new TutorialProgress();
            p.Begin();
            p.Advance(OnFloor(1));
            p.Skip();
            Assert.IsFalse(p.Active);
            Assert.AreEqual(0, p.Advance(Doorway));
            p.Begin();
            Assert.IsTrue(p.Active);
            Assert.AreEqual(0, p.Passed, "«Hacer el tutorial» otra vez lo rearma");
        }

        [Test]
        public void TerraceObstacles_CanBeJumpedCrouchedOrWalkedAround()
        {
            // Salto: v = 5 m/s y g = 15 m/s2 (PlayerMovement) dan 0,83 m. Agachado mide 1,1 m; de pie, 2,1 m.
            Assert.Less(MapTutorial.LowWallHeight, 0.75f);
            Assert.Greater(MapTutorial.BeamClearance, 1.2f);
            Assert.Less(MapTutorial.BeamClearance, 1.9f);
            // Los dos caen sobre piso macizo de la terraza (el hueco de la rampa va de 312 a 150 grados).
            foreach (float angle in new[] { MapTutorial.LowWallAngle, MapTutorial.BeamAngle })
                Assert.That(angle, Is.InRange(155f, 305f));
        }
    }
}
