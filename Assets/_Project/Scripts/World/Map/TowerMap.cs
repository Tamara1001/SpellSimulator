using System;
using TMPro;
using UnityEngine;
using SpellSimulator.Interfaces;
using SpellSimulator.Player;

namespace SpellSimulator.World.Map
{
    /// <summary>
    /// Mapa del valle colgado en la planta baja de la torre. Guarda las zonas y los portales del catálogo
    /// (Data/WorldZones.json, con la posición ya resuelta por el generador MapTowerMap), crea un <see cref="MapZoneMarker"/>
    /// por zona (círculo numerado) y por portal (rombo) y maneja la columna de texto: la entrada apuntada muestra su nombre y
    /// su uso; la presionada queda resaltada en la leyenda. Los rombos de portal son solo información: el mapa NO teletransporta
    /// (para viajar hay que cruzar el portal).
    ///
    /// Vista completa: es un <see cref="IFocusPanel"/>, igual que cualquier superficie que se mira. Apuntándolo en PC, Tab
    /// (<see cref="SurfaceFocus.ShowPanel"/>) encuadra el cuadro entero de frente y el cursor toca los marcadores; Tab, V o Esc
    /// cierran. En VR no hay vista completa: se apunta con el rayo directo en la pared.
    ///
    /// Sigue lo apuntado por el PlayerInteractor; los textos se arman una vez y en Update solo se asignan cuando cambia lo
    /// que se muestra (cero allocations por frame).
    /// </summary>
    public class TowerMap : MonoBehaviour, IFocusPanel
    {
        /// <summary>Cada cuánto se vuelve a buscar el interactor del jugador si todavía no existe (s).</summary>
        private const float InteractorRetrySeconds = 1f;

        private const string HintPc = "Apuntá un número o un rombo.\nClic: lo resalta en la lista.\nTab: vista completa (Tab o Esc cierra).\nLos rombos son portales: cruzalos para viajar.";
        private const string HintVr = "Apuntá un número o un rombo con el rayo.\nGatillo: lo resalta en la lista.\nLos rombos son portales: cruzalos para viajar.";

        [SerializeField, Tooltip("Zonas con la posición ya resuelta (las guarda MapTowerMap desde Data/WorldZones.json).")]
        private WorldZoneCatalog.Zone[] zones = Array.Empty<WorldZoneCatalog.Zone>();

        [SerializeField, Tooltip("Un marcador por zona, en el mismo orden.")]
        private MapZoneMarker[] markers = Array.Empty<MapZoneMarker>();

        [SerializeField, Tooltip("Portales con la posición de su estructura (Data/WorldZones.json, «portals»).")]
        private WorldZoneCatalog.Zone[] portals = Array.Empty<WorldZoneCatalog.Zone>();

        [SerializeField, Tooltip("Un rombo por portal, en el mismo orden.")]
        private MapZoneMarker[] portalMarkers = Array.Empty<MapZoneMarker>();

        [SerializeField, Tooltip("Texto con el nombre y el uso de la entrada apuntada.")]
        private TextMeshPro infoText;

        [SerializeField, Tooltip("Barra de resaltado de cada fila de la leyenda: zonas y después portales (sin el título).")]
        private GameObject[] legendBars = Array.Empty<GameObject>();

        private string[] infoLines = Array.Empty<string>();

        private PlayerInteractor interactor;
        private float nextInteractorSearch;
        private GameObject lastHoveredObject;
        private MapZoneMarker hovered;
        private int selected = -1;
        private int shownInfo = int.MinValue;
        private int shownBar = int.MinValue;
        private bool shownHintVr;

        public int ZoneCount => zones != null ? zones.Length : 0;
        public int MarkerCount => markers != null ? markers.Length : 0;
        public int PortalCount => portals != null ? portals.Length : 0;
        public int PortalMarkerCount => portalMarkers != null ? portalMarkers.Length : 0;

        /// <summary>Entrada marcada en la leyenda: una zona (0..ZoneCount−1) o un portal (ZoneCount + índice). −1 = ninguna.</summary>
        public int Selected => selected;

