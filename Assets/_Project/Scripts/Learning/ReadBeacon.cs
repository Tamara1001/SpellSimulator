using UnityEngine;
using SpellSimulator.Audio;

namespace SpellSimulator.Learning
{
    /// <summary>
    /// «El mundo responde» (paso 11, D27): enciende <see cref="lit"/> cuando el jugador leyó <see cref="bookId"/>. Lo usan el
    /// farol de cada atril de libro y el estandarte de cada campo de elemento. Sin Update: se recalcula al leer un libro y al
    /// reiniciar el progreso (eventos de LibraryBook y Discovery). Al encenderse por una lectura suena el mismo aviso que
    /// los faroles del tutorial; al cargar la partida se prende en silencio.
    /// </summary>
    public class ReadBeacon : MonoBehaviour
    {
        private const float RetrySeconds = 1f, Volume = 0.8f;
        private const string CueId = "seal_prepared";

        [SerializeField, Tooltip("Id del libro (glifos.json) que enciende este objeto.")]
        private string bookId;

        [SerializeField, Tooltip("Lo que se prende al leer el libro (malla clara, sin luz real).")]
        private GameObject lit;

        private bool ready;

        public string BookId => bookId;
        public bool IsLit => lit != null && lit.activeSelf;

        /// <summary>Lo llama el generador del mapa.</summary>
        public void Configure(string book, GameObject litObject)
        {
            bookId = book;
            lit = litObject;
        }

        private void OnEnable()
        {
            LibraryBook.BookRead += OnBookRead;
            Discovery.Discovered += OnDiscovered;
            Refresh(false);
            if (!ready) InvokeRepeating(nameof(Retry), RetrySeconds, RetrySeconds);
        }

        private void OnDisable()
        {
            LibraryBook.BookRead -= OnBookRead;
            Discovery.Discovered -= OnDiscovered;
            CancelInvoke(nameof(Retry));
        }

        private void OnBookRead(LibraryBook book, int learned)
        {
            if (book != null && book.BookId == bookId) Refresh(true);
        }

        private void OnDiscovered(string glyph, string how) => Refresh(false);

        private void Retry()
        {
            Refresh(false);
            if (ready) CancelInvoke(nameof(Retry));
        }

        /// <summary>Aplica el estado de lectura (público para el harness). <paramref name="announce"/>: suena si se acaba de prender.</summary>
        public void Refresh(bool announce)
        {
            if (lit == null || Discovery.Instance == null || Discovery.Instance.State == null) return;
            ready = true;
            bool on = Discovery.Instance.State.HasRead(bookId);
            if (lit.activeSelf == on) return;
            lit.SetActive(on);
            if (on && announce && AudioDirector.Instance != null) AudioDirector.Instance.Play(CueId, lit.transform.position, Volume);
        }
    }
}
