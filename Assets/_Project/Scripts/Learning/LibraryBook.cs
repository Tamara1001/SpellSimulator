using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SpellSimulator.DebugTools;
using SpellSimulator.Interfaces;
using SpellSimulator.Player;

namespace SpellSimulator.Learning
{
    /// <summary>
    /// Libro de la biblioteca: E lo abre en el lector de libros (UI Toolkit) y su ultima pagina ensena los glifos, que
    /// aparecen en el grimorio. En VR (sin lector) los ensena al apretarlo.
    /// </summary>
    public class LibraryBook : MonoBehaviour, IPressable, IHudInfo
    {
        /// <summary>(libro, glifos nuevos) al leerlo.</summary>
        public static event Action<LibraryBook, int> BookRead;

        [SerializeField] private string bookId;

        public string BookId => bookId;

        public void SetBook(string id) => bookId = id;

        private BookInfo info;

        private BookInfo Info => info ??= Discovery.Instance != null ? Discovery.Instance.Lore.Book(bookId) : null;

        private string readPrompt, rereadPrompt;

        // ---- Numeracion (B2): "Libro X de Y" y cuantos faltan por leer.
        private string hudName;
        private int hudUnread = -1;
        private Light glow;
        private float glowPhase;
        private const float GlowMin = 0.25f, GlowMax = 0.9f, GlowSpeed = 1.6f;

        /// <summary>Numero de este libro (1..Total) segun el orden de glifos.json.</summary>
        public int Number
        {
            get
            {
                var lore = Discovery.Instance != null ? Discovery.Instance.Lore : null;
                return lore == null ? 0 : lore.Books.FindIndex(b => b.Id == bookId) + 1;
            }
        }

        /// <summary>Total de libros de conocimiento.</summary>
        public int Total => Discovery.Instance != null ? Discovery.Instance.Lore.Books.Count : 0;

        /// <summary>Libros que el jugador todavia no leyo.</summary>
        public static int CountUnread()
        {
            var d = Discovery.Instance;
            if (d == null) return 0;
            int n = 0;
            for (int i = 0; i < d.Lore.Books.Count; i++) if (!d.State.HasRead(d.Lore.Books[i].Id)) n++;
            return n;
        }

        public string HudName
        {
            get
            {
                var info = Info;
                if (info == null) return "Libro";
                int unread = CountUnread();
                if (hudName == null || unread != hudUnread)
                {
                    hudUnread = unread;
                    hudName = unread > 0
                        ? $"Libro {Number} de {Total}: {info.Title} (faltan {unread} por leer)"
                        : $"Libro {Number} de {Total}: {info.Title} (leíste todos)";
                }
                return hudName;
            }
        }

        public Color HudColor => Info != null ? Info.Color : new Color(0.55f, 0.35f, 0.2f);
        public string HudHints => "E: leer";
        public float HudGauge => -1f;

        // ---- Brillo sutil para que se note que se puede leer: una luz calida que late despacio.
        private void Awake()
        {
            var go = new GameObject("BrilloLibro");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            glow = go.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.82f, 0.5f);
            glow.range = 0.9f;
            glow.intensity = GlowMin;
            glow.shadows = LightShadows.None;
            glowPhase = Mathf.Abs(transform.position.x * 3.1f + transform.position.z * 1.7f);
        }

        private void Update()
        {
            if (glow == null) return;
            float k = 0.5f + 0.5f * Mathf.Sin(Time.time * GlowSpeed + glowPhase);
            glow.intensity = Mathf.Lerp(GlowMin, GlowMax, k);
        }

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
                CoverSubtitle = $"Libro {Number} de {Total}  ·  " + (info.Glyphs.Count == 1 ? "un glifo explicado" : $"{info.Glyphs.Count} glifos explicados"),
                Kicker = "Contenido",
                Heading = info.Title,
                Body = contents.ToString().TrimEnd(),
                Note = (state.HasRead(bookId) ? "Ya leíste este libro. " : "") + (CountUnread() > 0 ? $"Faltan {CountUnread()} de {Total} libros por leer." : "Leíste todos los libros."),
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
