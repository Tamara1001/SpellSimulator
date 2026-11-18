using NUnit.Framework;
using UnityEngine;
using SpellSimulator.EditorTools.Map;
using SpellSimulator.World.Map;

namespace SpellSimulator.Tests
{
    /// <summary>Donde aparece el jugador: terraza (tutorial), nexo (salteo y reaparicion). Docs/MAPA_FLUJO_PLAN.md, D19-D23.</summary>
    public class SpawnRulesTests
    {
        [Test]
        public void TerraceHeight_MatchesTheTower()
        {
            Assert.AreEqual(TowerBuilder.TerraceY, SpawnRules.TerraceHeight, 0.001f);
        }

        [Test]
        public void Tutorial_StartsOnTheNorthBalconyLookingEastSoutheast()
        {
            Vector3 p = SpawnRules.Start(false, out float yaw);
            Vector2 tower = ValleyShape.TowerCenter;
            Assert.Less(Vector2.Distance(new Vector2(p.x, p.z), tower), TowerBuilder.TerraceRadius - 0.6f, "tiene que caer dentro de la terraza");
            Assert.Greater(p.y, ValleyShape.Height(tower.x, tower.y) + TowerBuilder.TerraceY);
            Assert.AreEqual(115f, yaw, 0.01f);
            // Balcon norte (r = 6,8 m): fuera del hueco de la rampa (que llega hasta InnerRadius) y dentro del parapeto.
            Assert.Greater(Vector2.Distance(new Vector2(p.x, p.z), tower), TowerBuilder.InnerRadius, "el balcon queda por fuera del muro");
            Assert.Greater(p.z, tower.y, "al norte de la torre");
        }

        [Test]
        public void SkippedTutorial_StartsAtTheNexus()
        {
            Vector3 p = SpawnRules.Start(true, out _);
            Assert.AreEqual(ValleyShape.Spawn.x, p.x, 0.001f);
            Assert.AreEqual(ValleyShape.Spawn.y, p.z, 0.001f);
            Assert.AreEqual(SpawnRules.Nexus, p);
            Assert.Greater(p.y, ValleyShape.Height(p.x, p.z));
        }

        [Test]
        public void FellOut_OnlyBelowTheWorld()
        {
            Assert.IsFalse(SpawnRules.FellOut(-2f), "el fondo del lago no es una caida");
            Assert.IsFalse(SpawnRules.FellOut(SpawnRules.Nexus.y));
            Assert.IsTrue(SpawnRules.FellOut(SpawnRules.FallHeight - 0.1f));
        }

        [Test]
        public void MapBoard_IsNextToTheNexusAndOffThePaths()
        {
            Vector2 board = SpawnRules.MapBoard;
            float d = Vector2.Distance(board, ValleyShape.Spawn);
            Assert.That(d, Is.InRange(3f, 7f), "el tablero se tiene que leer desde el nexo");
            Assert.Greater(ValleyShape.PathDistance(board.x, board.y), ValleyShape.PathWidth * 0.5f + 0.9f, "el tablero pisa un camino");
            Assert.IsFalse(ValleyShape.CanPlantTree(board.x, board.y), "no puede crecer un arbol sobre el tablero");
        }
    }
}
