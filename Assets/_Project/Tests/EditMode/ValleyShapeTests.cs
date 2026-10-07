using NUnit.Framework;
using UnityEngine;
using SpellSimulator.World.Map;

namespace SpellSimulator.Tests
{
    /// <summary>Forma del mapa: lugares clave donde tienen que estar y caminables.</summary>
    public class ValleyShapeTests
    {
        [Test]
        public void TowerSitsOnAFlatPlateau()
        {
            var c = ValleyShape.TowerCenter;
            float h = ValleyShape.Height(c.x, c.y);
            Assert.AreEqual(ValleyShape.HillHeight, h, 0.05f);
            // Toda la base de la torre (radio 7) queda plana.
            for (int a = 0; a < 360; a += 30)
            {
                Vector2 p = c + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * 7f;
                Assert.AreEqual(h, ValleyShape.Height(p.x, p.y), 0.1f);
            }
        }

        [Test]
        public void LakeBottomIsUnderWater_ShoreIsAbove()
        {
            var c = ValleyShape.LakeCenter;
            Assert.Less(ValleyShape.Height(c.x, c.y), ValleyShape.WaterLevel - 1f);
            Vector2 shore = c + new Vector2(ValleyShape.LakeRadii.x * 1.4f, 0f);
            Assert.Greater(ValleyShape.Height(shore.x, shore.y), ValleyShape.WaterLevel);
        }

        [Test]
        public void RiverBedIsUnderWater_WaterfallComesFromTheMountain()
        {
            Vector2 mid = (ValleyShape.WaterfallBase + ValleyShape.RiverEnd) / 2f;
            Assert.Less(ValleyShape.Height(mid.x, mid.y), ValleyShape.WaterLevel);
            var top = ValleyShape.WaterfallTop;
            Assert.Greater(ValleyShape.Height(top.x, top.y), 15f, "La cascada nace alto en la montaña.");
        }

        [Test]
        public void MountainsCloseTheValley()
        {
            for (int a = 0; a < 360; a += 20)
            {
                Vector2 dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                Vector2 edge = dir * (ValleyShape.MountainEnd - 2f);
                Assert.Greater(ValleyShape.Height(edge.x, edge.y), 25f, $"angulo {a}");
            }
        }

        [Test]
        public void PlayableGround_IsWalkable()
        {
            // Pendiente maxima caminable (~35 grados) en el area jugable, salvo el lago.
            const float step = 1f;
            for (float x = -60f; x <= 60f; x += 6f)
            for (float z = -60f; z <= 60f; z += 6f)
            {
                if (new Vector2(x, z).magnitude > ValleyShape.PlayableRadius - 8f) continue;
                if (ValleyShape.LakeFactor(x, z) < 1.2f) continue;
                if (ValleyShape.RiverDistance(x, z) < ValleyShape.RiverHalfWidth + 5f) continue;
                float dx = ValleyShape.Height(x + step, z) - ValleyShape.Height(x - step, z);
                float dz = ValleyShape.Height(x, z + step) - ValleyShape.Height(x, z - step);
                float slope = Mathf.Sqrt(dx * dx + dz * dz) / (2f * step);
                Assert.Less(slope, 0.7f, $"({x}, {z})");
            }
        }

        [Test]
        public void SpawnAndFieldAreClearOfTrees()
        {
            Assert.IsFalse(ValleyShape.CanPlantTree(ValleyShape.Spawn.x, ValleyShape.Spawn.y));
            Assert.IsFalse(ValleyShape.CanPlantTree(ValleyShape.FieldCenter.x, ValleyShape.FieldCenter.y));
            Assert.IsFalse(ValleyShape.CanPlantTree(ValleyShape.TowerCenter.x, ValleyShape.TowerCenter.y));
            Assert.IsTrue(ValleyShape.CanPlantTree(40f, 35f));
        }
    }
}
