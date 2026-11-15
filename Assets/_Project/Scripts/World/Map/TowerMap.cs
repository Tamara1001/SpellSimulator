using System;
using System.Text;
using TMPro;
using UnityEngine;
using SpellSimulator.Player;

namespace SpellSimulator.World.Map
{
    /// <summary>
    /// Mapa del valle colgado en la planta baja de la torre (Docs/LABORATORIO_V3_PLAN.md (e)). Guarda las zonas del catálogo
    /// (Data/WorldZones.json, con la posición ya resuelta por el generador MapTowerMap), crea un <see cref="MapZoneMarker"/>
    /// por zona y maneja la columna de texto: la zona apuntada muestra su nombre y su uso; la presionada queda marcada en la
    /// leyenda. Sigue lo apuntado por el PlayerInteractor (mouse en PC, rayo en VR); los textos se arman una vez y en Update
    /// solo se asignan cuando cambia lo que se muestra (cero allocations por frame).
    /// </summary>
    public class TowerMap : MonoBehaviour
    {
        /// <summary>Cada cuánto se vuelve a buscar el interactor del jugador si todavía no existe (s).</summary>
        private const float InteractorRetrySeconds = 1f;

        private const string HintPc = "Apuntá un número con el mouse para ver qué hay ahí.\nClic: lo marca en la lista.";
        private const string HintVr = "Apuntá un número con el rayo para ver qué hay ahí.\nGatillo: lo marca en la lista.";

        /// <summary>Resaltado de la línea elegida en la leyenda.</summary>
        private const string MarkOpen = "<mark=#E8C35A88>";
        private const string MarkClose = "</mark>";

        [SerializeField, Tooltip("Zonas con la posición ya resuelta (las guarda MapTowerMap desde Data/WorldZones.json).")]
        private WorldZoneCatalog.Zone[] zones = Array.Empty<WorldZoneCatalog.Zone>();

        [SerializeField, Tooltip("Un marcador por zona, en el mismo orden.")]
        private MapZoneMarker[] markers = Array.Empty<MapZoneMarker>();

        [SerializeField, Tooltip("Texto con el nombre y el uso de la zona apuntada.")]
        private TextMeshPro infoText;

        [SerializeField, Tooltip("Leyenda numerada de todas las zonas.")]
        private TextMeshPro legendText;

        private string[] infoLines = Array.Empty<string>();
        private string[] legendLines = Array.Empty<string>();
        private string legendDefault = "";

        private PlayerInteractor interactor;
        private float nextInteractorSearch;
        private GameObject lastHoveredObject;
        private MapZoneMarker hovered;
        private int selected = -1;
        private int shownInfo = int.MinValue;
        private int shownLegend = int.MinValue;
        private bool shownHintVr;

        public int ZoneCount => zones != null ? zones.Length : 0;
        public int MarkerCount => markers != null ? markers.Length : 0;

        /// <summary>Zona marcada en la leyenda (−1 = ninguna).</summary>
        public int Selected => selected;

        public WorldZoneCatalog.Zone GetZone(int i) => zones[i];
        public MapZoneMarker GetMarker(int i) => markers[i];

        /// <summary>
        /// Crea (o recrea) un marcador por zona sobre el pergamino, en <see cref="MapProjection.WorldToTable"/>. Lo llama el
        /// generador del Editor; <paramref name="markerMeshes"/> puede ser null (tests): el marcador queda sin malla.
        /// </summary>
        public void Setup(WorldZoneCatalog.Zone[] resolvedZones, Mesh[] markerMeshes, Material material)
        {
            if (markers != null)
                foreach (MapZoneMarker old in markers)
                    if (old != null) DestroyObject(old.gameObject);

            zones = resolvedZones ?? Array.Empty<WorldZoneCatalog.Zone>();
            markers = new MapZoneMarker[zones.Length];
            for (int i = 0; i < zones.Length; i++)
            {
                WorldZoneCatalog.Zone zone = zones[i];
                Vector2 t = MapProjection.WorldToTable(zone.x, zone.z);
                var go = new GameObject($"Marker_{i + 1}_{zone.id}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(t.x, t.y, MapProjection.FaceZ);
                go.transform.localRotation = Quaternion.identity;
                if (markerMeshes != null && i < markerMeshes.Length && markerMeshes[i] != null)
                {
                    go.AddComponent<MeshFilter>().sharedMesh = markerMeshes[i];
                    go.AddComponent<MeshRenderer>().sharedMaterial = material;
                }
                var marker = go.AddComponent<MapZoneMarker>();
                var box = go.GetComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = new Vector3(0f, 0f, -MapProjection.MarkerDepth * 0.5f);
                float side = MapProjection.MarkerRadius * 2f + 0.01f;
                box.size = new Vector3(side, side, MapProjection.MarkerDepth + 0.01f);
                marker.Configure(this, i, zone.name);
                markers[i] = marker;
            }
            BuildTexts();
            ForceShow();
        }

        /// <summary>Conecta los textos de la columna (los crea el generador del Editor) y les pone el estado inicial.</summary>
        public void SetPanels(TextMeshPro info, TextMeshPro legend)
        {
            infoText = info;
            legendText = legend;
            BuildTexts();
            ForceShow();
        }

        /// <summary>Marca una zona en la leyenda; presionar la misma otra vez la desmarca.</summary>
        public void Select(int index)
        {
            if (index < 0 || index >= ZoneCount) return;
            selected = selected == index ? -1 : index;
        }

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
            int info = hovered != null ? hovered.Index : selected;
            if (info != shownInfo || (info < 0 && PlatformDetector.IsVR != shownHintVr)) ShowInfo(info);
            if (selected != shownLegend) ShowLegend(selected);
        }

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

        private void ForceShow()
        {
            ShowInfo(hovered != null ? hovered.Index : selected);
            ShowLegend(selected);
        }

        private void ShowInfo(int index)
        {
            shownInfo = index;
            shownHintVr = PlatformDetector.IsVR;
            if (infoText == null) return;
            infoText.text = index >= 0 && index < infoLines.Length ? infoLines[index] : (shownHintVr ? HintVr : HintPc);
        }

        private void ShowLegend(int index)
        {
            shownLegend = index;
            if (legendText == null) return;
            legendText.text = index >= 0 && index < legendLines.Length ? legendLines[index] : legendDefault;
        }

        /// <summary>Arma una vez todos los textos posibles (uno por zona y la leyenda con cada línea marcada).</summary>
        private void BuildTexts()
        {
            int n = ZoneCount;
            infoLines = new string[n];
            legendLines = new string[n];
            var sb = new StringBuilder(512);
            for (int i = 0; i < n; i++)
            {
                WorldZoneCatalog.Zone zone = zones[i];
                infoLines[i] = $"<b><color={zone.HtmlColor}>{i + 1}</color>  {zone.name}</b>\n<size=85%>{zone.use}</size>";
            }
            legendDefault = Legend(sb, -1);
            for (int i = 0; i < n; i++) legendLines[i] = Legend(sb, i);
        }

        private string Legend(StringBuilder sb, int marked)
        {
            sb.Clear();
            for (int i = 0; i < ZoneCount; i++)
            {
                WorldZoneCatalog.Zone zone = zones[i];
                if (i > 0) sb.Append('\n');
                if (i == marked) sb.Append(MarkOpen);
                sb.Append("<color=").Append(zone.HtmlColor).Append("><b>").Append(i + 1).Append("</b></color>  ").Append(zone.name);
                if (i == marked) sb.Append(MarkClose);
            }
            return sb.ToString();
        }

        private static void DestroyObject(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }
}
