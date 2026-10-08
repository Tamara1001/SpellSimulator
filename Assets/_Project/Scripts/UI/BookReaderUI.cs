using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using SpellSimulator.Glyphs;
using SpellSimulator.Player;

namespace SpellSimulator.UI
{
    /// <summary>
    /// Lector de libros modal (UI Toolkit): un libro abierto a pantalla, de a doble pagina. La izquierda ilustra el glifo
    /// (trazos de plantilla del diccionario, <see cref="GlyphSketch"/>) o la portada; la derecha lleva el texto. Las paginas
    /// se pasan con las flechas, los botones laterales o haciendo clic en la hoja; en la ultima aparece "Aprender este
    /// conocimiento", que llama a la accion del libro y lo cierra. Mientras esta abierto el cursor queda libre y la vista
    /// quieta (<see cref="FocusMode"/>); Esc o "Cerrar" lo cierran. En VR no existe: <see cref="Open"/> devuelve false y el
    /// que llama resuelve sin lector.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class BookReaderUI : MonoBehaviour
    {
        [SerializeField, Tooltip("Diccionario de glifos: trazos de plantilla y tipo (sigilo/signo) de cada ilustracion.")]
        private GlyphDictionaryAsset dictionary;
        [SerializeField, Tooltip("Flechas del teclado / cruceta para pasar de pagina.")]
        private PlayerInputReader input;

        private const int TurnMilliseconds = 140;
        private const int CloseMilliseconds = 250;

        public static BookReaderUI Instance { get; private set; }

        /// <summary>True mientras el libro esta en pantalla (la HUD se oculta).</summary>
        public static bool IsOpen => Instance != null && Instance.open;

        /// <summary>Frame en que se cerro el lector por ultima vez (para que Esc no cierre y abra el grimorio a la vez).</summary>
        public static int ClosedFrame { get; private set; } = -1;

        /// <summary>Se abrio o se cerro el lector.</summary>
        public static event Action<bool> OpenChanged;

        private UIDocument document;
        private VisualElement reader, leftContent, rightContent, sketchHost, note, coverRule;
        private Label leftKicker, coverTitle, coverSubtitle, caption, rightKicker, heading, body, noteText, folioLeft, folioRight, counter;
        private Button learn, prev, next;
        private GlyphSketch sketch;
        private bool bound;

        private readonly List<ReaderPage> pages = new List<ReaderPage>();
        private string title;
        private Action onLearn;
        private int index;
        private bool open;
        private bool enteredFocus;
        private bool turning;
        // Temporizadores en tiempo real (no del scheduler del panel): pasar la hoja y ocultar al cerrar.
        private float turnAt = -1f, hideAt = -1f, revealAt = -1f;

        private void Awake()
        {
            Instance = this;
            document = GetComponent<UIDocument>();
            if (input == null) input = FindFirstObjectByType<PlayerInputReader>();
        }

        private void OnEnable() => bound = false;

        private void OnDisable()
        {
            if (open) Close();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------------------------------------------------------------- API

        /// <summary>
        /// Abre un libro. Con el mismo libro ya abierto lo cierra (E abre y cierra). Devuelve false si no hay lector
        /// (VR o escena sin UI): el que llama aplica <paramref name="onLearn"/> directamente.
        /// </summary>
        public static bool Open(string title, IReadOnlyList<ReaderPage> pages, Action onLearn)
        {
            if (Instance == null || PlatformDetector.IsVR || pages == null || pages.Count == 0) return false;
            return Instance.Show(title, pages, onLearn);
        }

        public static void CloseReader()
        {
            if (Instance != null) Instance.Close();
        }

        // ---------------------------------------------------------------- armado

        private bool Bind()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            reader = root?.Q("reader");
            if (reader == null) return false;
            leftContent = reader.Q("left-content");
            rightContent = reader.Q("right-content");
            sketchHost = reader.Q("sketch-host");
            note = reader.Q("right-note");
            coverRule = reader.Q("cover-rule");
            leftKicker = reader.Q<Label>("left-kicker");
            coverTitle = reader.Q<Label>("cover-title");
            coverSubtitle = reader.Q<Label>("cover-subtitle");
            caption = reader.Q<Label>("left-caption");
            rightKicker = reader.Q<Label>("right-kicker");
            heading = reader.Q<Label>("right-heading");
            body = reader.Q<Label>("right-body");
            noteText = reader.Q<Label>("right-note-text");
            folioLeft = reader.Q<Label>("folio-left");
            folioRight = reader.Q<Label>("folio-right");
            counter = reader.Q<Label>("counter");
            learn = reader.Q<Button>("learn");
            prev = reader.Q<Button>("prev");
            next = reader.Q<Button>("next");

            sketchHost.Clear();
            sketch = new GlyphSketch();
            sketch.style.flexGrow = 1;
            sketchHost.Add(sketch);

            learn.clicked += Learn;
            prev.clicked += () => Step(-1);
            next.clicked += () => Step(1);
            reader.Q<Button>("close").clicked += Close;
            reader.Q("reader-backdrop").RegisterCallback<ClickEvent>(_ => Close());
            // Clic en la hoja: la izquierda vuelve, la derecha avanza (salvo sobre un boton).
            reader.Q("page-left").RegisterCallback<ClickEvent>(e => { if (!(e.target is Button)) Step(-1); });
            reader.Q("page-right").RegisterCallback<ClickEvent>(e => { if (!IsInsideButton(e.target as VisualElement)) Step(1); });

            reader.AddToClassList("reader--hidden");
            return true;
        }

        private static bool IsInsideButton(VisualElement e)
        {
            for (; e != null; e = e.parent) if (e is Button) return true;
            return false;
        }

        // ---------------------------------------------------------------- abrir / cerrar

        private bool Show(string bookTitle, IReadOnlyList<ReaderPage> source, Action learnAction)
        {
            if (!bound) bound = Bind();
            if (!bound) return false;
            if (open && bookTitle == title) { Close(); return true; }

            title = bookTitle;
            onLearn = learnAction;
            pages.Clear();
            for (int i = 0; i < source.Count; i++) pages.Add(source[i]);
            index = 0;

            if (!open)
            {
                open = true;
                enteredFocus = !FocusMode.Active;
                if (enteredFocus) FocusMode.Enter();
                hideAt = -1f;
                reader.RemoveFromClassList("reader--hidden");
                reader.AddToClassList("reader--fading");
                revealAt = Time.unscaledTime + 0.02f;
                OpenChanged?.Invoke(true);
            }
            Render();
            return true;
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            ClosedFrame = Time.frameCount;
            onLearn = null;
            title = null;
            if (enteredFocus) FocusMode.Exit();
            enteredFocus = false;
            reader.AddToClassList("reader--fading");
            hideAt = Time.unscaledTime + CloseMilliseconds / 1000f;
            turning = false;
            turnAt = -1f;
            OpenChanged?.Invoke(false);
        }

        private void Learn()
        {
            Action action = onLearn;
            Close();
            action?.Invoke();
        }

        // ---------------------------------------------------------------- paginas

        private void Update()
        {
            if (!bound) { bound = Bind(); if (!bound) return; }
            float now = Time.unscaledTime;
            if (hideAt > 0f && now >= hideAt) { hideAt = -1f; if (!open) reader.AddToClassList("reader--hidden"); }
            if (!open) return;
            if (revealAt > 0f && now >= revealAt) { revealAt = -1f; reader.RemoveFromClassList("reader--fading"); }
            if (turning && now >= turnAt) FinishTurn();
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { Close(); return; }
            int delta = input != null ? input.PageDelta : 0;
            if (delta == 0 && keyboard != null)
            {
                if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.pageDownKey.wasPressedThisFrame) delta = 1;
                else if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.pageUpKey.wasPressedThisFrame) delta = -1;
            }
            if (delta != 0) Step(delta);
        }

        /// <summary>Pasa a la doble pagina siguiente (+1) o anterior (-1). Publico para el harness.</summary>
        public void Turn(int delta) => Step(delta);

        private void Step(int delta)
        {
            if (!open) return;
            int target = Mathf.Clamp(index + (delta > 0 ? 1 : -1), 0, pages.Count - 1);
            if (target == index) return;
            index = target;
            // Varias vueltas seguidas: la hoja ya se esta pasando, al terminar muestra la ultima.
            if (turning) return;
            // Pasar la hoja: el contenido se apaga, cambia y vuelve.
            turning = true;
            leftContent.AddToClassList("page-content--turning");
            rightContent.AddToClassList("page-content--turning");
            turnAt = Time.unscaledTime + TurnMilliseconds / 1000f;
        }

        private void FinishTurn()
        {
            turning = false;
            Render();
            leftContent.RemoveFromClassList("page-content--turning");
            rightContent.RemoveFromClassList("page-content--turning");
        }

        private void Render()
        {
            ReaderPage page = pages[index];
            bool last = index == pages.Count - 1;
            GlyphEntry entry = !string.IsNullOrEmpty(page.GlyphId) && dictionary != null ? dictionary.Dictionary.Get(page.GlyphId) : null;
            string kind = entry == null ? null : entry.Kind == GlyphKind.Sigil ? "SIGILO" : entry.Kind == GlyphKind.Sign ? "SIGNO" : null;

            // Izquierda: glifo, portada u ornamento.
            bool isCover = entry == null && !string.IsNullOrEmpty(page.CoverTitle);
            SetText(leftKicker, isCover ? "TRATADO" : null);
            SetText(coverTitle, isCover ? page.CoverTitle : null);
            SetText(coverSubtitle, isCover ? page.CoverSubtitle : null);
            coverRule.style.display = isCover ? DisplayStyle.Flex : DisplayStyle.None;
            SetText(caption, entry != null ? page.Heading : null);
            sketchHost.EnableInClassList("sketch-host--small", entry == null);
            sketch.SetStrokes(entry != null ? entry.TemplateStrokes : null);

            // Derecha: texto.
            string kicker = !string.IsNullOrEmpty(page.Kicker) ? page.Kicker : kind;
            SetText(rightKicker, kicker != null ? kicker.ToUpperInvariant() : null);
            SetText(heading, page.Heading);
            SetText(body, page.Body);
            noteText.text = page.Note ?? "";
            note.style.display = string.IsNullOrEmpty(page.Note) ? DisplayStyle.None : DisplayStyle.Flex;
            learn.style.display = last && onLearn != null ? DisplayStyle.Flex : DisplayStyle.None;

            folioLeft.text = $"— {index * 2 + 1} —";
            folioRight.text = $"— {index * 2 + 2} —";
            counter.text = $"{title?.ToUpperInvariant()}   ·   {index + 1} / {pages.Count}";
            prev.EnableInClassList("nav--disabled", index == 0);
            next.EnableInClassList("nav--disabled", last);
        }

        private static void SetText(Label label, string text)
        {
            label.text = text ?? "";
            label.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
