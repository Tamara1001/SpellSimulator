using System;
using TMPro;
using UnityEngine;
using SpellSimulator.Interfaces;
using SpellSimulator.Player;

namespace SpellSimulator.World.Map
{
    /// <summary>
    /// Mapa del valle colgado en la planta baja de la torre (Docs/LABORATORIO_V3_PLAN.md (e)). Guarda las zonas y los portales
    /// del catálogo (Data/WorldZones.json, con la posición ya resuelta por el generador MapTowerMap), crea un
    /// <see cref="MapZoneMarker"/> por zona (círculo numerado) y por portal (rombo) y maneja la columna de texto: la entrada
    /// apuntada muestra su nombre y su uso; la zona presionada queda resaltada en la leyenda; el rombo presionado lleva al
    /// jugador hasta ese portal (<see cref="Portal.TeleportLocalPlayerHere"/>).
    ///
    /// Pantalla completa: apuntando el cuadro, M (PC) o X (VR) lo trae delante de la cámara, escalado para llenar la vista
    /// (PC) o como un panel grande frente al jugador (VR); M/X o Esc lo devuelven a la pared (también si el jugador se aleja).
    /// Clic o gatillo sobre el cuadro también lo abre. Se apunta igual que en la pared (mira central en PC, rayo en VR).
    ///
    /// Sigue lo apuntado por el PlayerInteractor; los textos se arman una vez y en Update solo se asignan cuando cambia lo
    /// que se muestra (cero allocations por frame).
    /// </summary>
    public class TowerMap : MonoBehaviour, IPressable
    {
        /// <summary>Cada cuánto se vuelve a buscar el interactor del jugador si todavía no existe (s).</summary>
        private const float InteractorRetrySeconds = 1f;

        private const string HintPc = "Apuntá un número o un rombo.\nClic en un número: lo resalta en la lista.\nClic en un rombo: viajás a ese portal.\nM: pantalla completa (M o Esc cierra).";
        private const string HintVr = "Apuntá un número o un rombo con el rayo.\nGatillo en un número: lo resalta.\nGatillo en un rombo: viajás al portal.\nX: pantalla completa.";

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
        private PlayerInputReader input;
        private float nextInteractorSearch;
        private GameObject lastHoveredObject;
        private MapZoneMarker hovered;
        private int selected = -1;
        private int shownInfo = int.MinValue;
        private int shownBar = int.MinValue;
        private bool shownHintVr;

        // Pantalla completa: pose en la pared para volver y desde dónde se abrió.
        private bool fullscreen;
        private Transform viewCamera;
        private Vector3 openCameraPosition;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private Vector3 homeScale;

        public int ZoneCount => zones != null ? zones.Length : 0;
        public int MarkerCount => markers != null ? markers.Length : 0;
        public int PortalCount => portals != null ? portals.Length : 0;
        public int PortalMarkerCount => portalMarkers != null ? portalMarkers.Length : 0;

        /// <summary>Zona marcada en la leyenda (−1 = ninguna).</summary>
        public int Selected => selected;

        /// <summary>True mientras el mapa está a pantalla completa.</summary>
        public bool IsFullscreen => fullscreen;

        public WorldZoneCatalog.Zone GetZone(int i) => zones[i];
        public MapZoneMarker GetMarker(int i) => markers[i];
        public WorldZoneCatalog.Zone GetPortal(int i) => portals[i];
        public MapZoneMarker GetPortalMarker(int i) => portalMarkers[i];

        /// <summary>Un portal se tocó en el mapa y el jugador viajó hasta él (id del portal).</summary>
        public static event Action<string> TraveledFromMap;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => TraveledFromMap = null;

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

