using NUnit.Framework;
using UnityEngine;
using SpellSimulator.EditorTools.Map;
using SpellSimulator.World.Map;

namespace SpellSimulator.Tests
{
    /// <summary>
    /// Fija los sitios de las estructuras (Logs/MapBaseline.json) antes y despues de mudar el entrenamiento: aplanar el terreno
    /// nuevo (ValleyShape.Height) no puede mover ninguna estructura ni el frente del laboratorio (riesgo 1 del plan v3).
    /// La cabaña 1 y el puesto de suministros se mudaron al norte para dejarle lugar al entrenamiento (pedido de Tamara):
    /// sus valores son los sitios preferidos (MapStructures.CabinNorthPreferred / SupplyStallPreferred); si Find los corre
    /// por el desnivel del terreno, actualizar acá con lo que reporta «[Estructuras] posiciones».
    /// </summary>
    public class StructureSitesPinTests
    {
        private const float Tolerance = 0.06f;

        private static readonly (string name, float x, float z)[] Pinned =
        {
            ("Watchtower", 37.282f, 44.431f),
            ("Cave", -32.325f, 51.731f),
            ("Cabin_1", -8f, 46f),        // mudada al norte (era la cabaña del sur, en (12, -40))
            ("Cabin_2", -28f, 34f),
            ("StoneCircle", 50f, -10f),
            ("Shrine", 34f, -36f),
            ("SupplyStall", 10f, 48f),    // mudado al norte (estaba en (8, -22))
            ("Crag_1", 20f, -28f),
            ("Crag_2", 44f, 10f),
            ("Crag_3", -6f, -40f),
            ("Crag_4", 23.5f, 30f),
            ("EarthWorkshop", 40f, 30f),
            ("WaterLab", -44.189f, 1.338f),
            ("Laboratory", 47f, -27f),
        };

        [Test]
        public void ExistingSites_StayWhereTheyWere()
        {
            foreach (var (name, x, z) in Pinned)
            {
                Assert.IsTrue(MapStructures.TryGetSite(name, out Vector2 pos, out _), $"falta el sitio {name}");
                Assert.AreEqual(x, pos.x, Tolerance, $"{name}.x");
                Assert.AreEqual(z, pos.y, Tolerance, $"{name}.z");
            }
        }

        [Test]
        public void MovedSites_AreNorthOfTheTowerAndClearOfTheirNeighbours()
        {
            foreach (string name in new[] { "Cabin_1", "SupplyStall" })
            {
                Assert.IsTrue(MapStructures.TryGetSite(name, out Vector2 pos, out _), $"falta el sitio {name}");
                Assert.Greater(pos.y, ValleyShape.TowerCenter.y + ValleyShape.HillRadius, $"{name} no quedo al norte de la loma de la torre");
                Assert.Greater(Vector2.Distance(pos, ValleyShape.TowerCenter), ValleyShape.HillRadius + 4f, $"{name} pisa la loma de la torre");
                foreach (string other in new[] { "Cave", "Watchtower", "Cabin_2", "EarthWorkshop", "Crag_4" })
                {
                    MapStructures.TryGetSite(other, out Vector2 o, out _);
                    Assert.Greater(Vector2.Distance(pos, o), 9f, $"{name} esta pegado a {other}");
                }
            }
        }

        [Test]
        public void LaboratoryFront_IsUnchanged()
        {
            Assert.IsTrue(MapStructures.TryGetSite("Laboratory", out _, out Vector2 front));
            Assert.AreEqual(-0.966f, front.x, 0.01f);
            Assert.AreEqual(0.259f, front.y, 0.01f);
        }

        [Test]
        public void TrainingOrigin_IsClearOfEveryStructureAndTheLabTrack()
        {
            foreach (var (name, x, z) in Pinned)
                Assert.Greater(Vector2.Distance(ValleyShape.TrainingOrigin, new Vector2(x, z)), 11f, $"el entrenamiento esta pegado a {name}");
            // Entrenamiento y laboratorio a mas de 12 m.
            MapStructures.TryGetSite("Laboratory", out Vector2 labPos, out _);
            Assert.Greater(Vector2.Distance(ValleyShape.TrainingOrigin, labPos), 12f);
            // Final de la pista del laboratorio: pos + frente * (inicio + largo).
            MapStructures.TryGetSite("Laboratory", out Vector2 lab, out Vector2 front);
            Vector2 trackEnd = lab + front * (MapStructures.LaboratoryTrackStart + MapStructures.LaboratoryTrackLength);
            Assert.GreaterOrEqual(Vector2.Distance(ValleyShape.TrainingOrigin, trackEnd), MapStructures.TrainingKeepOut);
        }
    }
}
