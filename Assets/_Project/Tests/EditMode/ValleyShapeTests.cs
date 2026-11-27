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
        public void Paths_StayOnDryPlayableGroundAndOffTheTraining()
        {
            Vector2[] props = { TrainingLayout.TestProps, TrainingLayout.DummyA, TrainingLayout.DummyB, TrainingLayout.ToolTable, TrainingLayout.StampTable,
                TrainingLayout.Contraptions, TrainingLayout.GiantStand, TrainingLayout.GiantPaper, TrainingLayout.Easel, Vector2.zero };
            foreach (Vector2[] path in ValleyShape.Paths)
                for (int i = 0; i + 1 < path.Length; i++)
                    for (float t = 0f; t <= 1f; t += 0.1f)
                    {
                        Vector2 p = Vector2.Lerp(path[i], path[i + 1], t);
                        Assert.Less(p.magnitude, 61f, $"el camino sale del area jugable en {p}");
                        // 1 = borde del agua: el camino del nexo (el de siempre) termina mojandose los pies en la orilla (0,83 en su ultimo punto); ninguno entra mas.
                        Assert.Greater(ValleyShape.LakeFactor(p.x, p.y), 0.8f, $"el camino se mete en el lago en {p}");
                        Assert.Greater(ValleyShape.RiverDistance(p.x, p.y), ValleyShape.RiverHalfWidth + 1f, $"el camino cruza el arroyo en {p}");
                        foreach (Vector2 local in props)
                            Assert.Greater(Vector2.Distance(p, TrainingLayout.ToWorld(local)), ValleyShape.PathWidth * 0.5f + 1.5f,
                                $"el camino pisa el entrenamiento ({local}) en {p}");
                    }
        }

        [Test]
        public void Paths_LinkTheLearningRoute()
        {
            // Nexo -> entrenamiento (a la vista del camino) -> Archivo, y una rama al borde de cada campo.
            Assert.Less(ValleyShape.PathDistance(ValleyShape.Spawn.x, ValleyShape.Spawn.y), 0.01f);
            Assert.Less(ValleyShape.PathDistance(ValleyShape.TrainingOrigin.x, ValleyShape.TrainingOrigin.y), 13f);
            (string name, Vector2 center, float radius)[] zones =
            {
                ("Archivo", new Vector2(0f, -46f), 9f), ("agua", new Vector2(-22f, -30f), 5f), ("fuego", new Vector2(-18f, -50f), 8f),
                ("tierra", new Vector2(20f, -50f), 6f), ("viento", new Vector2(26f, -34f), 8f),
            };
            foreach (var (name, center, radius) in zones)
            {
                float d = ValleyShape.PathDistance(center.x, center.y);
                Assert.Greater(d, radius - 1.5f, $"un camino atraviesa la zona de {name}");
                Assert.Less(d, radius + 4.5f, $"ningun camino llega al borde de {name}");
            }
        }

        [Test]
        public void Paths_CircuitReachesTheLateGameWithoutCrossingIt()
        {
            // Paso 11b (D26): el circuito toca el borde de cada sitio del late game y no pasa por encima de ninguno
            // (MapStructures.Clear exige camino a mas de radio + 3,2 m del centro). Posiciones y radios de MapStructures.
            (string name, Vector2 center, float radius)[] sites =
            {
                ("santuario", new Vector2(44f, -38f), 3.8f), ("circulo", new Vector2(50f, -10f), 5.8f),
                ("atalaya", new Vector2(37.28f, 44.43f), 4.5f), ("puesto", new Vector2(10f, 48f), 2.8f),
                ("cabaña norte", new Vector2(-8f, 46f), 3.6f), ("cueva", new Vector2(-32.33f, 51.73f), 6.5f),
                ("cabaña del bosque", new Vector2(-28f, 34f), 3.6f), ("peñasco 2", new Vector2(44f, 10f), 6f),
                ("peñasco 3", new Vector2(47.75f, -25.7f), 6f),
            };
            foreach (var (name, center, radius) in sites)
            {
                float d = ValleyShape.PathDistance(center.x, center.y);
                Assert.GreaterOrEqual(d, radius + ValleyShape.PathWidth + 1f, $"un camino pisa {name}");
                if (!name.StartsWith("peñasco")) Assert.Less(d, radius + 8f, $"el circuito no llega a {name}");
            }
        }

        [Test]
        public void SpawnAndFieldAreClearOfTrees()
        {
            Assert.IsFalse(ValleyShape.CanPlantTree(ValleyShape.Spawn.x, ValleyShape.Spawn.y));
            Assert.IsFalse(ValleyShape.CanPlantTree(ValleyShape.FieldCenter.x, ValleyShape.FieldCenter.y));
            Assert.IsFalse(ValleyShape.CanPlantTree(ValleyShape.TowerCenter.x, ValleyShape.TowerCenter.y));
            Assert.IsTrue(ValleyShape.CanPlantTree(38f, 28f)); // (40, 35) quedo junto al circuito exterior (paso 11b)
        }
    }
}
