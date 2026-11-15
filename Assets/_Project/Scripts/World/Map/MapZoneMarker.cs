using UnityEngine;
using SpellSimulator.Interfaces;

namespace SpellSimulator.World.Map
{
    /// <summary>
    /// Marcador numerado de una zona en el mapa de la torre. Apuntarlo (mouse en PC, rayo en VR) muestra el nombre y el uso
    /// de la zona en el panel del cuadro; presionarlo (clic o gatillo) marca su línea en la leyenda. El resaltado y el panel
    /// los maneja <see cref="TowerMap"/>, que sigue lo apuntado por el PlayerInteractor en ambas plataformas.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class MapZoneMarker : MonoBehaviour, IPressable
    {
        /// <summary>Escala del marcador mientras se lo apunta.</summary>
        private const float HoverScale = 1.3f;

        [SerializeField, Tooltip("Mapa dueño del marcador.")]
        private TowerMap owner;

        [SerializeField, Tooltip("Índice de la zona en el catálogo del mapa.")]
        private int index;

        [SerializeField, Tooltip("Nombre de la zona (para el aviso de interacción).")]
        private string zoneName = "";

        public TowerMap Owner => owner;
        public int Index => index;

        /// <summary>Lo llama <see cref="TowerMap.Setup"/> al crear el marcador.</summary>
        public void Configure(TowerMap map, int zoneIndex, string name)
        {
            owner = map;
            index = zoneIndex;
            zoneName = name ?? "";
        }

        public bool PressWithClick => true;

        public string GetPressPrompt() => zoneName;

        public void Press()
        {
            if (owner != null) owner.Select(index);
        }

        /// <summary>Agranda el marcador mientras se lo apunta (sin allocations).</summary>
        public void SetHighlighted(bool on) => transform.localScale = on ? Vector3.one * HoverScale : Vector3.one;
    }
}
