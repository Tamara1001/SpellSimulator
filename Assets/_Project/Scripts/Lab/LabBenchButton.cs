using UnityEngine;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Lab
{
    /// <summary>Que hace un boton del banco de sellos.</summary>
    public enum LabBenchAction { Launch = 0, ResetSeal = 1 }

    /// <summary>
    /// Boton del banco de sellos (Laboratorio v2): «Lanzar» llama a <see cref="LabSealBench.Launch"/> y «Por defecto» a
    /// <see cref="LabSealBench.ResetSpec"/>. Se aprieta apuntando + clic (gatillo en VR). El bloqueo en red lo decide el banco;
    /// el lanzamiento tiene un enfriamiento corto para que un doble clic no dispare dos hechizos.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class LabBenchButton : MonoBehaviour, IPressable, LabParameters.IHoverTarget
    {
        /// <summary>Segundos minimos entre dos lanzamientos.</summary>
        private const float LaunchCooldown = 0.6f;

        [SerializeField, Tooltip("Lanzar el sello o volver al sello por defecto.")]
        private LabBenchAction action;
        [SerializeField, Tooltip("Banco de sellos (vacio = LabSealBench.Current).")]
        private LabSealBench bench;
        [SerializeField, Tooltip("Renderer que se resalta al apuntar (vacio = el primero en los hijos).")]
        private Renderer highlight;

        private LabControl.HoverTint tint;
        private float nextPress;

        /// <summary>Lo llama el generador de escena (Editor) despues de AddComponent.</summary>
        public void Configure(LabBenchAction action, LabSealBench bench, Renderer highlight)
        {
            this.action = action;
            this.bench = bench;
            this.highlight = highlight;
        }

        private void Awake()
        {
            if (highlight == null) highlight = GetComponentInChildren<Renderer>();
            tint.Init(highlight);
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnDisable() => tint.Set(false);

        public bool PressWithClick => true;

        public string GetPressPrompt() => action == LabBenchAction.Launch ? "Lanzar el sello" : "Sello por defecto";

        public void Press()
        {
            LabSealBench b = bench != null ? bench : LabSealBench.Current;
            if (b == null) return;
            if (action == LabBenchAction.ResetSeal)
            {
                b.ResetSpec();
                return;
            }
            if (Time.unscaledTime < nextPress) return;
            nextPress = Time.unscaledTime + LaunchCooldown;
            b.Launch();
        }

        public void SetHovered(bool on) => tint.Set(on);
    }
}
