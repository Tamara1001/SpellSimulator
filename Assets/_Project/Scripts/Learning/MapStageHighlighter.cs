using UnityEngine;
using SpellSimulator.World.Map;

namespace SpellSimulator.Learning
{
    /// <summary>
    /// Puente entre el aprendizaje y el tablero del mapa (paso 9, D32): calcula la etapa sugerida con <see cref="LearningFlow"/>
    /// y se la pasa al <see cref="TowerMap"/> de su mismo objeto. Se recalcula solo por eventos (al leer un libro y al
    /// reiniciar el progreso), nunca en Update. Lo agrega MapTowerMap al tablero.
    /// </summary>
    [RequireComponent(typeof(TowerMap))]
    public class MapStageHighlighter : MonoBehaviour
    {
        /// <summary>Cada cuánto se reintenta si Discovery todavía no existe (s).</summary>
        private const float RetrySeconds = 1f;

        private TowerMap map;
        private LearningFlow.Stage? shown;

        /// <summary>Etapa aplicada al tablero (null si todavía no se pudo calcular).</summary>
        public LearningFlow.Stage? Shown => shown;

        private void Awake() => map = GetComponent<TowerMap>();

        private void OnEnable()
        {
            LibraryBook.BookRead += OnBookRead;
            Discovery.Discovered += OnDiscovered;
            Refresh();
            if (shown == null) InvokeRepeating(nameof(RetryUntilDiscovery), RetrySeconds, RetrySeconds);
        }

        private void OnDisable()
        {
            LibraryBook.BookRead -= OnBookRead;
            Discovery.Discovered -= OnDiscovered;
            CancelInvoke(nameof(RetryUntilDiscovery));
        }

        private void OnBookRead(LibraryBook book, int learned) => Refresh();

        private void OnDiscovered(string glyph, string how) => Refresh();

        private void RetryUntilDiscovery()
        {
            Refresh();
            if (shown != null) CancelInvoke(nameof(RetryUntilDiscovery));
        }

        /// <summary>Recalcula y aplica la etapa (público para el harness).</summary>
        public void Refresh()
        {
            if (map == null || Discovery.Instance == null || Discovery.Instance.State == null) return;
            DiscoveryState state = Discovery.Instance.State;
            LearningFlow.Stage stage = LearningFlow.StageFor(state.HasRead);
            if (shown == stage) return;
            shown = stage;
            map.SetSuggested(LearningFlow.ZonesFor(stage));
        }
    }
}
