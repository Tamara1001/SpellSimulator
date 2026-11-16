using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Casting;
using SpellSimulator.EditorTools.Map;
using SpellSimulator.World.Map;

namespace SpellSimulator.Tests
{
    /// <summary>Mapa de la torre (Docs/LABORATORIO_V3_PLAN.md (e)): proyección, catálogo de zonas y portales, marcadores, leyenda, sin viaje y vista completa (Tab).</summary>
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
                // Presionar un rombo solo resalta su fila de la leyenda (no es un botón de viaje); otra vez la desmarca.
                map.GetPortalMarker(0).Press();
                Assert.AreEqual(zones.Length, map.Selected);
                map.GetPortalMarker(0).Press();
                Assert.AreEqual(-1, map.Selected);
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

        // ---------------------------------------------------------------- sin viaje y vista completa (Tab)

        [Test]
        public void ElMapa_NoTeletransporta()
        {
            // El viaje salió del mapa: ni TowerMap ni el marcador tienen una acción de viaje, y presionar un rombo no mueve a nadie.
            Assert.IsNull(typeof(TowerMap).GetMethod("Travel"), "TowerMap no debe tener acción de viaje");
            Assert.IsNull(typeof(TowerMap).GetEvent("TraveledFromMap"), "TowerMap no debe avisar viajes");
            WorldZoneCatalog.Zone[] portals = MapTowerMap.LoadResolvedPortals();
            var go = new GameObject("TowerMapNoTravel");
            var witness = new GameObject("Witness");
            try
            {
                witness.transform.position = new Vector3(1f, 2f, 3f);
                var map = go.AddComponent<TowerMap>();
                map.Setup(MapTowerMap.LoadResolvedZones(), null, null);
                map.SetupPortals(portals, null, null);
                for (int i = 0; i < map.PortalMarkerCount; i++) map.GetPortalMarker(i).Press();
                Assert.AreEqual(new Vector3(1f, 2f, 3f), witness.transform.position);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(witness);
            }
        }

        [Test]
        public void ElMapa_EsUnPanelEnfocable_ConElCuadroEntero()
        {
            var go = new GameObject("TowerMapPanel");
            try
            {
                go.transform.SetPositionAndRotation(new Vector3(4f, 1.6f, 2f), Quaternion.LookRotation(Vector3.right)); // +Z hacia el muro
                var map = go.AddComponent<TowerMap>();
                Assert.IsInstanceOf<SpellSimulator.Interfaces.IFocusPanel>(map);
                Assert.IsTrue(map.TryGetFocusFrame(out Vector3 center, out Vector3 normal, out Vector2 size));
                AssertNear(new Vector2(MapProjection.BoardWidth, MapProjection.BoardHeight), size);
                Assert.AreEqual(-1f, normal.x, Eps, "la normal mira hacia la habitación (−Z del cuadro)");
                Assert.AreEqual(go.transform.position.x + MapProjection.FaceZ * go.transform.forward.x, center.x, Eps);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SurfaceFocus_FitDistance_EncuadraSinSalirse()
        {
            var cases = new[] { (60f, 16f / 9f), (60f, 4f / 3f), (90f, 1f) };
            float fill = SpellSimulator.Player.SurfaceFocus.PanelFill;
            foreach (var (fov, aspect) in cases)
            {
                float d = SpellSimulator.Player.SurfaceFocus.FitDistance(MapProjection.BoardWidth, MapProjection.BoardHeight, fov, aspect, fill);
                float viewH = 2f * d * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad), viewW = viewH * aspect;
                Assert.LessOrEqual(MapProjection.BoardHeight, viewH * fill + 1e-4f, "se sale a lo alto");
                Assert.LessOrEqual(MapProjection.BoardWidth, viewW * fill + 1e-4f, "se sale a lo ancho");
                Assert.IsTrue(Mathf.Abs(MapProjection.BoardHeight - viewH * fill) < 1e-4f || Mathf.Abs(MapProjection.BoardWidth - viewW * fill) < 1e-4f,
                    "no llena la vista en ninguna dirección");
            }
            // Un cuadro más grande exige más distancia.
            Assert.Greater(SpellSimulator.Player.SurfaceFocus.FitDistance(3f, 2f, 60f, 1.78f), SpellSimulator.Player.SurfaceFocus.FitDistance(1.5f, 1f, 60f, 1.78f));
        }

        private static void AssertNear(Vector2 expected, Vector2 actual, float eps = Eps)
        {
            Assert.AreEqual(expected.x, actual.x, eps, $"x: esperado {expected}, real {actual}");
            Assert.AreEqual(expected.y, actual.y, eps, $"y: esperado {expected}, real {actual}");
        }
    }
}
