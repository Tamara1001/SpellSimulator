using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Casting;
using SpellSimulator.EditorTools.Map;
using SpellSimulator.World.Map;

namespace SpellSimulator.Tests
{
    /// <summary>Mapa de la torre (Docs/LABORATORIO_V3_PLAN.md (e)): proyección, catálogo de zonas y portales, marcadores, leyenda y pantalla completa.</summary>
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
            Assert.IsTrue(ids.Contains("cabin-north") && ids.Contains("supply-stall"));
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

        // ---------------------------------------------------------------- portales

        [Test]
        public void Portales_EstanEnPortalsJsonYDentroDelCuadro()
        {
            WorldZoneCatalog.Zone[] portals = MapTowerMap.LoadResolvedPortals();
            Assert.GreaterOrEqual(portals.Length, 4, "faltan los portales del mapa");
            Assert.IsTrue(File.Exists(PortalRecipes.AssetPath), "falta Data/Portals/portals.json");
            PortalRecipes recipes = PortalRecipes.Load(File.ReadAllText(PortalRecipes.AssetPath));
            var ids = new HashSet<string>();
            foreach (WorldZoneCatalog.Zone portal in portals)
            {
                Assert.IsTrue(ids.Add(portal.id), $"portal repetido: {portal.id}");
                Assert.IsNotNull(recipes.Find(portal.id), $"{portal.id}: no es un portal de portals.json");
                Assert.IsFalse(string.IsNullOrEmpty(portal.name), $"{portal.id}: sin nombre");
                Assert.IsTrue(ColorUtility.TryParseHtmlString(portal.color, out _), $"{portal.id}: color inválido {portal.color}");
                Vector2 t = MapProjection.PortalToTable(portal.x, portal.z);
                Assert.IsTrue(MapProjection.InsideTable(t, MapProjection.PortalRadius), $"el rombo de {portal.id} cae fuera del cuadro");
            }
        }

        [Test]
        public void Portales_RombosNoTapanMarcadoresNiOtrosRombos()
        {
            WorldZoneCatalog.Zone[] zones = MapTowerMap.LoadResolvedZones();
            WorldZoneCatalog.Zone[] portals = MapTowerMap.LoadResolvedPortals();
            for (int i = 0; i < portals.Length; i++)
            {
                Vector2 p = MapProjection.PortalToTable(portals[i].x, portals[i].z);
                foreach (WorldZoneCatalog.Zone zone in zones)
                    Assert.GreaterOrEqual(Vector2.Distance(p, MapProjection.WorldToTable(zone.x, zone.z)),
                        MapProjection.MarkerRadius + MapProjection.PortalRadius, $"el rombo de {portals[i].id} tapa el marcador de {zone.id}");
                for (int j = i + 1; j < portals.Length; j++)
                    Assert.GreaterOrEqual(Vector2.Distance(p, MapProjection.PortalToTable(portals[j].x, portals[j].z)),
                        MapProjection.PortalRadius * 2f, $"{portals[i].id} y {portals[j].id} se superponen");
            }
        }

