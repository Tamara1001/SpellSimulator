using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SpellSimulator.DebugTools;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Learning
{
    /// <summary>
    /// Libro de la biblioteca: E lo abre en el lector de libros (UI Toolkit) y su ultima pagina ensena los glifos, que
    /// aparecen en el grimorio. En VR (sin lector) los ensena al apretarlo.
    /// </summary>
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

        /// <summary>
        /// Abre el libro en el lector (portada, una doble pagina por glifo y el cierre con "Aprender este conocimiento").
        /// Sin lector (VR, escena sin UI) se aprende directamente como antes.
        /// </summary>
        public void Press()
        {
            var info = Info;
            if (info == null) return;
            if (!UI.BookReaderUI.Open(info.Title, BuildPages(info), Learn)) Learn();
        }

        private List<UI.ReaderPage> BuildPages(BookInfo info)
        {
            var lore = Discovery.Instance.Lore;
            var state = Discovery.Instance.State;
            var pages = new List<UI.ReaderPage>(info.Glyphs.Count + 2);
            var contents = new StringBuilder();
            foreach (var g in info.Glyphs) contents.Append("·  ").Append(lore.Name(g)).Append('\n');
            pages.Add(new UI.ReaderPage
            {
                CoverTitle = info.Title,
                CoverSubtitle = info.Glyphs.Count == 1 ? "Un glifo explicado" : $"{info.Glyphs.Count} glifos explicados",
                Kicker = "Contenido",
                Heading = info.Title,
                Body = contents.ToString().TrimEnd(),
                Note = state.HasRead(bookId) ? "Ya leíste este libro." : null,
            });
            foreach (var g in info.Glyphs)
                pages.Add(new UI.ReaderPage
                {
                    GlyphId = g,
                    Heading = lore.Name(g),
                    Body = lore.Text(g),
                    Note = state.Knows(g) ? "Ya conocías este glifo." : null,
                });
            int unknown = 0;
            foreach (var g in info.Glyphs) if (!state.Knows(g)) unknown++;
            pages.Add(new UI.ReaderPage
            {
                CoverTitle = "Colofón",
                CoverSubtitle = "Fin del tratado",
                Kicker = "Para aprender",
                Heading = unknown > 0 ? "Copia los glifos en tu grimorio" : "Nada nuevo en estas páginas",
                Body = unknown > 0
                    ? $"Al aprender este conocimiento, {(unknown == 1 ? "el glifo nuevo pasa" : $"los {unknown} glifos nuevos pasan")} a tu grimorio (Tab). Desde ahí podés repasarlos y cargarlos en una estampa."
                    : "Ya conocías todos los glifos de este libro. Podés repasarlos en tu grimorio (Tab).",
            });
            return pages;
        }

        private void Learn()
        {
            var info = Info;
            if (info == null || Discovery.Instance == null) return;
            int learned = Discovery.Instance.Read(info);
            var names = string.Join(", ", info.Glyphs.ConvertAll(g => Discovery.Instance.Lore.Name(g)));
            DiagnosticBus.OnDiagnosticsUpdated?.Invoke(learned > 0
                ? $"<b>[Libro] {info.Title}</b>\nAprendiste: {names}. Abri el grimorio (Tab) para verlos."
                : $"<b>[Libro] {info.Title}</b>\nYa conocias: {names}.");
            BookRead?.Invoke(this, learned);
        }
    }
}
