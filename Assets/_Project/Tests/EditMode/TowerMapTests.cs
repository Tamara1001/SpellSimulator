using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using SpellSimulator.EditorTools.Map;
using SpellSimulator.World.Map;

namespace SpellSimulator.Tests
{
    /// <summary>Mapa de la torre (Docs/LABORATORIO_V3_PLAN.md (e)): proyección, catálogo de zonas y marcadores.</summary>
    public class TowerMapTests
    {
        private const float Eps = 1e-5f;

        [Test]
        public void WorldToTable_CentroCaeEnElCentroDelMapa()
        {
            Vector2 c = MapProjection.WorldToTable(MapProjection.WorldCenter.x, MapProjection.WorldCenter.y);
            Assert.AreEqual(MapProjection.TableCenter.x, c.x, Eps);
            Assert.AreEqual(MapProjection.TableCenter.y, c.y, Eps);
        }

        [Test]
        public void WorldToTable_EsquinasDelValleCaenEnLasEsquinasDelMapa()
        {
            float h = MapProjection.WorldSpan * 0.5f, half = MapProjection.TableSide * 0.5f;
            Vector2 w = MapProjection.WorldCenter;
            // Sudoeste abajo a la izquierda, noreste arriba a la derecha (norte arriba, este a la derecha).
            AssertNear(MapProjection.TableCenter + new Vector2(-half, -half), MapProjection.WorldToTable(w.x - h, w.y - h));
            AssertNear(MapProjection.TableCenter + new Vector2(half, half), MapProjection.WorldToTable(w.x + h, w.y + h));
            AssertNear(MapProjection.TableCenter + new Vector2(half, -half), MapProjection.WorldToTable(w.x + h, w.y - h));
            AssertNear(MapProjection.TableCenter + new Vector2(-half, half), MapProjection.WorldToTable(w.x - h, w.y + h));
            Assert.IsTrue(MapProjection.InsideTable(MapProjection.WorldToTable(w.x - h, w.y + h)));
            Assert.IsFalse(MapProjection.InsideTable(MapProjection.WorldToTable(w.x - h - 1f, w.y)));
        }

        [Test]
        public void WorldToTable_IdaYVuelta()
        {
            var p = new Vector2(23f, -45f);
            AssertNear(p, MapProjection.TableToWorld(MapProjection.WorldToTable(p.x, p.y)), 1e-3f);
            AssertNear(new Vector2(0.4f, -0.2f), MapProjection.WorldToPlane(new Vector2(14f, -2f), new Vector2(10f, 0f), 0.1f));
        }

        [Test]
        public void Catalogo_ZonasValidasYDentroDelCuadro()
        {
            Assert.IsTrue(File.Exists(WorldZoneCatalog.AssetPath), "falta Data/WorldZones.json");
            WorldZoneCatalog raw = WorldZoneCatalog.Parse(File.ReadAllText(WorldZoneCatalog.AssetPath));
            Assert.GreaterOrEqual(raw.zones.Length, 10, "el catálogo tiene muy pocas zonas");

            WorldZoneCatalog.Zone[] resolved = MapTowerMap.LoadResolvedZones();
            Assert.AreEqual(raw.zones.Length, resolved.Length);
            var ids = new HashSet<string>();
            foreach (WorldZoneCatalog.Zone zone in resolved)
            {
                Assert.IsFalse(string.IsNullOrEmpty(zone.id), "zona sin id");
                Assert.IsTrue(ids.Add(zone.id), $"id repetido: {zone.id}");
                Assert.IsFalse(string.IsNullOrEmpty(zone.name), $"{zone.id}: sin nombre");
                Assert.IsFalse(string.IsNullOrEmpty(zone.use), $"{zone.id}: sin uso");
                Assert.IsTrue(ColorUtility.TryParseHtmlString(zone.color, out _), $"{zone.id}: color inválido {zone.color}");
                Vector2 t = MapProjection.WorldToTable(zone.x, zone.z);
                Assert.IsTrue(MapProjection.InsideTable(t, MapProjection.MarkerRadius), $"{zone.id} ({zone.x:F1}, {zone.z:F1}) cae fuera del cuadro");
            }
            Assert.IsTrue(ids.Contains("tower") && ids.Contains("training") && ids.Contains("laboratory"));
        }

        [Test]
        public void Catalogo_MarcadoresNoSeTapan()
        {
            WorldZoneCatalog.Zone[] zones = MapTowerMap.LoadResolvedZones();
            for (int i = 0; i < zones.Length; i++)
                for (int j = i + 1; j < zones.Length; j++)
                {
                    float d = Vector2.Distance(MapProjection.WorldToTable(zones[i].x, zones[i].z), MapProjection.WorldToTable(zones[j].x, zones[j].z));
                    Assert.GreaterOrEqual(d, MapProjection.MarkerRadius * 2f, $"{zones[i].id} y {zones[j].id} se superponen");
                }
        }

        [Test]
        public void Setup_UnMarcadorPorZona()
        {
            WorldZoneCatalog.Zone[] zones = MapTowerMap.LoadResolvedZones();
            var go = new GameObject("TowerMapTest");
            try
            {
                var map = go.AddComponent<TowerMap>();
                map.Setup(zones, null, null);
                Assert.AreEqual(zones.Length, map.ZoneCount);
                Assert.AreEqual(zones.Length, map.MarkerCount);
                Assert.AreEqual(zones.Length, go.GetComponentsInChildren<MapZoneMarker>().Length);
                for (int i = 0; i < zones.Length; i++)
                {
                    MapZoneMarker marker = map.GetMarker(i);
                    Assert.AreEqual(i, marker.Index);
                    Assert.AreSame(map, marker.Owner);
                    Vector3 local = marker.transform.localPosition;
                    Assert.IsTrue(MapProjection.InsideTable(new Vector2(local.x, local.y), MapProjection.MarkerRadius));
                }

                // Volver a armarlo no duplica marcadores; presionar marca y desmarca.
                map.Setup(zones, null, null);
                Assert.AreEqual(zones.Length, go.GetComponentsInChildren<MapZoneMarker>().Length);
                map.GetMarker(0).Press();
                Assert.AreEqual(0, map.Selected);
                map.GetMarker(0).Press();
                Assert.AreEqual(-1, map.Selected);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void AssertNear(Vector2 expected, Vector2 actual, float eps = Eps)
        {
            Assert.AreEqual(expected.x, actual.x, eps, $"x: esperado {expected}, real {actual}");
            Assert.AreEqual(expected.y, actual.y, eps, $"y: esperado {expected}, real {actual}");
        }
    }
}