        [Test]
        public void SetupPortals_UnRomboPorPortal_SinTocarLosMarcadores()
        {
            WorldZoneCatalog.Zone[] zones = MapTowerMap.LoadResolvedZones();
            WorldZoneCatalog.Zone[] portals = MapTowerMap.LoadResolvedPortals();
            var go = new GameObject("TowerMapPortalsTest");
            try
            {
                var map = go.AddComponent<TowerMap>();
                map.Setup(zones, null, null);
                map.SetupPortals(portals, null, null);
                map.SetupPortals(portals, null, null); // rehacerlo no duplica
                Assert.AreEqual(portals.Length, map.PortalMarkerCount);
                Assert.AreEqual(zones.Length, map.MarkerCount);
                Assert.AreEqual(zones.Length + portals.Length, go.GetComponentsInChildren<MapZoneMarker>().Length);
                for (int i = 0; i < portals.Length; i++)
                {
                    MapZoneMarker marker = map.GetPortalMarker(i);
                    Assert.IsTrue(marker.IsPortal);
                    Assert.AreEqual(i, marker.Index);
                    Assert.AreSame(map, marker.Owner);
                }
                Assert.IsFalse(map.GetMarker(0).IsPortal);
                // Presionar un rombo no marca ninguna zona (viaja; sin portales en la escena solo avisa).
                map.GetPortalMarker(0).Press();
                Assert.AreEqual(-1, map.Selected);
                Assert.IsFalse(map.IsFullscreen);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---------------------------------------------------------------- leyenda y pantalla completa

        [Test]
        public void Leyenda_EntranTodasLasFilasDentroDelPergamino()
        {
            int zones = MapTowerMap.LoadResolvedZones().Length, portals = MapTowerMap.LoadResolvedPortals().Length;
            int rows = MapProjection.LegendRows(zones, portals);
            Assert.AreEqual(zones + portals + 1, rows, "una fila por zona, el título «Portales» y una por portal");
            float rowH = MapProjection.LegendRowHeight(rows);
            Assert.Greater(rowH, 0.03f, "las filas quedaron demasiado bajas para leerse");
            float inner = MapProjection.BoardHeight * 0.5f - MapProjection.FrameBar;
            Assert.Less(MapProjection.LegendTop, inner);
            Assert.GreaterOrEqual(MapProjection.LegendBottom, -inner);
            int last = MapProjection.LegendRowOf(zones + portals - 1, zones);
            Assert.AreEqual(rows - 1, last);
            Assert.GreaterOrEqual(MapProjection.LegendRowCenterY(last, rowH) - rowH * 0.5f, MapProjection.LegendBottom - 1e-4f);
            // La primera fila de portales va después del título.
            Assert.AreEqual(zones + 1, MapProjection.LegendRowOf(zones, zones));
            // Chapa y nombre dentro de la columna, a la derecha de la divisoria del mapa.
            float columnLeft = MapProjection.ColumnX - MapProjection.ColumnWidth * 0.5f;
            Assert.Greater(MapProjection.LegendChipX - MapProjection.LegendChipRadius, columnLeft);
            Assert.Greater(columnLeft, MapProjection.TableCenter.x + MapProjection.TableSide * 0.5f);
            Assert.Less(MapProjection.LegendNameLeft, MapProjection.ColumnX + MapProjection.ColumnWidth * 0.5f);
        }

        [Test]
        public void Leyenda_TextoNuncaCoplanar()
        {
            // Nombre sobre la barra y número sobre la chapa: ≥ 6 mm de lo que tienen debajo; la chapa sobresale de la barra.
            Assert.GreaterOrEqual(MapProjection.TextLift, 0.006f);
            Assert.Greater(MapProjection.LegendChipDepth, MapProjection.LegendBarDepth);
            Assert.Greater(MapProjection.LegendBarDepth, 0f);
        }

        [Test]
        public void FitScale_LlenaLaVistaSinSalirse()
        {
            var cases = new[]
            {
                (MapProjection.FullscreenDistancePc, 60f, 16f / 9f),
                (MapProjection.FullscreenDistancePc, 60f, 4f / 3f),
                (MapProjection.FullscreenDistanceVr, MapProjection.FullscreenFovVr, MapProjection.FullscreenAspectVr),
            };
            foreach (var (distance, fov, aspect) in cases)
            {
                float s = MapProjection.FitScale(distance, fov, aspect);
                float viewH = 2f * distance * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad), viewW = viewH * aspect;
                float h = MapProjection.BoardHeight * s, w = MapProjection.BoardWidth * s;
                float fillH = viewH * MapProjection.FullscreenFill, fillW = viewW * MapProjection.FullscreenFill;
                Assert.LessOrEqual(h, fillH + 1e-4f, "se sale a lo alto");
                Assert.LessOrEqual(w, fillW + 1e-4f, "se sale a lo ancho");
                Assert.IsTrue(Mathf.Abs(h - fillH) < 1e-4f || Mathf.Abs(w - fillW) < 1e-4f, "no llena la vista en ninguna dirección");
            }
            // PC con 60° y 16:9 a 0,5 m: el cuadro se achica (≈ 0,47) para quedar cerca y no atravesar paredes.
            Assert.Less(MapProjection.FitScale(MapProjection.FullscreenDistancePc, 60f, 16f / 9f), 1f);
        }

        private static void AssertNear(Vector2 expected, Vector2 actual, float eps = Eps)
        {
            Assert.AreEqual(expected.x, actual.x, eps, $"x: esperado {expected}, real {actual}");
            Assert.AreEqual(expected.y, actual.y, eps, $"y: esperado {expected}, real {actual}");
        }
    }
}
