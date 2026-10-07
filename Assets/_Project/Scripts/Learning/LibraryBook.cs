using System;
using UnityEngine;
using SpellSimulator.DebugTools;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Learning
{
    /// <summary>Libro de la biblioteca: al leerlo (E / grip) ensena sus glifos y aparecen en el grimorio.</summary>
    public class LibraryBook : MonoBehaviour, IPressable
    {
        /// <summary>(libro, glifos nuevos) al leerlo.</summary>
        public static event Action<LibraryBook, int> BookRead;

        [SerializeField] private string bookId;

        public string BookId => bookId;

        public void SetBook(string id) => bookId = id;

        private BookInfo info;

        private BookInfo Info => info ??= Discovery.Instance != null ? Discovery.Instance.Lore.Book(bookId) : null;

        private string readPrompt, rereadPrompt;

        // Textos armados una sola vez: el interactor los pide en cada frame (sin GC).
        public string GetPressPrompt()
        {
            var info = Info;
            if (info == null) return "";
            if (readPrompt == null)
            {
                readPrompt = $"Leer \"{info.Title}\"";
                rereadPrompt = $"Releer \"{info.Title}\"";
            }
            return Discovery.Instance.State.HasRead(bookId) ? rereadPrompt : readPrompt;
        }

        public void Press()
        {
            var info = Info;
            if (info == null) return;
            int learned = Discovery.Instance.Read(info);
            var names = string.Join(", ", info.Glyphs.ConvertAll(g => Discovery.Instance.Lore.Name(g)));
            DiagnosticBus.OnDiagnosticsUpdated?.Invoke(learned > 0
                ? $"<b>[Libro] {info.Title}</b>\nAprendiste: {names}. Abri el grimorio (Tab) para verlos."
                : $"<b>[Libro] {info.Title}</b>\nYa conocias: {names}.");
            BookRead?.Invoke(this, learned);
        }
    }
}
