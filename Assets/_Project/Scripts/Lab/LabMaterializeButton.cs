using UnityEngine;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Boton «materializar» de la mesa del laboratorio: se aprieta apuntando + clic (gatillo en VR) y pide a
    /// <see cref="LabSpawner"/> una caja, una zona de viento, un parche de fuego o (fase 3) una fuente con los valores actuales.
    /// Los topes, el enfriamiento y el bloqueo en red los decide el spawner.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class LabMaterializeButton : MonoBehaviour, IPressable, LabParameters.IHoverTarget
    {
        [SerializeField, Tooltip("Que materializa este boton.")]
        private LabSpawnKind kind = LabSpawnKind.Box;
        [SerializeField, Tooltip("Renderer que se resalta al apuntar (vacio = el primero en los hijos).")]
        private Renderer highlight;

        private string prompt;
        private LabControl.HoverTint tint;

        private void Awake()
        {
            if (highlight == null) highlight = GetComponentInChildren<Renderer>();
            tint.Init(highlight);
            prompt = kind switch
            {
                LabSpawnKind.Box => "Materializar caja",
                LabSpawnKind.Wind => "Materializar viento",
                LabSpawnKind.Fire => "Materializar fuego",
                LabSpawnKind.Fountain => "Materializar fuente",
                _ => "Materializar"
            };
        }

        public bool PressWithClick => true;

        public string GetPressPrompt() => prompt ?? "";

        public void Press()
        {
            LabSpawner spawner = LabSpawner.Current;
            if (spawner != null) spawner.Materialize(kind);
        }

        public void SetHovered(bool on) => tint.Set(on);
    }
}
