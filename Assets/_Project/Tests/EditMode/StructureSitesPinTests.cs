using NUnit.Framework;
using UnityEngine;
using SpellSimulator.EditorTools.Map;
using SpellSimulator.World.Map;

namespace SpellSimulator.Tests
{
    /// <summary>
    /// Fija los sitios de las estructuras (Logs/MapBaseline.json) antes y despues de mudar el entrenamiento: aplanar el terreno
    /// nuevo (ValleyShape.Height) no puede mover ninguna estructura ni el frente del laboratorio (riesgo 1 del plan v3).
    /// </summary>
    public class StructureSitesPinTests
    {
        private const float Tolerance = 0.06f;

        private static readonly (string name, float x, float z)[] Pinned =
        {
            ("Watchtower", 37.282f, 44.431f),
            ("Cave", -32.325f, 51.731f),
            ("Cabin_1", 12f, -40f),
            ("Cabin_2", -28f, 34f),
            ("StoneCircle", 50f, -10f),
            ("Shrine", 34f, -36f),
            ("SupplyStall", 8f, -22f),
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
            // Final de la pista del laboratorio: pos + frente * (inicio + largo).
            MapStructures.TryGetSite("Laboratory", out Vector2 lab, out Vector2 front);
            Vector2 trackEnd = lab + front * (MapStructures.LaboratoryTrackStart + MapStructures.LaboratoryTrackLength);
            Assert.GreaterOrEqual(Vector2.Distance(ValleyShape.TrainingOrigin, trackEnd), MapStructures.TrainingKeepOut);
        }
    }
}
