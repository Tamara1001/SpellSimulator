using TMPro;
using UnityEngine;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Contadores fijos de la mesa del laboratorio: el ultimo valor medido de cada tipo (area quemada, distancia de la caja,
    /// alcance y duracion del hechizo) con su unidad y la marca "(superpuesto)". Coma decimal, igual que el CSV. Sin Update:
    /// el texto solo cambia cuando llega una medicion (<see cref="LabMeasurements.Measured"/>).
    /// </summary>
    public class LabCounters : MonoBehaviour
    {
        [SerializeField, Tooltip("Texto del area quemada del ultimo episodio de fuego.")]
        private TMP_Text fireAreaText;
        [SerializeField, Tooltip("Texto de la distancia recorrida por la ultima caja.")]
        private TMP_Text boxDistanceText;
        [SerializeField, Tooltip("Texto del alcance del ultimo hechizo.")]
        private TMP_Text castReachText;
        [SerializeField, Tooltip("Texto de la duracion del ultimo hechizo.")]
        private TMP_Text castDurationText;

        private const string Empty = "—";
        private const string OverlappedMark = " (superpuesto)";

        private const string FireTitle = "Área quemada";
        private const string BoxTitle = "Distancia de la caja";
        private const string ReachTitle = "Alcance del hechizo";
        private const string DurationTitle = "Duración del hechizo";

        private void Awake()
        {
            SetEmpty(fireAreaText, FireTitle);
            SetEmpty(boxDistanceText, BoxTitle);
            SetEmpty(castReachText, ReachTitle);
            SetEmpty(castDurationText, DurationTitle);
        }

        private void OnEnable() => LabMeasurements.Measured += OnMeasured;
        private void OnDisable() => LabMeasurements.Measured -= OnMeasured;

        private void OnMeasured(LabMeasurement m)
        {
            switch (m.Kind)
            {
                // El contador usa una unidad corta; el CSV lleva la completa ("m2 (celdas de 4 m2)").
                case LabMeasurementKind.FireArea: Show(fireAreaText, FireTitle, m, "0", "m2"); break;
                case LabMeasurementKind.BoxDisplacement: Show(boxDistanceText, BoxTitle, m, "0.00", "m"); break;
                case LabMeasurementKind.CastReach: Show(castReachText, ReachTitle, m, "0.0", "m"); break;
                case LabMeasurementKind.CastDuration: Show(castDurationText, DurationTitle, m, "0.0", "s"); break;
            }
        }

        private static void Show(TMP_Text text, string title, in LabMeasurement m, string format, string unit)
        {
            if (text == null) return;
            string value = LabCsv.FormatNumber(m.Value, format);
            if (value.Length == 0) value = Empty;
            text.SetText(title + ": " + value + " " + unit + (m.Overlapped ? OverlappedMark : string.Empty));
        }

        private static void SetEmpty(TMP_Text text, string title)
        {
            if (text != null) text.SetText(title + ": " + Empty);
        }
    }
}
