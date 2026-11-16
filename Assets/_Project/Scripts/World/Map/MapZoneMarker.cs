using UnityEngine;
using SpellSimulator.Interfaces;

namespace SpellSimulator.World.Map
{
    /// <summary>
    /// Marcador del mapa de la torre: el círculo numerado de una zona o el rombo de un portal. Apuntarlo (mouse en PC, rayo
    /// en VR) muestra el nombre y el uso en el panel del cuadro; presionarlo (clic o gatillo) marca la entrada en la leyenda
    /// (también el rombo de un portal: es solo información, no viaja). El resaltado y el panel los maneja <see cref="TowerMap"/>,
    /// que sigue lo apuntado por el PlayerInteractor en ambas plataformas.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class MapZoneMarker : MonoBehaviour, IPressable
    {
        /// <summary>Escala del marcador mientras se lo apunta.</summary>
        private const float HoverScale = 1.3f;

        [SerializeField, Tooltip("Mapa dueño del marcador.")]
        private TowerMap owner;

        [SerializeField, Tooltip("Índice de la zona (o del portal) en el catálogo del mapa.")]
        private int index;

        [SerializeField, Tooltip("True si es el rombo de un portal (solo información: no viaja).")]
        private bool portal;

        [SerializeField, Tooltip("Nombre de la zona o del portal (para el aviso de interacción).")]
        private string zoneName = "";

        public TowerMap Owner => owner;
        public int Index => index;
        public bool IsPortal => portal;

        /// <summary>Lo llama <see cref="TowerMap"/> al crear el marcador.</summary>
        public void Configure(TowerMap map, int zoneIndex, string name, bool isPortal = false)
        {
            owner = map;
            index = zoneIndex;
            portal = isPortal;
            zoneName = name ?? "";
        }

        public bool PressWithClick => true;

        public string GetPressPrompt() => zoneName;

        public void Press()
        {
            if (owner == null) return;
            if (portal) owner.SelectPortal(index);
            else owner.Select(index);
        }

        /// <summary>Agranda el marcador mientras se lo apunta (sin allocations).</summary>
        public void SetHighlighted(bool on) => transform.localScale = on ? Vector3.one * HoverScale : Vector3.one;
    }
}
