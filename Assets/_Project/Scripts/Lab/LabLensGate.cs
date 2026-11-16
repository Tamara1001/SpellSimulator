using UnityEngine;
using SpellSimulator.Core;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Muestra un contenido de datos del laboratorio solo con las lentes de tinta puestas (<see cref="LabOverlay.Visible"/>), de
    /// cerca y mirando hacia el (<see cref="LabLensVisibility"/>, con histeresis). Lo usan la banda de resultado, la lectura del
    /// atril de dibujo y los contadores del pizarron (Laboratorio v4, punto 10). Sondeo a 10 Hz y SetActive solo al cambiar.
    /// La mirada es la cabeza del jugador local (PlayerRegistry; en PC la camara) o <see cref="ViewerOverride"/> (pruebas).
    /// </summary>
    public class LabLensGate : MonoBehaviour
    {
        private const float PollSeconds = 0.1f;

        /// <summary>Mirada forzada (solo pruebas: HarnessPlaySmoke). Null = el jugador local.</summary>
        public static Transform ViewerOverride { get; set; }

        /// <summary>
        /// Laboratorio v6: la pizarra de mediciones, la banda de resultado y la lectura del atril se retiraron (ahora son las
        /// etiquetas por mirada de <see cref="LabGazeTagPresenter"/>). Con true (por defecto) el gate no muestra nunca su contenido.
        /// Codigo muerto a limpiar al final de v6; las pruebas viejas pueden ponerlo en false.
        /// </summary>
        public static bool Retired { get; set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ViewerOverride = null;
            Retired = true;
        }

        [SerializeField, Tooltip("Lo que se muestra/oculta (no debe ser este objeto).")]
        private GameObject content;
        [SerializeField, Tooltip("Punto que hay que mirar (vacio = este objeto).")]
        private Transform anchor;
        [SerializeField, Tooltip("Distancia (m) para empezar a mostrar (3 m por defecto).")]
        private float showDistance = LabLensVisibility.ShowDistance;

        private bool visible;
        private float nextPoll;
        private Camera cachedCamera;

        /// <summary>True si el contenido esta a la vista ahora.</summary>
        public bool IsShowing => visible;
        /// <summary>Contenido que controla.</summary>
        public GameObject Content => content;

        /// <summary>Lo llama el generador de escena (Editor) despues de AddComponent.</summary>
        public void Configure(GameObject content, Transform anchor, float showDistance)
        {
            this.content = content;
            this.anchor = anchor;
            this.showDistance = showDistance;
        }

        private void Awake()
        {
            if (anchor == null) anchor = transform;
            if (content != null && content.activeSelf) content.SetActive(false);
            visible = false;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + PollSeconds;
            bool next = false;
            if (!Retired && LabOverlay.Visible && Viewer(out Vector3 eye, out Vector3 forward))
            {
                Vector3 to = anchor.position - eye;
                float distance = to.magnitude;
                float angle = distance > 1e-4f ? Vector3.Angle(forward, to) : 0f;
                next = LabLensVisibility.IsVisible(true, distance, angle, visible, showDistance);
            }
            if (next == visible) return;
            visible = next;
            if (content != null) content.SetActive(next);
        }

        private bool Viewer(out Vector3 eye, out Vector3 forward) => TryGetViewer(ref cachedCamera, out eye, out forward);

        /// <summary>
        /// Ojo y direccion de la mirada del jugador local (o <see cref="ViewerOverride"/>); lo comparte <see cref="LabGazeTagPresenter"/>.
        /// <paramref name="cameraCache"/> guarda Camera.main entre llamadas.
        /// </summary>
        public static bool TryGetViewer(ref Camera cameraCache, out Vector3 eye, out Vector3 forward)
        {
            Transform head = ViewerOverride;
            if (head == null)
            {
                IPlayerContext player = PlayerRegistry.Local;
                if (player != null) head = player.Head;
            }
            if (head == null)
            {
                if (cameraCache == null) cameraCache = Camera.main;
                if (cameraCache != null) head = cameraCache.transform;
            }
            if (head == null)
            {
                eye = forward = Vector3.zero;
                return false;
            }
            eye = head.position;
            forward = head.forward;
            return true;
        }
    }
}