        /// <summary>
        /// Lleva al jugador local hasta el portal <paramref name="portalIndex"/> del mapa (el mismo teletransporte que el cruce
        /// de un portal abierto). Cierra la pantalla completa antes de viajar.
        /// </summary>
        public void Travel(int portalIndex)
        {
            if (portalIndex < 0 || portalIndex >= PortalCount) return;
            WorldZoneCatalog.Zone entry = portals[portalIndex];
            Portal target = Portal.Find(entry.id);
            if (target == null)
            {
                Hud.Notify("map", "Ese portal no responde", 2f);
                return;
            }
            if (fullscreen) CloseView();
            if (!target.TeleportLocalPlayerHere())
            {
                Hud.Notify("map", "Ese portal no responde", 2f);
                return;
            }
            Hud.Notify("map", "Viajaste a: " + entry.name, 2.5f);
            TraveledFromMap?.Invoke(entry.id);
        }

        // ---------------------------------------------------------------- IPressable (el cuadro)

        public bool PressWithClick => true;

        public string GetPressPrompt() => "Mapa del valle";

        /// <summary>Clic o gatillo sobre el cuadro (fuera de los marcadores): lo abre a pantalla completa.</summary>
        public void Press()
        {
            if (!fullscreen) OpenView();
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

        private void OnDisable()
        {
            if (fullscreen) CloseView();
        }

        private void Update()
        {
            TrackHover();
            HandleView();
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
                input = interactor.GetComponent<PlayerInputReader>();
                if (input == null) input = FindAnyObjectByType<PlayerInputReader>();
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

        // ---------------------------------------------------------------- pantalla completa

        private void HandleView()
        {
            if (fullscreen)
            {
                bool close = viewCamera == null || (input != null && (input.IsTogglingMap || input.IsPressingEscapeRaw));
                if (!close && (viewCamera.position - openCameraPosition).sqrMagnitude > MapProjection.FullscreenMaxDrift * MapProjection.FullscreenMaxDrift)
                    close = true;
                if (close) CloseView();
                return;
            }
            // Solo se abre apuntando el cuadro o uno de sus marcadores.
            if (input != null && input.IsTogglingMap && (hovered != null || lastHoveredObject == gameObject)) OpenView();
        }

        private void OpenView()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            viewCamera = cam.transform;
            openCameraPosition = viewCamera.position;
            homePosition = transform.position;
            homeRotation = transform.rotation;
            homeScale = transform.localScale;

            Vector3 forward;
            float distance, scale;
            if (PlatformDetector.IsVR)
            {
                // Panel grande frente al jugador, vertical y a la altura de los ojos (no se inclina con la cabeza).
                forward = Vector3.ProjectOnPlane(viewCamera.forward, Vector3.up);
                if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(viewCamera.up, Vector3.up);
                forward.Normalize();
                distance = MapProjection.FullscreenDistanceVr;
                scale = MapProjection.FitScale(distance, MapProjection.FullscreenFovVr, MapProjection.FullscreenAspectVr);
            }
            else
            {
                // Pegado a la cámara y escalado para llenar la pantalla: se apunta con la mira moviendo la vista.
                forward = viewCamera.forward;
                distance = MapProjection.FullscreenDistancePc;
                scale = MapProjection.FitScale(distance, cam.fieldOfView, cam.aspect);
            }
            // El frente del cuadro (−Z) mira a la cámara: su +Z apunta en la dirección de la vista.
            Vector3 up = PlatformDetector.IsVR ? Vector3.up : viewCamera.up;
            transform.SetPositionAndRotation(viewCamera.position + forward * distance, Quaternion.LookRotation(forward, up));
            transform.localScale = homeScale * scale;
            Physics.SyncTransforms();
            fullscreen = true;
            FocusMode.EnterPanelView();
        }

        private void CloseView()
        {
            fullscreen = false;
            viewCamera = null;
            transform.SetPositionAndRotation(homePosition, homeRotation);
            transform.localScale = homeScale;
            Physics.SyncTransforms();
            FocusMode.ExitPanelView();
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
                infoLines[n + i] = $"<b><color={portal.HtmlColor}>Portal:</color>  {portal.name}</b>\n<size=85%>{portal.use}</size>";
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
