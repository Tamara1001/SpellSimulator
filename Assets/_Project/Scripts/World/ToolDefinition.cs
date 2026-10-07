using UnityEngine;

namespace SpellSimulator.World
{
    /// <summary>
    /// Datos de una herramienta de dibujo (lapiz, pincel, goma, trapo). Las herramientas de la escena leen de aca, asi
    /// que una herramienta nueva es un asset, no codigo. Ver GAME_DESIGN (catalogo de objetos).
    /// </summary>
    [CreateAssetMenu(menuName = "SpellSimulator/Tool Definition", fileName = "Tool")]
    public class ToolDefinition : ScriptableObject
    {
        public string displayName = "Lapiz";
        public PhysicalTool.ToolKind kind = PhysicalTool.ToolKind.Pen;

        [Header("Trazo (lapiz y pincel)")]
        [Tooltip("Ancho del trazo en metros. No cambia el reconocimiento: solo como se ve la tinta.")]
        [Range(0.002f, 0.2f)] public float strokeWidth = 0.04f;
        [Tooltip("Estabilizador en PC. Alto = sin retraso (el mouse no tiembla).")]
        public float pcStabilizerSpeed = 40f;
        [Tooltip("Estabilizador en VR. Bajo = mas suave contra el temblor, mas retraso.")]
        public float vrStabilizerSpeed = 14f;

        [Header("Tinta")]
        public float inkCapacity = 100f;
        [Tooltip("Tinta gastada por metro de trazo. Un pincel ancho gasta mas.")]
        public float inkPerMeter = 8f;
        public Color fullInkColor = new Color(0.12f, 0.08f, 0.06f);
        public Color emptyInkColor = new Color(0.75f, 0.72f, 0.68f);

        [Header("Goma")]
        [Tooltip("Radio de borrado en metros.")]
        public float eraseRadius = 0.025f;

        [Header("Trapo")]
        [Tooltip("Recorrido (UV, ida y vuelta suma) para limpiar el lienzo entero.")]
        public float wipeLengthToClear = 1.2f;
    }
}
