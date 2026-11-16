using System.Text;
using TMPro;
using UnityEngine;
using SpellSimulator.Compiler;
using SpellSimulator.Core;
using SpellSimulator.Player;

namespace SpellSimulator.Lab
{
    /// <summary>Pool de etiquetas del campo, limitado a cuatro objetivos dentro de la mirada del jugador.</summary>
    public sealed class LabGazeTagPresenter : MonoBehaviour
    {
        private const int Capacity = 6;
        private const float PollSeconds = 0.1f;
        private readonly Vector3[] positions = new Vector3[Capacity];
        private readonly bool[] shown = new bool[Capacity];
        private readonly int[] boxNumbers = new int[Capacity];
        private readonly int[] selected = new int[LabGazeTags.MaxVisible];
        private readonly float[] scores = new float[LabGazeTags.MaxVisible];
        private readonly Transform[] anchors = new Transform[Capacity];
        private readonly TextMeshPro[] labels = new TextMeshPro[Capacity];
        private readonly GameObject[] plates = new GameObject[Capacity];
        private readonly StringBuilder text = new StringBuilder(128);
        private LabSealBench bench;
        private LabField field;
        private float nextPoll;
        private Camera cam;
        private Vector3 viewerPosition;
        private int padIndex = -1;
        private string reachText = "—", durationText = "—", fireAreaText = "—";
        private string measurementLine = "alcance — · duración — · área quemada —";

        private void OnEnable() => LabMeasurements.Measured += OnMeasured;
        private void OnDisable() => LabMeasurements.Measured -= OnMeasured;

        private void Awake()
        {
            bench = FindFirstObjectByType<LabSealBench>();
            field = FindFirstObjectByType<LabField>();
            for (int i = 0; i < Capacity; i++)
            {
                GameObject anchor = new GameObject("LabGazeAnchor");
                anchor.transform.SetParent(transform, false);
                anchors[i] = anchor.transform;
                GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Quad);
                plate.name = "LabGazeGoldBorder";
                plate.transform.SetParent(anchor.transform, false);
                plate.transform.localScale = new Vector3(0.62f, 0.2f, 1f);
                var collider = plate.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                var renderer = plate.GetComponent<Renderer>();
                renderer.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.66f, 0.43f, 0.16f, 0.96f) };
                plates[i] = plate;
                GameObject inner = GameObject.CreatePrimitive(PrimitiveType.Quad);
                inner.name = "LabGazeDarkFace";
                inner.transform.SetParent(anchor.transform, false);
                inner.transform.localPosition = new Vector3(0f, 0f, -0.001f);
                inner.transform.localScale = new Vector3(0.606f, 0.186f, 1f);
                var innerCollider = inner.GetComponent<Collider>();
                if (innerCollider != null) Destroy(innerCollider);
                inner.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default"))
                    { color = new Color(0.035f, 0.025f, 0.045f, 0.9f) };
                GameObject go = new GameObject("LabGazeText");
                go.transform.SetParent(anchor.transform, false);
                go.transform.localPosition = new Vector3(0f, 0f, 0.006f);
                var tmp = go.AddComponent<TextMeshPro>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = 3.4f;
                tmp.color = new Color(1f, 0.91f, 0.72f);
                tmp.enableWordWrapping = true;
                tmp.rectTransform.sizeDelta = new Vector2(0.58f, 0.17f);
                labels[i] = tmp;
                anchor.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + PollSeconds;
            if (!LabOverlay.Visible || !TryViewer(out Vector3 eye, out Vector3 forward)) { HideAll(); return; }
            viewerPosition = eye;
            int count = Collect();
            int picked = LabGazeTags.Select(eye, forward, positions, count, shown, selected, scores);
            for (int i = 0; i < Capacity; i++) shown[i] = false;
            for (int i = 0; i < picked; i++) shown[selected[i]] = true;
            for (int i = 0; i < Capacity; i++)
            {
                if (!shown[i]) { if (anchors[i].gameObject.activeSelf) anchors[i].gameObject.SetActive(false); continue; }
                anchors[i].position = positions[i] + Vector3.up * 0.42f;
                Vector3 look = eye - anchors[i].position;
                if (look.sqrMagnitude > 1e-5f) anchors[i].rotation = Quaternion.LookRotation(look, Vector3.up);
                if (!anchors[i].gameObject.activeSelf) anchors[i].gameObject.SetActive(true);
                UpdateText(i);
            }
        }

        private int Collect()
        {
            for (int i = 0; i < Capacity; i++) positions[i] = new Vector3(1000f, 1000f, 1000f);
            Transform paper = bench != null ? bench.PaperTransform : null;
            if (paper != null) positions[0] = paper.position;
            if (field != null)
            {
                for (int slot = 1; slot <= LabSpawner.MaxBoxCount; slot++)
                {
                    GameObject box = field.BoxAt(slot);
                    if (box == null) continue;
                    positions[slot] = box.transform.position;
                    boxNumbers[slot] = slot;
                }
            }
            LabSketchPad pad = bench != null ? bench.SketchPad : null;
            padIndex = Capacity - 1;
            if (pad != null && pad.Canvas != null) positions[padIndex] = pad.Canvas.WorldCenter;
            return Capacity;
        }

        private void UpdateText(int index)
        {
            text.Clear();
            if (index == 0)
            {
                LabSealResult result = bench != null ? bench.UltimoResultado : default;
                text.Append(result.HasResult ? (result.Valid ? "Válido" : "Inválido") : "Sin lanzamiento");
                if (result.HasResult)
                    text.Append(" · ").Append(Element(result.Element)).Append(" · calidad ").Append(Mathf.RoundToInt(result.QualityPercent)).Append(" %");
                text.Append('\n').Append(measurementLine);
                labels[index].SetText(text);
            }
            else if (index == padIndex)
                labels[index].SetText(bench.UsesDrawing ? "Dibujo a mano" : "Vista previa");
            else
                labels[index].SetText("Caja {0} · a {1:0.00} m", boxNumbers[index], Vector3.Distance(positions[index], viewerPosition));
        }

        private bool TryViewer(out Vector3 eye, out Vector3 forward)
        {
            IPlayerContext player = PlayerRegistry.Local;
            Transform head = player != null ? player.Head : null;
            if (head == null) { if (cam == null) cam = Camera.main; if (cam != null) head = cam.transform; }
            if (head == null) { eye = forward = Vector3.zero; return false; }
            eye = head.position; forward = head.forward; return true;
        }

        private void OnMeasured(LabMeasurement measurement)
        {
            string value = LabCsv.FormatNumber(measurement.Value,
                measurement.Kind == LabMeasurementKind.FireArea ? "0" : "0.00");
            switch (measurement.Kind)
            {
                case LabMeasurementKind.CastReach: reachText = value + " m"; break;
                case LabMeasurementKind.CastDuration: durationText = value + " s"; break;
                case LabMeasurementKind.FireArea: fireAreaText = value + " m²"; break;
            }
            measurementLine = "alcance " + reachText + " · duración " + durationText + " · área quemada " + fireAreaText;
        }

        private void HideAll() { for (int i = 0; i < Capacity; i++) { shown[i] = false; if (anchors[i] != null && anchors[i].gameObject.activeSelf) anchors[i].gameObject.SetActive(false); } }
        private static string Element(ElementType e) => e == ElementType.Fire ? "fuego" : e == ElementType.Water ? "agua" : e == ElementType.Wind ? "viento" : e == ElementType.Earth ? "tierra" : e == ElementType.Light ? "luz" : "sin elemento";
    }
}