        public WorldZoneCatalog.Zone GetZone(int i) => zones[i];
        public MapZoneMarker GetMarker(int i) => markers[i];
        public WorldZoneCatalog.Zone GetPortal(int i) => portals[i];
        public MapZoneMarker GetPortalMarker(int i) => portalMarkers[i];

        /// <summary>
        /// Crea (o recrea) un marcador por zona sobre el pergamino, en <see cref="MapProjection.WorldToTable"/>. Lo llama el
        /// generador del Editor; <paramref name="markerMeshes"/> puede ser null (tests): el marcador queda sin malla.
        /// </summary>
        public void Setup(WorldZoneCatalog.Zone[] resolvedZones, Mesh[] markerMeshes, Material material)
        {
            DestroyMarkers(markers);
            zones = resolvedZones ?? Array.Empty<WorldZoneCatalog.Zone>();
            markers = new MapZoneMarker[zones.Length];
            for (int i = 0; i < zones.Length; i++)
            {
                WorldZoneCatalog.Zone zone = zones[i];
                Vector2 t = MapProjection.WorldToTable(zone.x, zone.z);
                markers[i] = CreateMarker($"Marker_{i + 1}_{zone.id}", t, MapProjection.MarkerRadius, i, zone.name, false,
                    markerMeshes != null && i < markerMeshes.Length ? markerMeshes[i] : null, material);
            }
            BuildTexts();
            ForceShow();
        }

        /// <summary>
        /// Crea (o recrea) un rombo por portal, en <see cref="MapProjection.PortalToTable"/> (corrido del marcador de la zona).
        /// <paramref name="symbolMeshes"/> puede ser null (tests).
        /// </summary>
        public void SetupPortals(WorldZoneCatalog.Zone[] resolvedPortals, Mesh[] symbolMeshes, Material material)
        {
            DestroyMarkers(portalMarkers);
            portals = resolvedPortals ?? Array.Empty<WorldZoneCatalog.Zone>();
            portalMarkers = new MapZoneMarker[portals.Length];
            for (int i = 0; i < portals.Length; i++)
            {
                WorldZoneCatalog.Zone portal = portals[i];
                Vector2 t = MapProjection.PortalToTable(portal.x, portal.z);
                portalMarkers[i] = CreateMarker($"Portal_{portal.id}", t, MapProjection.PortalRadius, i, portal.name, true,
                    symbolMeshes != null && i < symbolMeshes.Length ? symbolMeshes[i] : null, material);
            }
            BuildTexts();
            ForceShow();
        }

        /// <summary>Conecta el texto de la zona apuntada y las barras de la leyenda (los crea el generador del Editor).</summary>
        public void SetPanels(TextMeshPro info, GameObject[] bars)
        {
            infoText = info;
            legendBars = bars ?? Array.Empty<GameObject>();
            BuildTexts();
            ForceShow();
        }

        /// <summary>Marca una zona en la leyenda; presionar la misma otra vez la desmarca.</summary>
        public void Select(int index)
        {
            if (index < 0 || index >= ZoneCount) return;
            selected = selected == index ? -1 : index;
        }

        /// <summary>Marca un portal en la leyenda (solo información: no viaja); presionar el mismo otra vez lo desmarca.</summary>
        public void SelectPortal(int portalIndex)
        {
            if (portalIndex < 0 || portalIndex >= PortalCount) return;
            int entry = ZoneCount + portalIndex;
            selected = selected == entry ? -1 : entry;
        }

        // ---------------------------------------------------------------- IFocusPanel (Tab: vista completa)

        /// <summary>
        /// Encuadre de la vista completa: el centro del pergamino, la normal hacia la habitación (el frente del cuadro es −Z) y
        /// el tamaño del cuadro entero (con la escala del objeto).
        /// </summary>
        public bool TryGetFocusFrame(out Vector3 center, out Vector3 normal, out Vector2 size)
        {
            center = transform.TransformPoint(new Vector3(0f, 0f, MapProjection.FaceZ));
            normal = -transform.forward;
            float k = transform.lossyScale.x;
            size = new Vector2(MapProjection.BoardWidth * k, MapProjection.BoardHeight * k);
            return true;
        }

