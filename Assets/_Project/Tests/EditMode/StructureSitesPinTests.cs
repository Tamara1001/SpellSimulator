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
            ("Shrine", 44f, -38f),      // mudado al este para dejarle lugar al campo de viento (paso 6)
            ("SupplyStall", 10f, 48f),    // mudado al norte (estaba en (8, -22))
            ("Crag_1", 28f, 17f),       // mudado al noreste de la torre: a 8,5 m del campo de viento no cabia
            ("Crag_2", 44f, 10f),
            ("Crag_3", 47.750f, -25.701f), // corrido ~1 m por el santuario; mudado al sitio viejo del laboratorio (antes (-6, -40), sobre el Archivo)
            ("Crag_4", 22.750f, 31.299f), // corrido ~1,5 m por el penasco 1
            ("EarthWorkshop", 40f, 30f),
            ("WaterLab", -44.189f, 1.338f),
            ("Laboratory", 20f, -8f),
            ("FireField", -18f, -50f),    // campos de elemento (paso 6): fijos, el terreno se aplana en ValleyShape.Height
            ("WindField", 26f, -34f),
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
        public void ElementFields_AreFlatAndDoNotOverlapOtherSites()
        {
            foreach (string field in new[] { "FireField", "WindField" })
            {
                Assert.IsTrue(MapStructures.TryGetSite(field, out Vector2 center, out _), $"falta el sitio {field}");
                // Plano hasta el 70 % del radio (como el entrenamiento): sin escalones para los blancos ni la mesa.
                for (float a = 0f; a < 360f; a += 45f)
                    foreach (float r in new[] { 0f, 3f, ValleyShape.ElementFieldRadius * 0.7f - 0.1f })
                    {
                        Vector2 p = center + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * r;
                        Assert.AreEqual(0f, ValleyShape.Height(p.x, p.y), 0.02f, $"{field}: el terreno no es llano en {p}");
                    }
                Assert.Less(center.magnitude + ValleyShape.ElementFieldRadius, 63f, $"{field} se sale del area jugable");
                foreach (var (name, x, z) in Pinned)
                {
                    if (name == field) continue;
                    float min = ValleyShape.ElementFieldRadius + (name.StartsWith("Crag") ? 6f : 3.8f);
                    Assert.Greater(Vector2.Distance(center, new Vector2(x, z)), min, $"{field} pisa {name}");
                }
            }
        }

        [Test]
        public void Laboratory_IsInTheMeadowNearTheNexusWithTheTrackToTheEast()
        {
            Assert.IsTrue(MapStructures.TryGetSite("Laboratory", out Vector2 lab, out Vector2 front));
            Assert.AreEqual(1f, front.x, 0.01f);
            Assert.AreEqual(0f, front.y, 0.01f);
            Assert.LessOrEqual(Vector2.Distance(lab, ValleyShape.Spawn), 22f, "el laboratorio tiene que quedar a mano del nexo");
            // La pista (12 x 10 m delante de la plataforma) no pisa caminos, otras estructuras ni sale del area jugable.
            Vector2 side = new Vector2(front.y, -front.x);
            for (float d = MapStructures.LaboratoryTrackStart; d <= MapStructures.LaboratoryTrackStart + MapStructures.LaboratoryTrackLength; d += 2f)
                for (float s = -0.5f; s <= 0.51f; s += 0.25f)
                {
                    Vector2 p = lab + front * d + side * (s * MapStructures.LaboratoryTrackWidth);
                    Assert.Less(p.magnitude, 63f, $"la pista sale del area jugable en {p}");
                    Assert.Greater(ValleyShape.PathDistance(p.x, p.y), ValleyShape.PathWidth + 0.5f, $"la pista pisa un camino en {p}");
                    foreach (var (name, x, z) in Pinned)
                        if (name != "Laboratory")
                            Assert.Greater(Vector2.Distance(p, new Vector2(x, z)), 7f, $"la pista pisa {name} en {p}");
                }
            // El camino del nexo llega hasta el borde de la plataforma sin meterse debajo.
            float toPath = ValleyShape.PathDistance(lab.x, lab.y);
            Assert.Greater(toPath, MapStructures.LaboratoryPlatformRadius);
            Assert.Less(toPath, MapStructures.LaboratoryPlatformRadius + 2f);
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