        // ---------------------------------------------------------------- ciclo

        private void Awake()
        {
            BuildTexts();
        }

        private void Start()
        {
            ForceShow();
        }

        private void Update()
        {
            TrackHover();
            int info = hovered != null ? EntryOf(hovered) : selected;
            if (info != shownInfo || (info < 0 && PlatformDetector.IsVR != shownHintVr)) ShowInfo(info);
            int bar = hovered != null ? EntryOf(hovered) : selected;
            if (bar != shownBar) ShowBar(bar);
        }

        private int EntryOf(MapZoneMarker marker) => marker.IsPortal ? ZoneCount + marker.Index : marker.Index;

        private void TrackHover()
        {
            if (interactor == null)
            {
                if (Time.unscaledTime < nextInteractorSearch) return;
                nextInteractorSearch = Time.unscaledTime + InteractorRetrySeconds;
                interactor = FindAnyObjectByType<PlayerInteractor>();
                if (interactor == null) return;
            }

            GameObject go = interactor.HoveredObject;
            if (go == lastHoveredObject) return;
            lastHoveredObject = go;
            if (hovered != null) hovered.SetHighlighted(false);
            hovered = null;
            if (go != null && go.TryGetComponent(out MapZoneMarker marker) && marker.Owner == this)
            {
                hovered = marker;
                hovered.SetHighlighted(true);
            }
        }

        // ---------------------------------------------------------------- textos

        private void ForceShow()
        {
            int entry = hovered != null ? EntryOf(hovered) : selected;
            ShowInfo(entry);
            ShowBar(entry);
        }

        private void ShowInfo(int entry)
        {
            shownInfo = entry;
            shownHintVr = PlatformDetector.IsVR;
            if (infoText == null) return;
            infoText.text = entry >= 0 && entry < infoLines.Length ? infoLines[entry] : (shownHintVr ? HintVr : HintPc);
        }

        /// <summary>Enciende solo la barra de la fila <paramref name="entry"/> (−1: ninguna).</summary>
        private void ShowBar(int entry)
        {
            shownBar = entry;
            if (legendBars == null) return;
            for (int i = 0; i < legendBars.Length; i++)
                if (legendBars[i] != null && legendBars[i].activeSelf != (i == entry)) legendBars[i].SetActive(i == entry);
        }

        /// <summary>Arma una vez todos los textos posibles del panel (uno por zona y uno por portal).</summary>
        private void BuildTexts()
        {
            int n = ZoneCount, p = PortalCount;
            infoLines = new string[n + p];
            for (int i = 0; i < n; i++)
            {
                WorldZoneCatalog.Zone zone = zones[i];
                infoLines[i] = $"<b><color={zone.HtmlColor}>{i + 1}</color>  {zone.name}</b>\n<size=85%>{zone.use}</size>";
            }
            for (int i = 0; i < p; i++)
            {
                WorldZoneCatalog.Zone portal = portals[i];
                infoLines[n + i] = $"<b><color={portal.HtmlColor}>Portal:</color>  {portal.name}</b>\n<size=85%>{portal.use}\nEl mapa no teletransporta.</size>";
            }
        }

        // ---------------------------------------------------------------- marcadores

        private MapZoneMarker CreateMarker(string name, Vector2 table, float radius, int index, string label, bool isPortal, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(table.x, table.y, MapProjection.FaceZ);
            go.transform.localRotation = Quaternion.identity;
            if (mesh != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
            var marker = go.AddComponent<MapZoneMarker>();
            var box = go.GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0f, -MapProjection.MarkerDepth * 0.5f);
            float side = radius * 2f + 0.01f;
            box.size = new Vector3(side, side, MapProjection.MarkerDepth + 0.01f);
            marker.Configure(this, index, label, isPortal);
            return marker;
        }

        private static void DestroyMarkers(MapZoneMarker[] list)
        {
            if (list == null) return;
            foreach (MapZoneMarker old in list)
                if (old != null) DestroyObject(old.gameObject);
        }

        private static void DestroyObject(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }
}
