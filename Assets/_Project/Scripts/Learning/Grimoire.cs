using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Glyphs;
using SpellSimulator.Interfaces;
using SpellSimulator.Player;
using SpellSimulator.Rendering;

namespace SpellSimulator.Learning
{
    /// <summary>
    /// Pantallas del grimorio. La portada (Menu) es un indice: desde ahi se llega a cada seccion en cualquier orden.
    /// </summary>
    public enum GrimoireScreen { Menu, Magic, GlyphIndex, GlyphDetail, Seals, Controls, Options, Spells, SpellDetail, Pause, Portals }

    /// <summary>
    /// Libro-menu diegetico (SYSTEMS_DESIGN 9). Tab / Y / menu izquierdo lo abre flotando frente al jugador, un poco
    /// por encima de la barra de objetos. Portada = indice (Magia, Glifos, Sellos, Controles, Opciones); cada seccion
    /// tiene boton "Menu" y paginacion. Los botones se aprietan apuntando + clic; flechas / rueda pasan paginas.
    /// No se cierra al alejarse. E sobre cualquier parte que no sea un boton lo agarra y el movedor lo arrastra
    /// (Rigidbody cinematico + <see cref="IRemoteGrabbable"/>); al soltarlo queda flotando. Con B se fija delante de la
    /// camara a pantalla casi completa.
    /// </summary>
    public class Grimoire : MonoBehaviour, IRemoteGrabbable
    {
        public static event Action Opened;
        public static event Action Closed;
        public static event Action PageTurned;
        /// <summary>El jugador pidio reiniciar el mundo desde el libro.</summary>
        public static event Action ResetWorldRequested;
        /// <summary>"Menú principal" en Opciones (lo escucha UI.MainMenu; sin oyente el boton no aparece).</summary>
        public static event Action MainMenuRequested;

        [SerializeField] private PlayerInputReader input;
        [SerializeField] private GlyphDictionaryAsset dictionary;
        [SerializeField, Tooltip("Hechizos (Data/Spells/*.json). Los asigna SpellSimulator > Learning > Setup Grimoire; en el Editor, si queda vacio, se leen de la carpeta.")]
        private TextAsset[] spellFiles;
        [SerializeField, Tooltip("Portales (Data/Portals/portals.json), para la seccion Portales. Lo asigna Setup Grimoire.")]
        private TextAsset portalsFile;
        [Header("Materiales (SpellSimulator > Learning > Setup Grimoire)")]
        [SerializeField, Tooltip("Pagina izquierda (lienzo del glifo animado).")] private Material pageMaterial;
        [SerializeField, Tooltip("Pagina derecha (pergamino espejado).")] private Material pageRightMaterial;
        [SerializeField] private Material brushMaterial;
        [SerializeField, Tooltip("Cuero de tapas y lomo.")] private Material coverMaterial;
        [SerializeField] private Material goldMaterial;
        [SerializeField, Tooltip("Canto de las hojas (pila de paginas).")] private Material edgeMaterial;
        [SerializeField, Tooltip("Cinta marcapaginas y correa.")] private Material ribbonMaterial;
        [SerializeField] private Material waxMaterial;
        [SerializeField] private Material buttonMaterial;
        [SerializeField] private Material buttonHoverMaterial;
        [SerializeField, Tooltip("Filigrana bajo los titulos.")] private Material ornamentMaterial;
        [SerializeField, Tooltip("Emblema dorado de la contratapa.")] private Material emblemMaterial;
        [SerializeField, Tooltip("Fondo del lienzo si la pagina no usa el shader de pergamino.")]
        private Color parchment = new Color(0.934f, 0.875f, 0.639f);
        [SerializeField, Tooltip("Puntos de trazo dibujados por segundo en la animacion.")]
        private float strokeSpeed = 160f;
        [SerializeField, Tooltip("Distancia horizontal a la que aparece al abrirlo (m).")]
        private float openDistance = 0.72f;
        [SerializeField, Tooltip("Cuanto mas abajo que los ojos queda el centro del libro al abrirlo (m).")]
        private float openDrop = 0.07f;
        [SerializeField, Tooltip("Inclinacion hacia atras al abrirlo (grados, como sobre un atril).")]
        private float openTilt = 22f;

        // Medidas de una pagina (m) y del libro.
        private const float PageWidth = 0.27f, PageHeight = 0.36f;
        private const float HalfW = PageWidth / 2f, HalfH = PageHeight / 2f;
        /// <summary>Angulo de cada mitad respecto del plano (el libro abierto hace una V suave).</summary>
        private const float SpreadAngle = 7f;
        private const float CoverMargin = 0.016f, CoverThickness = 0.008f, StackDepth = 0.018f;
        /// <summary>Fraccion de la pantalla que ocupa el libro en modo fijo.</summary>
        private const float FocusFill = 0.9f;

        // Diagramacion de una pagina (coordenadas locales de la pagina, origen en su centro).
        private const float ContentWidth = PageWidth - 0.05f;
        private const float TitleY = 0.146f, OrnamentY = 0.121f, BodyTop = 0.111f, BodyBottom = -0.138f;
        private const float BarY = -0.155f, BarHeight = 0.03f;
        private const float TitleSize = 0.24f, BodySize = 0.135f, ButtonSize = 0.118f, SmallSize = 0.098f;

        /// <summary>Glifos por pagina del indice (2 columnas x 6 filas).</summary>
        private const int IndexColumns = 2, IndexRows = 6, IndexPageSize = IndexColumns * IndexRows;
        private const int ButtonPool = 20, LabelPool = 20, OrnamentPool = 4;
        private const int Left = 0, Right = 1;

        /// <summary>Distancia (m) a la que un grimorio suelto vuelve al inventario (tras GrimoireMath.ReturnSeconds).</summary>
        public const float ReturnDistance = 12f;
        private const float ReturnCheckInterval = 0.5f;
        private float nextReturnCheck, awaySeconds;

        private Transform root;
        private Rigidbody body;
        private readonly Transform[] pages = new Transform[2];
        private readonly TextMeshPro[] titles = new TextMeshPro[2];
        private readonly TextMeshPro[] bodies = new TextMeshPro[2];
        private DrawingCanvas leftCanvas;
        private readonly List<Collider> grabColliders = new List<Collider>();
        private Camera viewer;
        private PlayerInteractor interactor;
        private ObjectMover mover;

        // Pantalla actual y datos de navegacion.
        private GrimoireScreen screen;
        private string glyph;                               // glifo de la pantalla GlyphDetail
        private readonly List<string> glyphs = new List<string>(); // glifos descubiertos, en el orden del indice
        private int indexPage;                              // pagina del indice de glifos
        private int spread;                                 // hoja de Magia / Sellos / Controles

        // Hechizos (ADR-034): catalogo del proyecto y los desbloqueados (inicial o de un libro leido).
        private SpellCatalog catalog;
        private readonly List<SpellDef> spells = new List<SpellDef>();
        private SpellDef spell;                             // hechizo de la pantalla SpellDetail
        private int spellPage;                              // pagina de la lista de hechizos
        private Func<string, bool> bookRead;
        private const int SpellsPerPage = 7;

        // Hechizos propios (ADR-034, H5): biblioteca guardada, ultimo lanzamiento, renombrar con el teclado y borrar con doble clic.
        private SpellLibrary library;
        private SpellCaster spellCaster;
        private bool renaming;
        private string renameText = "";
        private InputGate gateBeforeRename;
        private bool deleteArmed;
        private float deleteArmedAt;
        private const float DeleteConfirmSeconds = 3f;

        // Pools fijos (creados en Build): cada pantalla reutiliza los primeros y oculta el resto.
        private GrimoireButton[] buttons;
        private TextMeshPro[] labels;
        private Transform[] ornaments;
        private int buttonsUsed, labelsUsed, ornamentsUsed;
        private GrimoireButton hoveredButton;
        // Confirmacion del reinicio del mundo (PauseMenuMath): segundo clic dentro de ConfirmSeconds.
        private float resetArmedAt = PauseMenuMath.Disarmed;
        private bool resetArmed;

        // Modo fijo (B): el libro se parenta a la camara y se guarda la pose para volver.
        private bool focused;
        private Transform savedParent;
        private Vector3 savedPosition;
        private Quaternion savedRotation;
        private bool wasUsingTool;

        // Arrastrado por el movedor (E).
        private bool remoteHeld;

        // Animacion del trazo.
        private readonly List<(Vector2 uv, bool start, float width)> strokeQueue = new List<(Vector2, bool, float)>();
        private int strokeIndex;
        private float strokeBudget;
        // Puntos por segundo de la animacion actual: se fija al primer frame para que cada glifo dure lo mismo (A3).
        private float strokeRate;
        private readonly List<Vector2> tmpSmooth = new List<Vector2>(512);
        private readonly List<Vector2> tmpPath = new List<Vector2>(512);
        private readonly List<Vector2> tmpResampled = new List<Vector2>(2048);

        public static Grimoire Instance { get; private set; }

        public bool IsOpen => root != null && root.gameObject.activeSelf;

        /// <summary>True si esta abierto en modo pantalla completa (fijo a la camara, B): ahi la HUD se oculta.</summary>
        public bool IsFullscreen => IsOpen && focused;

        /// <summary>Glifo de la pantalla de detalle abierta (null en cualquier otra). Lo usan las estampas para cargarse.</summary>
        public string CurrentGlyph => IsOpen && screen == GrimoireScreen.GlyphDetail ? glyph : null;

        /// <summary>True si el collider es parte del libro (paginas, botones, agarre).</summary>
        public bool Owns(Collider c) => c != null && root != null && c.transform.IsChildOf(root);

        /// <summary>True mientras el movedor lo arrastra.</summary>
        public bool IsHeld => remoteHeld;

        public GrimoireScreen CurrentScreen => screen;

        private void Awake() => Instance = this;

        private void Start()
        {
            if (input != null)
            {
                interactor = input.GetComponent<PlayerInteractor>();
                mover = input.GetComponent<ObjectMover>();
            }
            GrimoireOptions.ApplyMaster();
            LoadSpells();
            Build();
            root.gameObject.SetActive(false);
            Discovery.Discovered += OnDiscovered;
        }

        private void OnDestroy()
        {
            Discovery.Discovered -= OnDiscovered;
            if (focused) FocusMode.Exit();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (input == null) return;
            // TAB o ESC abren/cierran el grimorio. ESC no actua si el lector de libros esta abierto o acaba de cerrarse
            // con esa misma pulsacion (el lector se encarga de ESC primero).
            bool escape = input.IsPressingEscape && !SpellSimulator.UI.BookReaderUI.IsOpen
                && Time.frameCount != SpellSimulator.UI.BookReaderUI.ClosedFrame;
            if (input.IsTogglingBook)
            {
                if (IsOpen) Close();
                else Open();
            }
            else if (escape)
            {
                // Esc: cerrado -> se abre a pantalla completa; en la mano -> pasa a pantalla completa; a pantalla -> se cierra.
                if (!IsOpen) { Open(); Show(GrimoireScreen.Pause); EnterFocus(); }
                else if (!focused) EnterFocus();
                else Close();
            }
            if (!IsOpen) { awaySeconds = 0f; return; }
            CheckReturnToInventory();

            // Flanco de subida del clic: en modo fijo presiona el boton bajo el puntero.
            bool usingTool = input.IsUsingTool;
            bool pressed = usingTool && !wasUsingTool;
            wasUsingTool = usingTool;
            if (focused && pressed) PressUnderPointer();

            // Teclado: Z / X, flechas o cruceta (la rueda ya no pasa paginas).
            int delta = input.PageDelta;
            if (delta != 0) Step(delta);

            // La confirmacion del reinicio vence sola: el boton vuelve a su rotulo normal.
            if (resetArmed && !PauseMenuMath.IsArmed(resetArmedAt, Time.unscaledTime))
            {
                resetArmed = false;
                resetArmedAt = PauseMenuMath.Disarmed;
                if (screen == GrimoireScreen.Pause || screen == GrimoireScreen.Options) Refresh();
            }

            if (renaming) UpdateRename();
            if (deleteArmed && Time.unscaledTime - deleteArmedAt > DeleteConfirmSeconds)
            {
                deleteArmed = false;
                if (screen == GrimoireScreen.SpellDetail) Refresh();
            }

            UpdateHover();
            AnimateStroke();
        }

        /// <summary>
        /// Libro suelto (flotando en el mundo: ni arrastrado, ni en la mano de VR, ni a pantalla completa) y lejos del
        /// jugador: tras unos segundos se oculta y vuelve al inventario (T lo abre de nuevo). Chequeo cada 0.5 s.
        /// </summary>
        private void CheckReturnToInventory()
        {
            if (Time.time < nextReturnCheck) return;
            float dt = nextReturnCheck <= 0f ? ReturnCheckInterval : Time.time - (nextReturnCheck - ReturnCheckInterval);
            nextReturnCheck = Time.time + ReturnCheckInterval;
            if (viewer == null) viewer = Camera.main;
            if (viewer == null || root == null) return;

            float distance = Vector3.Distance(viewer.transform.position, root.position);
            bool held = remoteHeld || root.parent != transform;
            bool inHand = focused;
            awaySeconds = distance > ReturnDistance && !held && !inHand ? awaySeconds + dt : 0f;
            if (!GrimoireMath.ShouldReturn(distance, held, inHand, awaySeconds)) return;

            awaySeconds = 0f;
            Close();
            Hud.Notify("book", "El grimorio volvio a tu inventario (T)", 3f);
        }

        // ---------------------------------------------------------------- abrir / cerrar

        public void Open()
        {
            if (viewer == null) viewer = Camera.main;
            if (viewer == null) return;
            Transform cam = viewer.transform;
            root.SetParent(transform, true); // por si se cerro mientras estaba en la mano
            Vector3 flat = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            if (flat.sqrMagnitude < 0.01f) flat = Vector3.forward;
            // A la altura de la vista y un poco abajo: la barra de objetos (abajo al centro) no lo tapa.
            root.position = cam.position + flat * openDistance + Vector3.down * openDrop;
            // Abierto hacia el jugador, inclinado como sobre un atril.
            root.rotation = Quaternion.LookRotation(flat, Vector3.up) * Quaternion.Euler(openTilt, 0f, 0f);
            root.gameObject.SetActive(true);
            wasUsingTool = input != null && input.IsUsingTool;
            RefreshGlyphs();
            RefreshSpells();
            // Si el glifo abierto ya no esta descubierto (se olvido todo), vuelve al menu.
            GrimoireScreen s = screen;
            string g = glyph;
            if (s == GrimoireScreen.GlyphDetail && !glyphs.Contains(g)) { s = GrimoireScreen.Menu; g = null; }
            if (s == GrimoireScreen.SpellDetail && (spell == null || !spells.Contains(spell))) { s = GrimoireScreen.Spells; g = null; }
            Show(s, g);
            Opened?.Invoke();
        }

        /// <summary>Abre el libro directamente fijo a la camara (pantalla completa). Lo usa "Información" del menu principal.</summary>
        public void OpenFullscreen()
        {
            if (!IsOpen) Open();
            if (IsOpen && !focused) EnterFocus();
        }

        public void Close()
        {
            if (renaming) EndRename(false);
            if (!IsOpen) return;
            if (remoteHeld && mover != null) mover.End(true);
            remoteHeld = false;
            ExitFocus();
            SetHover(null);
            root.gameObject.SetActive(false);
            Closed?.Invoke();
        }

        // ---------------------------------------------------------------- agarre (movedor)

        public bool DriveKinematic => true;

        public void OnRemoteGrab()
        {
            if (focused) ExitFocus();
            remoteHeld = true;
            if (body != null) body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        /// <summary>Con pin o sin pin queda flotando donde esta: es un libro, no cae.</summary>
        public void OnRemoteRelease(bool pin)
        {
            remoteHeld = false;
            if (body != null) body.interpolation = RigidbodyInterpolation.None;
        }

        // ---------------------------------------------------------------- modo fijo (B)

        /// <summary>
        /// Fija el libro delante de la camara, centrado y del tamano que lo deja ocupar ~90% de la pantalla.
        /// Guarda la pose anterior para restaurarla al salir.
        /// </summary>
        private void EnterFocus()
        {
            if (viewer == null) viewer = Camera.main;
            if (viewer == null) return;
            if (remoteHeld && mover != null) mover.End(true);
            savedParent = root.parent;
            savedPosition = root.position;
            savedRotation = root.rotation;
            root.SetParent(viewer.transform, false);
            // Con identity el libro queda de cara a la camara (su cara visible es -Z local, igual que los quads).
            root.localPosition = new Vector3(0f, 0f, FocusDistance(viewer));
            root.localRotation = Quaternion.identity;
            // Mientras esta fijo no se puede agarrar (E podria arrancarlo de la camara).
            SetGrabbable(false);
            focused = true;
            FocusMode.Enter();
        }

        private void ExitFocus()
        {
            if (!focused) return;
            focused = false;
            SetGrabbable(true);
            Transform parent = savedParent != null ? savedParent : transform;
            root.SetParent(parent, true);
            root.SetPositionAndRotation(savedPosition, savedRotation);
            FocusMode.Exit();
        }

        private void SetGrabbable(bool on)
        {
            for (int i = 0; i < grabColliders.Count; i++) grabColliders[i].enabled = on;
        }

        /// <summary>Distancia para que el libro (con tapas) ocupe ~90% de la vista, limitada a 0.3..1.2 m.</summary>
        private static float FocusDistance(Camera cam)
        {
            float halfTan = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float byHeight = (PageHeight + 2f * CoverMargin + 0.02f) / FocusFill / (2f * halfTan);
            float byWidth = (2f * PageWidth + 2f * CoverMargin + 0.06f) / FocusFill / (2f * halfTan * cam.aspect);
            return Mathf.Clamp(Mathf.Max(byHeight, byWidth), 0.3f, 1.2f);
        }

        /// <summary>Raycast con el puntero del mouse (o la mira en VR) y presiona el primer IPressable que toca.</summary>
        private void PressUnderPointer()
        {
            if (viewer == null) return;
            Ray ray = FocusMode.GetRay(viewer);
            if (!Physics.Raycast(ray, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Collide)) return;
            var pressable = hit.collider.GetComponentInParent<IPressable>();
            pressable?.Press();
        }

        /// <summary>Resalta el boton apuntado: el del puntero en modo fijo, o el que mira el interactor.</summary>
        private void UpdateHover()
        {
            GrimoireButton target = null;
            if (focused)
            {
                if (viewer != null && Physics.Raycast(FocusMode.GetRay(viewer), out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Collide))
                    hit.collider.TryGetComponent(out target);
            }
            else if (interactor != null)
            {
                GameObject go = interactor.HoveredObject;
                if (go != null) go.TryGetComponent(out target);
            }
            if (target != null && !Owns(target.GetComponent<Collider>())) target = null;
            SetHover(target);
        }

        private void SetHover(GrimoireButton target)
        {
            if (target == hoveredButton) return;
            if (hoveredButton != null) hoveredButton.SetHovered(false);
            hoveredButton = target;
            if (hoveredButton != null) hoveredButton.SetHovered(true);
        }

        // ---------------------------------------------------------------- navegacion

        /// <summary>Flechas / rueda: pasa de glifo, de pagina del indice o de hoja de la seccion.</summary>
        private void Step(int delta)
        {
            switch (screen)
            {
                case GrimoireScreen.GlyphDetail:
                {
                    int i = glyphs.IndexOf(glyph);
                    int n = i + delta;
                    if (i >= 0 && n >= 0 && n < glyphs.Count) Go(GrimoireScreen.GlyphDetail, glyphs[n]);
                    break;
                }
                case GrimoireScreen.GlyphIndex:
                {
                    int next = indexPage + delta;
                    if (next >= 0 && next < IndexPageCount()) { indexPage = next; Go(GrimoireScreen.GlyphIndex); }
                    break;
                }
                case GrimoireScreen.SpellDetail:
                {
                    int i = spells.IndexOf(spell);
                    int n = i + delta;
                    if (i >= 0 && n >= 0 && n < spells.Count) { spell = spells[n]; spellPage = n / SpellsPerPage; Go(GrimoireScreen.SpellDetail); }
                    break;
                }
                case GrimoireScreen.Spells:
                {
                    int next = spellPage + delta;
                    if (next >= 0 && next < SpellPageCount()) { spellPage = next; Go(GrimoireScreen.Spells); }
                    break;
                }
                case GrimoireScreen.Magic:
                case GrimoireScreen.Seals:
                case GrimoireScreen.Controls:
                {
                    int next = spread + delta;
                    if (next >= 0 && next < Spreads(screen).Length) { spread = next; Go(screen); }
                    break;
                }
            }
        }

        /// <summary>Cambia de pantalla y avisa (sonido de pasar pagina).</summary>
        private void Go(GrimoireScreen next, string id = null)
        {
            if (next != screen && (next == GrimoireScreen.Magic || next == GrimoireScreen.Seals || next == GrimoireScreen.Controls)) spread = 0;
            Show(next, id);
            PageTurned?.Invoke();
        }

        /// <summary>Abre una pantalla desde codigo (harness / tests). No suena.</summary>
        public void ShowScreen(GrimoireScreen next, int sheet = 0)
        {
            spread = Mathf.Max(0, sheet);
            indexPage = Mathf.Max(0, sheet);
            spellPage = Mathf.Max(0, sheet);
            if (next == GrimoireScreen.SpellDetail) { RefreshSpells(); spell = spells.Count > 0 ? spells[Mathf.Min(sheet, spells.Count - 1)] : null; if (spell == null) next = GrimoireScreen.Spells; }
            if (IsOpen) Show(next, next == GrimoireScreen.GlyphDetail && glyphs.Count > 0 ? glyphs[0] : null);
        }

        private static GrimoireSpread[] Spreads(GrimoireScreen s) =>
            s == GrimoireScreen.Magic ? GrimoireTexts.Magic : s == GrimoireScreen.Seals ? GrimoireTexts.Seals : GrimoireTexts.Controls;

        private int IndexPageCount() => Mathf.Max(1, Mathf.CeilToInt((glyphs.Count + CircleExtraSlots()) / (float)IndexPageSize));

        /// <summary>El Circulo Magico (siempre primero) ocupa 2 casillas de la grilla: 1 casilla extra.</summary>
        private int CircleExtraSlots() => glyphs.Count > 0 && glyphs[0] == Casting.StampPattern.MagicCircleId ? 1 : 0;

        /// <summary>Casilla de la grilla (0-based, 2 columnas) donde empieza el glifo k.</summary>
        private int SlotOf(int k) => k == 0 ? 0 : k + CircleExtraSlots();

        /// <summary>Glifos descubiertos: sigilos primero y despues signos, en el orden del diccionario.</summary>
        private void RefreshGlyphs()
        {
            glyphs.Clear();
            var discovery = Discovery.Instance;
            if (discovery == null || dictionary == null) return;
            foreach (var kind in new[] { GlyphKind.Sigil, GlyphKind.Sign })
                foreach (var entry in dictionary.Dictionary.Entries)
                    if (entry.Kind == kind && discovery.State.Knows(entry.Id) && entry.TemplateStrokes.Count > 0)
                        glyphs.Add(entry.Id);
            // El Circulo Magico va primero (arriba de todo) aunque no este en el modelo ONNX.
            int circle = glyphs.IndexOf(Casting.StampPattern.MagicCircleId);
            if (circle > 0) { glyphs.RemoveAt(circle); glyphs.Insert(0, Casting.StampPattern.MagicCircleId); }
        }

        private void OnDiscovered(string glyphId, string source)
        {
            if (!IsOpen) return;
            RefreshGlyphs();
            Show(screen, glyph);
        }

        // ---------------------------------------------------------------- pantallas

        /// <summary>Dibuja una pantalla: limpia la pagina izquierda, rotula los textos y reasigna botones y adornos.</summary>
        private void Show(GrimoireScreen s, string id = null)
        {
            screen = s;
            glyph = id;
            if (s != GrimoireScreen.Pause && s != GrimoireScreen.Options) { resetArmed = false; resetArmedAt = PauseMenuMath.Disarmed; }
            if (renaming && s != GrimoireScreen.SpellDetail) EndRename(false);
            if (s != GrimoireScreen.SpellDetail) deleteArmed = false;
            leftCanvas.Clear();
            strokeQueue.Clear();
            strokeIndex = 0;
            strokeBudget = 0f;
            strokeRate = 0f;
            HidePools();
            for (int side = 0; side < 2; side++)
            {
                titles[side].text = "";
                SetBody(side, "", BodyTop, BodyBottom);
            }

            switch (s)
            {
                case GrimoireScreen.Menu: ShowMenu(); return;
                case GrimoireScreen.Magic:
                case GrimoireScreen.Seals:
                case GrimoireScreen.Controls: ShowSpread(s); break;
                case GrimoireScreen.GlyphIndex: ShowIndex(); break;
                case GrimoireScreen.GlyphDetail: ShowDetail(id); break;
                case GrimoireScreen.Options: ShowOptions(); break;
                case GrimoireScreen.Pause: ShowPause(); break;
                case GrimoireScreen.Spells: ShowSpells(); break;
                case GrimoireScreen.Portals: ShowPortals(); break;
                case GrimoireScreen.SpellDetail: ShowSpellDetail(); break;
            }
            PlaceMenuButton();
        }

        private void ShowMenu()
        {
            var discovery = Discovery.Instance;
            int total = discovery != null ? CountLore(discovery.Lore) : glyphs.Count;

            // Izquierda: titulo, sello animado (doble anillo + un sigilo) y una leyenda.
            SetTitle(Left, "Grimorio");
            Label(Left, 0f, BodyTop - 0.008f, ContentWidth, 0.02f, $"<i><color={GrimoireTexts.Faded}>de sellos, sigilos y signos</color></i>", SmallSize, TextAlignmentOptions.Center);
            Vector2 center = new Vector2(0.5f, 0.45f);
            QueueRing(center, 0.088f, 0.0055f);
            QueueRing(center, 0.079f, 0.0028f);
            string emblem = FirstSigil();
            if (emblem != null) QueueTemplate(dictionary.Dictionary.Get(emblem).TemplateStrokes, center, 0.1f);
            PlaceOrnament(Left, 0f, -0.123f, 0.13f);
            Label(Left, 0f, BarY, ContentWidth, 0.024f, $"<color={GrimoireTexts.Faded}><i>T</i> cierra   ·   <i>Esc</i> lo acerca</color>", SmallSize, TextAlignmentOptions.Center);

            // Derecha: indice de secciones.
            SetTitle(Right, "Índice");
            float w = ContentWidth * 0.86f, h = 0.034f;
            string n = $"<color={GrimoireTexts.Gold}>";
            RefreshSpells();
            h = 0.028f;
            int portalsKnown = KnownPortals().Count;
            Place(Right, 0f, 0.108f, w, h, $"{n}I</color>   Magia", ButtonSize, () => Go(GrimoireScreen.Magic));
            Place(Right, 0f, 0.0785f, w, h, $"{n}II</color>   Glifos   <size=75%>({glyphs.Count} de {total})</size>", ButtonSize, () => Go(GrimoireScreen.GlyphIndex));
            Place(Right, 0f, 0.049f, w, h, $"{n}III</color>   Sellos", ButtonSize, () => Go(GrimoireScreen.Seals));
            Place(Right, 0f, 0.0195f, w, h, $"{n}IV</color>   Hechizos   <size=75%>({spells.Count})</size>", ButtonSize, () => Go(GrimoireScreen.Spells));
            Place(Right, 0f, -0.010f, w, h, $"{n}V</color>   Portales   <size=75%>({portalsKnown})</size>", ButtonSize, () => Go(GrimoireScreen.Portals));
            Place(Right, 0f, -0.0395f, w, h, $"{n}VI</color>   Controles", ButtonSize, () => Go(GrimoireScreen.Controls));
            Place(Right, 0f, -0.069f, w, h, $"{n}VII</color>   Opciones", ButtonSize, () => Go(GrimoireScreen.Options));
            Place(Right, 0f, -0.0985f, w, h, $"{n}VIII</color>   Pausa", ButtonSize, () => Go(GrimoireScreen.Pause));
            PlaceOrnament(Right, 0f, -0.145f, 0.1f);
        }

        private List<Casting.PortalDefinition> KnownPortals() =>
            PortalKnowledge.Known(PortalKnowledge.Shared, Discovery.Instance != null ? Discovery.Instance.Lore.Books : null, bookRead);

        /// <summary>Portales anotados por los libros de caminos leidos (solo informativo, ADR-035).</summary>
        private void ShowPortals()
        {
            List<Casting.PortalDefinition> known = KnownPortals();
            int total = Discovery.Instance != null ? PortalKnowledge.Total(PortalKnowledge.Shared, Discovery.Instance.Lore.Books) : known.Count;
            SetTitle(Left, "Portales");
            SetBody(Left, GrimoireTexts.PortalsIntro(known.Count, total), BodyTop, BodyBottom);
            SetTitle(Right, "Caminos anotados");
            if (known.Count == 0) { SetBody(Right, GrimoireTexts.NoPortals, BodyTop, BodyBottom); return; }
            var text = new System.Text.StringBuilder();
            for (int k = 0; k < known.Count; k++)
            {
                if (k > 0) text.Append("\n\n");
                text.Append(GrimoireTexts.PortalEntry(known[k].DisplayName, IdentityParts(known[k], true), IdentityParts(known[k], false)));
            }
            SetBody(Right, text.ToString(), BodyTop, BodyBottom);
        }

        private static string IdentityParts(Casting.PortalDefinition portal, bool sigil)
        {
            var lore = Discovery.Instance != null ? Discovery.Instance.Lore : null;
            if (sigil) return lore != null ? lore.Name(portal.Identity.Sigil) : portal.Identity.Sigil;
            var names = new List<string>();
            foreach (string s in portal.Identity.Signs) names.Add(lore != null ? lore.Name(s) : s);
            return string.Join("  +  ", names);
        }

        private void ShowSpread(GrimoireScreen s)
        {
            GrimoireSpread[] all = Spreads(s);
            spread = Mathf.Clamp(spread, 0, all.Length - 1);
            GrimoireSpread sheet = all[spread];
            float size = s == GrimoireScreen.Controls ? BodySize * 0.95f : BodySize;
            SetTitle(Left, sheet.LeftTitle);
            SetBody(Left, sheet.LeftBody, BodyTop, BodyBottom, size);
            SetTitle(Right, sheet.RightTitle);
            float rightBottom = BodyBottom;
            if (s == GrimoireScreen.Seals)
            {
                // Atajo a los glifos (para cargar una estampa).
                rightBottom = -0.095f;
                Place(Right, 0f, -0.116f, ContentWidth * 0.7f, 0.03f, "Ir a Glifos", ButtonSize, () => Go(GrimoireScreen.GlyphIndex));
            }
            SetBody(Right, sheet.RightBody, BodyTop, rightBottom, size);
            Pager(spread, all.Length, () => Step(-1), () => Step(1));
        }

        private void ShowIndex()
        {
            var discovery = Discovery.Instance;
            int total = discovery != null ? CountLore(discovery.Lore) : glyphs.Count;
            int sigils = 0;
            for (int k = 0; k < glyphs.Count; k++)
                if (dictionary.Dictionary.Get(glyphs[k]).Kind == GlyphKind.Sigil) sigils++;

            SetTitle(Left, "Glifos");
            SetBody(Left, GrimoireTexts.GlyphIntro(glyphs.Count, total, sigils, glyphs.Count - sigils), BodyTop, BodyBottom);

            int pages = IndexPageCount();
            indexPage = Mathf.Clamp(indexPage, 0, pages - 1);
            SetTitle(Right, "Descubiertos");
            if (glyphs.Count == 0)
            {
                SetBody(Right, "Aún no descubriste ningún glifo.\n\nLeé los libros de la biblioteca o dibujá bien un glifo nuevo para descubrirlo.", BodyTop, BodyBottom);
                return;
            }
            float colW = ContentWidth / 2f - 0.004f;
            for (int g = 0; g < glyphs.Count; g++)
            {
                int slot = SlotOf(g);
                if (slot / IndexPageSize != indexPage) continue;
                int k = slot % IndexPageSize;
                string id = glyphs[g];
                // Dos columnas, de arriba abajo. El Circulo Magico (g == 0, slot 0) ocupa las dos columnas.
                bool wide = g == 0 && CircleExtraSlots() == 1;
                float x = wide ? 0f : (k % IndexColumns == 0 ? -ContentWidth / 4f : ContentWidth / 4f);
                float y = 0.092f - k / IndexColumns * 0.041f;
                string name = discovery != null ? discovery.Lore.Name(id) : id;
                Place(Right, x, y, wide ? ContentWidth - 0.004f : colW, 0.031f, name, ButtonSize * (wide ? 1.05f : 0.92f), () => Go(GrimoireScreen.GlyphDetail, id));
            }
            Pager(indexPage, pages, () => Step(-1), () => Step(1));
        }

        private void ShowDetail(string id)
        {
            var discovery = Discovery.Instance;
            var entry = dictionary.Dictionary.Get(id);
            bool sigil = entry.Kind == GlyphKind.Sigil;
            string name = discovery != null ? discovery.Lore.Name(id) : id;
            string text = discovery != null ? discovery.Lore.Text(id) : "";
            int i = glyphs.IndexOf(id);

            // Izquierda: el glifo dibujandose, con su clase arriba y la posicion abajo.
            SetTitle(Left, sigil ? "Sigilo" : "Signo");
            QueueTemplate(entry.TemplateStrokes, new Vector2(0.5f, 0.5f), 0.19f);
            Place(Left, 0.034f, BarY, 0.088f, BarHeight, "Índice", ButtonSize * 0.9f, () =>
            {
                indexPage = i >= 0 ? SlotOf(i) / IndexPageSize : 0;
                Go(GrimoireScreen.GlyphIndex);
            });

            // Derecha: nombre, clase, texto y acciones.
            SetTitle(Right, name);
            string kind = sigil ? "Sigilo: elige el elemento del sello." : "Signo: da forma, tamaño o rumbo.";
            SetBody(Right, $"<i><color={GrimoireTexts.Faded}>{kind}</color></i>\n\n{text}", BodyTop, -0.09f);
            // Cargar el glifo que se ve en la estampa que se lleva en la mano.
            Place(Right, 0f, -0.112f, ContentWidth * 0.86f, 0.031f, "Cargar en el sello", ButtonSize, () =>
            {
                if (World.Stamp.Held != null) World.Stamp.Held.Load(id);
                else DebugTools.DiagnosticBus.OnDiagnosticsUpdated?.Invoke("<b>[Sello]</b> Agarra un sello (E) y volve a apretar el boton.");
            });
            Pager(i, glyphs.Count, () => Step(-1), () => Step(1));
        }

        // ---------------------------------------------------------------- hechizos (ADR-034)

        private void LoadSpells()
        {
            var texts = new List<string>();
            if (spellFiles != null)
                for (int i = 0; i < spellFiles.Length; i++)
                    if (spellFiles[i] != null) texts.Add(spellFiles[i].text);
#if UNITY_EDITOR
            // Tolerante: si nadie corrio el setup, en el Editor se leen directo de la carpeta de datos.
            if (texts.Count == 0)
                foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/_Project/Data/Spells" }))
                {
                    var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                    if (asset != null) texts.Add(asset.text);
                }
#endif
            catalog = SpellCatalog.FromJsons(texts, msg => Debug.LogWarning("[Hechizos] " + msg));
            SpellCatalog.Shared = catalog;
            bookRead = IsBookRead;
            PortalKnowledge.Shared = portalsFile != null ? Casting.PortalRecipes.Load(portalsFile.text) : Casting.PortalRecipes.LoadDefault();
            library = SpellLibraryStore.Load(msg => Debug.LogWarning("[Hechizos] " + msg));
            foreach (SpellDef own in library.Items) catalog.AddPlayer(own);
        }

        private static bool IsBookRead(string book) => Discovery.Instance != null && Discovery.Instance.State.HasRead(book);

        /// <summary>Hechizos desbloqueados: los iniciales y los de libros ya leidos.</summary>
        private void RefreshSpells()
        {
            spells.Clear();
            if (catalog != null) catalog.Unlocked(bookRead, spells);
        }

        private int SpellPageCount() => Mathf.Max(1, Mathf.CeilToInt(spells.Count / (float)SpellsPerPage));

        /// <summary>Pista de un hechizo que todavia no se conoce; cambia al pasar de pagina de la lista.</summary>
        private string NextRumor()
        {
            if (catalog == null) return "";
            var hints = new List<string>();
            foreach (SpellDef def in catalog.All)
                if (def.Origin != SpellLibrary.PlayerOrigin && !spells.Contains(def) && !string.IsNullOrWhiteSpace(def.Hint)) hints.Add(def.Hint);
            return hints.Count == 0 ? "" : hints[Mathf.Abs(spellPage) % hints.Count];
        }

        private void ShowSpells()
        {
            RefreshSpells();
            int total = catalog != null ? catalog.All.Count : 0;
            SetTitle(Left, "Hechizos");
            SetBody(Left, GrimoireTexts.SpellIntro(spells.Count, total) + GrimoireTexts.Rumor(NextRumor()), BodyTop, BodyBottom + 0.04f);
            Place(Left, 0f, BarY + 0.04f, ContentWidth * 0.9f, BarHeight, "Guardar el último hechizo", ButtonSize * 0.9f, SaveLastCast);

            SetTitle(Right, "Conocidos");
            if (spells.Count == 0)
            {
                SetBody(Right, GrimoireTexts.NoSpells, BodyTop, BodyBottom);
                return;
            }
            int pages = SpellPageCount();
            spellPage = Mathf.Clamp(spellPage, 0, pages - 1);
            float w = ContentWidth * 0.92f;
            for (int k = 0; k < SpellsPerPage; k++)
            {
                int index = spellPage * SpellsPerPage + k;
                if (index >= spells.Count) break;
                SpellDef def = spells[index];
                Place(Right, 0f, 0.092f - k * 0.041f, w, 0.031f,
                    $"{def.Name}   <size=70%><color={GrimoireTexts.Faded}>{def.ElementName}</color></size>", ButtonSize * 0.95f,
                    () => { spell = def; Go(GrimoireScreen.SpellDetail); });
            }
            Pager(spellPage, pages, () => Step(-1), () => Step(1));
        }

        private void ShowSpellDetail()
        {
            if (spell == null) { ShowSpells(); return; }
            int i = spells.IndexOf(spell);
            SetTitle(Left, spell.Name);
            // El sello completo dibujandose: sigilo, signos y el circulo casi cerrado al final.
            QueueSpell(spell, new Vector2(0.5f, 0.47f), SpellRadius);
            Place(Left, 0.034f, BarY, 0.088f, BarHeight, "Índice", ButtonSize * 0.9f, () =>
            {
                spellPage = i >= 0 ? i / SpellsPerPage : 0;
                Go(GrimoireScreen.Spells);
            });

            SetTitle(Right, spell.Name);
            SetBody(Right, GrimoireTexts.SpellPage(spell), BodyTop, BodyBottom + 0.04f);
            // Cargar el hechizo completo (circulo con su hueco + glifos) en el sello que se lleva en la mano.
            SpellDef shown = spell;
            Place(Right, 0f, -0.112f, ContentWidth * 0.86f, 0.031f, "Cargar en el sello", ButtonSize, () =>
            {
                if (World.Stamp.Held == null) { Hud.Notify("stampspell", "Necesitás un sello en la mano", 2.5f); return; }
                World.Stamp.Held.LoadSpell(shown.Id, shown.Name, SpellStrokeBuilder.Build(shown, TemplateOf));
            });
            if (spell.Origin == SpellLibrary.PlayerOrigin)
            {
                SpellDef own = spell;
                if (renaming)
                {
                    titles[Right].text = renameText + "<color=" + GrimoireTexts.Faded + ">|</color>";
                    Place(Right, -0.066f, -0.150f, 0.094f, BarHeight, "Aceptar", ButtonSize * 0.85f, () => EndRename(true));
                    Place(Right, 0.066f, -0.150f, 0.094f, BarHeight, "Cancelar", ButtonSize * 0.85f, () => EndRename(false));
                    return;
                }
                if (!PlatformDetector.IsVR)
                    Place(Left, 0.129f, BarY, 0.088f, BarHeight, "Renombrar", ButtonSize * 0.9f, BeginRename);
                Place(Right, 0f, -0.150f, ContentWidth * 0.86f, BarHeight, deleteArmed ? "¿Seguro? Tocá de nuevo" : "Borrar hechizo", ButtonSize * 0.9f, () => DeleteSpell(own));
                return;
            }
            Pager(i, spells.Count, () => Step(-1), () => Step(1));
        }

        // ---------------------------------------------------------------- hechizos propios (guardar, renombrar, borrar)

        /// <summary>Guarda lo dibujado en el ultimo hechizo lanzado como hechizo propio y abre su pagina.</summary>
        private void SaveLastCast()
        {
            if (spellCaster == null) spellCaster = FindFirstObjectByType<SpellCaster>();
            CastSnapshot snap = spellCaster != null ? spellCaster.LastCast : null;
            if (snap == null) { Hud.Notify("savespell", "Lanzá un hechizo primero: se guarda el último que lanzaste", 3f); return; }
            if (!library.CanAdd) { Hud.Notify("savespell", $"Ya tenés {SpellLibrary.MaxSpells} hechizos propios: borrá alguno", 3f); return; }
            const float gap = 34f;
            var strokes = SpellRecording.ToRingUnits(snap.Strokes, snap.RingCenterUV, snap.WorldSize, snap.Spell.sealRadius, gap);
            SpellDef def = strokes != null ? library.Add(null, SpellRecording.ElementId(snap.Spell.element), strokes, gap) : null;
            if (def == null) { Hud.Notify("savespell", "No se pudo guardar ese hechizo", 3f); return; }
            catalog.AddPlayer(def);
            SpellLibraryStore.Save(library, msg => Debug.LogWarning("[Hechizos] " + msg));
            Hud.Notify("savespell", $"Hechizo guardado: {def.Name}", 3f);
            RefreshSpells();
            spell = def;
            spellPage = Mathf.Max(0, spells.IndexOf(def)) / SpellsPerPage;
            Go(GrimoireScreen.SpellDetail);
        }

        private void DeleteSpell(SpellDef own)
        {
            if (!deleteArmed) { deleteArmed = true; deleteArmedAt = Time.unscaledTime; Refresh(); return; }
            deleteArmed = false;
            library.Remove(own.Id);
            catalog.RemovePlayer(own.Id);
            SpellLibraryStore.Save(library, msg => Debug.LogWarning("[Hechizos] " + msg));
            Hud.Notify("savespell", $"Hechizo borrado: {own.Name}", 2.5f);
            RefreshSpells();
            spell = null;
            spellPage = Mathf.Clamp(spellPage, 0, SpellPageCount() - 1);
            Go(GrimoireScreen.Spells);
        }

        private void BeginRename()
        {
            if (spell == null || Keyboard.current == null) return;
            if (!focused) { Hud.Notify("rename", "Abrí el grimorio a pantalla completa (Esc) para renombrar", 3f); return; }
            renaming = true;
            renameText = spell.Name;
            gateBeforeRename = input.Gate;
            input.Gate = InputGate.Typing;
            Keyboard.current.onTextInput += OnTextInput;
            Refresh();
        }

        private void EndRename(bool accept)
        {
            if (!renaming) return;
            renaming = false;
            if (Keyboard.current != null) Keyboard.current.onTextInput -= OnTextInput;
            if (input != null) input.Gate = gateBeforeRename;
            if (accept && spell != null && library.Rename(spell.Id, renameText))
            {
                SpellLibraryStore.Save(library, msg => Debug.LogWarning("[Hechizos] " + msg));
            }
            if (screen == GrimoireScreen.SpellDetail) Refresh();
        }

        private void OnTextInput(char c)
        {
            if (!renaming) return;
            if (c == '\b') { if (renameText.Length > 0) renameText = renameText.Substring(0, renameText.Length - 1); }
            else if (!char.IsControl(c) && c != '<' && c != '>' && renameText.Length < SpellLibrary.MaxNameLength) renameText += c;
            if (titles[Right] != null) titles[Right].text = renameText + "<color=" + GrimoireTexts.Faded + ">|</color>";
        }

        private void UpdateRename()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) { EndRename(false); return; }
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) EndRename(true);
            else if (input.IsPressingEscapeRaw) EndRename(false);
        }

        /// <summary>Radio del anillo del sello dibujado en la pagina (m).</summary>
        private const float SpellRadius = 0.098f;

        private List<List<Vector2>> TemplateOf(string id) => dictionary.Dictionary.Get(id).TemplateStrokes;

        /// <summary>Encola los trazos del hechizo (unidades de anillo) centrados en center (UV de la pagina), con el mismo suavizado y remuestreo que los glifos.</summary>
        private void QueueSpell(SpellDef def, Vector2 center, float radiusMeters)
        {
            List<List<Vector2>> strokes = SpellStrokeBuilder.Build(def, TemplateOf);
            for (int s = 0; s < strokes.Count; s++)
            {
                GrimoireStrokeMath.Smooth(strokes[s], GrimoireStrokeMath.SmoothRadius, tmpSmooth);
                tmpPath.Clear();
                for (int i = 0; i < tmpSmooth.Count; i++) tmpPath.Add(tmpSmooth[i] * radiusMeters);
                tmpResampled.Clear();
                GrimoireStrokeMath.Resample(tmpPath, GrimoireStrokeMath.StepMeters, tmpResampled);
                for (int i = 0; i < tmpResampled.Count; i++)
                {
                    Vector2 p = tmpResampled[i];
                    strokeQueue.Add((new Vector2(center.x + p.x / PageWidth, center.y + p.y / PageHeight), i == 0, SigilLineWidth));
                }
            }
        }

        private void ShowOptions()
        {
            // Izquierda: el mundo, el progreso y la mano principal.
            SetTitle(Left, "Opciones");
            float w = ContentWidth * 0.86f;
            Label(Left, 0f, 0.098f, ContentWidth, 0.02f, GrimoireTexts.Heading("Mundo"), SmallSize * 1.1f, TextAlignmentOptions.Center);
            Place(Left, 0f, 0.068f, w, 0.031f, PauseMenuMath.ResetLabel(resetArmed), ButtonSize, PressResetWorld);
            Note(Left, 0.04f, "Todo vuelve a su lugar.");
            Place(Left, 0f, 0.005f, w, 0.031f, "Olvidar lo aprendido", ButtonSize, () =>
            {
                Discovery.Instance?.ResetProgress();
                RefreshGlyphs();
                Go(GrimoireScreen.Menu);
            });
            Note(Left, -0.023f, "El grimorio vuelve al inicio.");
            Label(Left, 0f, -0.06f, ContentWidth, 0.02f, GrimoireTexts.Heading("Mano principal"), SmallSize * 1.1f, TextAlignmentOptions.Center);
            bool rightHand = PlayerSettings.MainHandRight;
            Place(Left, 0f, -0.09f, w, 0.031f, rightHand ? "Derecha" : "Izquierda", ButtonSize, () =>
            {
                PlayerSettings.MainHandRight = !PlayerSettings.MainHandRight;
                Refresh();
            });
            Note(Left, -0.118f, "La que lleva la pluma y la barra.");

            // Derecha: control y sonido.
            SetTitle(Right, "Ajustes");
            Label(Right, 0f, 0.098f, ContentWidth, 0.02f, GrimoireTexts.Heading("Control"), SmallSize * 1.1f, TextAlignmentOptions.Center);
            Stepper(0.074f, "Sensibilidad", GrimoireOptions.Multiplier(PlayerSettings.LookSensitivity), dir =>
                PlayerSettings.LookSensitivity = GrimoireOptions.StepSensitivity(PlayerSettings.LookSensitivity, dir));
            RowLabel(0.042f, "Invertir eje Y");
            Place(Right, 0.058f, 0.042f, 0.09f, 0.028f, PlayerSettings.InvertY ? "Sí" : "No", ButtonSize, () =>
            {
                PlayerSettings.InvertY = !PlayerSettings.InvertY;
                Refresh();
            });
            RowLabel(0.010f, "Calidad");
            Place(Right, 0.058f, 0.010f, 0.09f, 0.028f, GraphicsQuality.Label(GraphicsQuality.Current), ButtonSize, () =>
            {
                GraphicsQuality.Current = GraphicsQuality.Next(GraphicsQuality.Current);
                Refresh();
            });
            Label(Right, 0f, -0.026f, ContentWidth, 0.02f, GrimoireTexts.Heading("Sonido"), SmallSize * 1.1f, TextAlignmentOptions.Center);
            Stepper(-0.057f, "General", GrimoireOptions.Percent(GrimoireOptions.MasterVolume), dir =>
                GrimoireOptions.MasterVolume = GrimoireOptions.StepVolume(GrimoireOptions.MasterVolume, dir));
            Stepper(-0.089f, "Música", GrimoireOptions.Percent(GrimoireOptions.MusicVolume), dir =>
                GrimoireOptions.MusicVolume = GrimoireOptions.StepVolume(GrimoireOptions.MusicVolume, dir));
            Stepper(-0.121f, "Efectos", GrimoireOptions.Percent(GrimoireOptions.EffectsVolume), dir =>
                GrimoireOptions.EffectsVolume = GrimoireOptions.StepVolume(GrimoireOptions.EffectsVolume, dir));

            // Abajo a la derecha (la barra de esta pantalla esta libre): volver al menu principal.
            if (MainMenuRequested != null)
                Place(Right, 0.044f, BarY, 0.13f, BarHeight, "Menú principal", ButtonSize * 0.9f, () =>
                {
                    Close();
                    MainMenuRequested?.Invoke();
                });
        }

        /// <summary>Primer clic arma la confirmacion (3 s); el segundo, dentro de ese plazo, reinicia el mundo y cierra el libro.</summary>
        private void PressResetWorld()
        {
            if (PauseMenuMath.IsArmed(resetArmedAt, Time.unscaledTime))
            {
                resetArmed = false;
                resetArmedAt = PauseMenuMath.Disarmed;
                ResetWorldRequested?.Invoke();
                Close();
                return;
            }
            resetArmed = true;
            resetArmedAt = Time.unscaledTime;
            Refresh();
        }

        /// <summary>Salir: al menu principal si hay oyente (MainMenu); si no, cierra la aplicacion (o detiene el Play en el Editor).</summary>
        private void PressExit()
        {
            Close();
            if (MainMenuRequested != null) { MainMenuRequested.Invoke(); return; }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// Pausa (Esc abre el libro aca): Reanudar, Reiniciar, Controles, Opciones completas y Salir, mas los ajustes
        /// rapidos de control a la derecha. El juego no se congela (Time.timeScale intacto).
        /// </summary>
        private void ShowPause()
        {
            SetTitle(Left, "Pausa");
            float w = ContentWidth * 0.86f;
            Place(Left, 0f, 0.09f, w, 0.031f, "Reanudar", ButtonSize, Close);
            Place(Left, 0f, 0.05f, w, 0.031f, PauseMenuMath.ResetLabel(resetArmed), ButtonSize, PressResetWorld);
            Place(Left, 0f, 0.01f, w, 0.031f, "Controles", ButtonSize, () => Go(GrimoireScreen.Controls));
            Place(Left, 0f, -0.03f, w, 0.031f, "Opciones y sonido", ButtonSize, () => Go(GrimoireScreen.Options));
            Place(Left, 0f, -0.07f, w, 0.031f, MainMenuRequested != null ? "Salir al menú principal" : "Salir del juego", ButtonSize, PressExit);
            Note(Left, -0.108f, "El mundo sigue mientras leés.");

            SetTitle(Right, "Ajustes rápidos");
            Label(Right, 0f, 0.098f, ContentWidth, 0.02f, GrimoireTexts.Heading("Control"), SmallSize * 1.1f, TextAlignmentOptions.Center);
            Stepper(0.074f, "Sensibilidad", GrimoireOptions.Multiplier(PlayerSettings.LookSensitivity), dir =>
                PlayerSettings.LookSensitivity = GrimoireOptions.StepSensitivity(PlayerSettings.LookSensitivity, dir));
            RowLabel(0.042f, "Invertir eje Y");
            Place(Right, 0.058f, 0.042f, 0.09f, 0.028f, PlayerSettings.InvertY ? "Sí" : "No", ButtonSize, () =>
            {
                PlayerSettings.InvertY = !PlayerSettings.InvertY;
                Refresh();
            });
            RowLabel(0.010f, "Calidad");
            Place(Right, 0.058f, 0.010f, 0.09f, 0.028f, GraphicsQuality.Label(GraphicsQuality.Current), ButtonSize, () =>
            {
                GraphicsQuality.Current = GraphicsQuality.Next(GraphicsQuality.Current);
                Refresh();
            });
            RowLabel(-0.022f, "Mano principal");
            Place(Right, 0.058f, -0.022f, 0.09f, 0.028f, PlayerSettings.MainHandRight ? "Derecha" : "Izquierda", ButtonSize * 0.9f, () =>
            {
                PlayerSettings.MainHandRight = !PlayerSettings.MainHandRight;
                Refresh();
            });
        }

        /// <summary>Fila de opcion en la pagina derecha: nombre, boton -, valor, boton +.</summary>
        private void Stepper(float y, string name, string value, Action<int> change)
        {
            RowLabel(y, name);
            Place(Right, 0.018f, y, 0.026f, 0.026f, "-", ButtonSize * 1.1f, () => { change(-1); Refresh(); });
            Label(Right, 0.058f, y, 0.052f, 0.026f, value, ButtonSize, TextAlignmentOptions.Center);
            Place(Right, 0.098f, y, 0.026f, 0.026f, "+", ButtonSize * 1.1f, () => { change(1); Refresh(); });
        }

        private void RowLabel(float y, string name) =>
            Label(Right, -0.06f, y, 0.1f, 0.026f, name, ButtonSize, TextAlignmentOptions.Left);

        private void Note(int side, float y, string text) =>
            Label(side, 0f, y, ContentWidth, 0.018f, $"<i><color={GrimoireTexts.Faded}>{text}</color></i>", SmallSize, TextAlignmentOptions.Center);

        /// <summary>Vuelve a dibujar la pantalla actual sin sonido de pagina (tras cambiar una opcion).</summary>
        private void Refresh() => Show(screen, glyph);

        private void PlaceMenuButton() =>
            Place(Left, -0.064f, BarY, 0.088f, BarHeight, "Menú", ButtonSize * 0.9f, () => Go(GrimoireScreen.Menu));

        /// <summary>Paginacion abajo de la pagina derecha: anterior, "i de n" y siguiente.</summary>
        private void Pager(int index, int count, Action previous, Action next)
        {
            if (count <= 1 || index < 0) return;
            if (index > 0) Place(Right, -0.066f, BarY, 0.094f, BarHeight, "« Anterior", ButtonSize * 0.85f, previous);
            Label(Right, 0f, BarY, 0.036f, BarHeight, $"<color={GrimoireTexts.Faded}>{index + 1} / {count}</color>", SmallSize, TextAlignmentOptions.Center);
            if (index < count - 1) Place(Right, 0.066f, BarY, 0.094f, BarHeight, "Siguiente »", ButtonSize * 0.85f, next);
        }

        private string FirstSigil()
        {
            for (int k = 0; k < glyphs.Count; k++)
                if (dictionary.Dictionary.Get(glyphs[k]).Kind == GlyphKind.Sigil) return glyphs[k];
            return null;
        }

        private static int CountLore(GlyphLore lore)
        {
            int n = 0;
            foreach (var _ in lore.Ids) n++;
            return n;
        }

        // ---------------------------------------------------------------- textos y adornos

        private void SetTitle(int side, string text)
        {
            titles[side].text = GrimoireTexts.Title(text);
            if (!string.IsNullOrEmpty(text)) PlaceOrnament(side, 0f, OrnamentY, 0.15f);
        }

        private void SetBody(int side, string text, float top, float bottom, float size = BodySize)
        {
            TextMeshPro t = bodies[side];
            t.rectTransform.localPosition = new Vector3(0f, (top + bottom) / 2f, -0.0012f);
            t.rectTransform.sizeDelta = new Vector2(ContentWidth, top - bottom);
            t.fontSizeMax = size;
            t.fontSizeMin = size * 0.7f;
            t.text = text;
        }

        /// <summary>Texto suelto del pool (filas de opciones, numeros de pagina, leyendas).</summary>
        private void Label(int side, float x, float y, float w, float h, string text, float size, TextAlignmentOptions alignment)
        {
            if (labelsUsed >= LabelPool) return;
            TextMeshPro t = labels[labelsUsed++];
            t.transform.SetParent(pages[side], false);
            t.rectTransform.localPosition = new Vector3(x, y, -0.0012f);
            t.rectTransform.sizeDelta = new Vector2(w, h);
            t.alignment = alignment;
            t.fontSizeMax = size;
            t.fontSizeMin = size * 0.6f;
            t.text = text;
            t.gameObject.SetActive(true);
        }

        private void PlaceOrnament(int side, float x, float y, float width)
        {
            if (ornamentsUsed >= OrnamentPool) return;
            Transform o = ornaments[ornamentsUsed++];
            o.SetParent(pages[side], false);
            o.localPosition = new Vector3(x, y, -0.0008f);
            o.localScale = new Vector3(width, width * 0.125f, 1f);
            o.gameObject.SetActive(true);
        }

        // ---------------------------------------------------------------- trazo animado (pagina izquierda)

        /// <summary>Plantilla en [0,1] (Y arriba) centrada en center (UV de la pagina) con un lado de size metros.</summary>
        private void QueueTemplate(List<List<Vector2>> strokes, Vector2 center, float size)
        {
            float sx = size / PageWidth, sy = size / PageHeight;
            foreach (var stroke in strokes)
            {
                // Suaviza la escalera de la plantilla y remuestrea por longitud de arco (en metros): misma densidad
                // de puntos en todos los glifos, asi la velocidad es pareja.
                GrimoireStrokeMath.Smooth(stroke, GrimoireStrokeMath.SmoothRadius, tmpSmooth);
                tmpPath.Clear();
                for (int i = 0; i < tmpSmooth.Count; i++) tmpPath.Add(tmpSmooth[i] * size);
                tmpResampled.Clear();
                GrimoireStrokeMath.Resample(tmpPath, GrimoireStrokeMath.StepMeters, tmpResampled);
                for (int i = 0; i < tmpResampled.Count; i++)
                {
                    Vector2 p = tmpResampled[i] / size;
                    strokeQueue.Add((new Vector2(center.x + (p.x - 0.5f) * sx, center.y + (p.y - 0.5f) * sy), i == 0, SigilLineWidth));
                }
            }
        }

        /// <summary>Ancho de la linea de los sigilos del libro (m): fina y limpia.</summary>
        private const float SigilLineWidth = 0.0035f;
        /// <summary>Lado (px) de la textura de la pagina izquierda.</summary>
        private const int PageTextureSize = 1024;

        private void QueueRing(Vector2 center, float radius, float width)
        {
            int Segments = Mathf.Max(16, Mathf.CeilToInt(2f * Mathf.PI * radius / GrimoireStrokeMath.StepMeters));
            for (int i = 0; i <= Segments; i++)
            {
                float a = i / (float)Segments * Mathf.PI * 2f + Mathf.PI * 0.5f;
                strokeQueue.Add((new Vector2(center.x + Mathf.Cos(a) * radius / PageWidth, center.y + Mathf.Sin(a) * radius / PageHeight), i == 0, width));
            }
        }

        private void AnimateStroke()
        {
            if (strokeIndex >= strokeQueue.Count) return;
            // Duracion fija por glifo: todos los puntos de la cola se reparten en DrawSeconds.
            if (strokeRate <= 0f) strokeRate = Mathf.Max(1f, strokeQueue.Count / GrimoireStrokeMath.DrawSeconds);
            strokeBudget += strokeRate * Time.deltaTime;
            while (strokeBudget >= 1f && strokeIndex < strokeQueue.Count)
            {
                var (uv, start, width) = strokeQueue[strokeIndex++];
                leftCanvas.DrawAt(uv, start, width);
                strokeBudget -= 1f;
            }
        }

        // ---------------------------------------------------------------- pools

        private void HidePools()
        {
            SetHover(null);
            for (int i = 0; i < ButtonPool; i++) buttons[i].gameObject.SetActive(false);
            for (int i = 0; i < LabelPool; i++) labels[i].gameObject.SetActive(false);
            for (int i = 0; i < OrnamentPool; i++) ornaments[i].gameObject.SetActive(false);
            buttonsUsed = labelsUsed = ornamentsUsed = 0;
        }

        /// <summary>
        /// Toma el siguiente boton libre del pool, lo pone en la pagina (coordenadas locales de la pagina), lo rotula
        /// y le asigna la accion. Solo se llama al cambiar de pantalla, no por frame.
        /// </summary>
        private void Place(int side, float x, float y, float w, float h, string label, float fontSize, Action action)
        {
            if (buttonsUsed >= ButtonPool) return;
            GrimoireButton b = buttons[buttonsUsed++];
            b.transform.SetParent(pages[side], false);
            b.transform.localPosition = new Vector3(x, y, -0.0004f);
            b.transform.localRotation = Quaternion.identity;
            b.Layout(w, h, label, fontSize, action);
            b.gameObject.SetActive(true);
        }

        // ---------------------------------------------------------------- construccion

        private void Build()
        {
            root = new GameObject("Grimoire").transform;
            root.SetParent(transform, false);
            // Cuerpo cinematico: el movedor lo lleva con MovePosition/MoveRotation (DriveKinematic) y nunca cae.
            body = root.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;

            for (int side = 0; side < 2; side++) BuildHalf(side);
            BuildSpine();
            BuildRibbon();

            buttons = new GrimoireButton[ButtonPool];
            for (int i = 0; i < ButtonPool; i++)
            {
                var go = new GameObject("Boton" + i, typeof(BoxCollider));
                go.transform.SetParent(root, false);
                buttons[i] = go.AddComponent<GrimoireButton>();
                buttons[i].Build(goldMaterial, buttonMaterial, buttonHoverMaterial);
                go.SetActive(false);
            }
            labels = new TextMeshPro[LabelPool];
            for (int i = 0; i < LabelPool; i++)
            {
                labels[i] = GrimoireTexts.MakeText("Texto" + i, root, new Vector2(0.1f, 0.02f), SmallSize, TextAlignmentOptions.Center, GrimoireTexts.Ink);
                labels[i].enableAutoSizing = true;
                labels[i].gameObject.SetActive(false);
            }
            ornaments = new Transform[OrnamentPool];
            for (int i = 0; i < OrnamentPool; i++)
            {
                ornaments[i] = Quad("Filigrana" + i, root, Vector3.zero, Vector2.one, ornamentMaterial);
                ornaments[i].gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Una mitad del libro abierto: pagina (lienzo o pergamino), pila de hojas, tapa de cuero con cantoneras,
        /// textos y zona de agarre. Gira unos grados sobre el lomo para que el libro haga una V.
        /// </summary>
        private void BuildHalf(int side)
        {
            float sign = side == Left ? -1f : 1f;
            var half = new GameObject(side == Left ? "MitadIzquierda" : "MitadDerecha").transform;
            half.SetParent(root, false);
            half.localRotation = Quaternion.Euler(0f, sign * SpreadAngle, 0f);

            var page = new GameObject(side == Left ? "PaginaIzquierda" : "PaginaDerecha").transform;
            page.SetParent(half, false);
            page.localPosition = new Vector3(sign * HalfW, 0f, 0f);
            pages[side] = page;

            // Hoja de arriba: la izquierda es un lienzo (glifo animado), la derecha pergamino.
            Transform sheet = Quad("Hoja", page, new Vector3(0f, 0f, -0.0003f), new Vector2(PageWidth, PageHeight),
                side == Left ? pageMaterial : (pageRightMaterial != null ? pageRightMaterial : pageMaterial));
            if (side == Left)
            {
                bool parchmentShader = pageMaterial != null && pageMaterial.HasProperty("_Parchment");
                Color pageBackground = parchmentShader ? Color.white : parchment;
                // Con el objeto inactivo se prepara la textura ANTES de Awake: sin esto el lienzo salia de ~140x180 px
                // (resolucion x escala de la pagina en metros) y la tinta se veia pixelada. 1024x1024 sobre 27x36 cm.
                sheet.gameObject.SetActive(false);
                leftCanvas = sheet.gameObject.AddComponent<DrawingCanvas>();
                leftCanvas.Prepare(PageTextureSize, brushMaterial, pageBackground, 0.02f, EmissionSide.AwayFromViewer);
                sheet.gameObject.SetActive(true);
                leftCanvas.Configure(brushMaterial, pageBackground, 0.02f);
            }

            // Pila de hojas: capas finas alternando tono, cada una un poco mas chica hacia el canto (se ve en los bordes).
            const int Layers = 6;
            float layer = StackDepth / Layers;
            for (int i = 0; i < Layers; i++)
            {
                float inset = i * 0.0011f;
                Transform slab = Box("Hojas" + i, half, new Vector3(sign * (HalfW - inset / 2f), 0f, layer * (i + 0.5f)),
                    new Vector3(PageWidth - inset, PageHeight - inset * 1.4f, layer * 0.92f), edgeMaterial);
                if (i % 2 == 1) Tint(slab, new Color(0.82f, 0.75f, 0.6f));
            }

            // Tapa de cuero (sobresale de las hojas) y cantoneras doradas en las esquinas de afuera.
            float coverW = PageWidth + CoverMargin, coverH = PageHeight + 2f * CoverMargin;
            float coverZ = StackDepth + CoverThickness / 2f;
            Box("Tapa", half, new Vector3(sign * coverW / 2f, 0f, coverZ), new Vector3(coverW, coverH, CoverThickness), coverMaterial);
            float ox = sign * coverW, oy = coverH / 2f;
            for (int k = -1; k <= 1; k += 2)
            {
                float y = k * oy;
                Box("Cantonera", half, new Vector3(ox - sign * 0.022f, y - k * 0.0035f, coverZ), new Vector3(0.044f, 0.007f, CoverThickness + 0.003f), goldMaterial);
                Box("Cantonera", half, new Vector3(ox - sign * 0.0035f, y - k * 0.022f, coverZ), new Vector3(0.007f, 0.044f, CoverThickness + 0.003f), goldMaterial);
                Box("Remache", half, new Vector3(ox - sign * 0.0075f, y - k * 0.0075f, coverZ), new Vector3(0.008f, 0.008f, CoverThickness + 0.004f), goldMaterial);
            }
            // Filete dorado a lo largo del borde de afuera de la tapa.
            Box("Filete", half, new Vector3(ox - sign * 0.0035f, 0f, coverZ), new Vector3(0.0022f, coverH - 0.1f, CoverThickness + 0.0015f), goldMaterial);
            // Emblema en la contratapa (se ve al girar el libro).
            Transform emblem = Quad("Emblema", half, new Vector3(sign * coverW / 2f, 0f, StackDepth + CoverThickness + 0.0006f), new Vector2(0.17f, 0.17f), emblemMaterial);
            emblem.localRotation = Quaternion.Euler(0f, 180f, 0f);

            if (side == Right)
            {
                // Cierre: correa de cuero con hebilla dorada en el borde de afuera.
                Box("Correa", half, new Vector3(coverW + 0.016f, 0f, coverZ), new Vector3(0.036f, 0.034f, 0.004f), buttonMaterial);
                Box("Hebilla", half, new Vector3(coverW + 0.03f, 0f, coverZ), new Vector3(0.01f, 0.044f, 0.009f), goldMaterial);
            }

            // Textos fijos de la pagina: titulo y cuerpo (los demas salen del pool).
            titles[side] = GrimoireTexts.MakeText("Titulo", page, new Vector2(ContentWidth, 0.04f), TitleSize, TextAlignmentOptions.Center, GrimoireTexts.Ink);
            titles[side].rectTransform.localPosition = new Vector3(0f, TitleY, -0.0012f);
            titles[side].enableAutoSizing = true;
            titles[side].fontSizeMax = TitleSize;
            titles[side].fontSizeMin = TitleSize * 0.6f;
            titles[side].textWrappingMode = TextWrappingModes.NoWrap;
            bodies[side] = GrimoireTexts.MakeText("Cuerpo", page, new Vector2(ContentWidth, BodyTop - BodyBottom), BodySize, TextAlignmentOptions.TopLeft, GrimoireTexts.Ink);
            bodies[side].enableAutoSizing = true;

            // Agarre: disparador detras de la hoja (los botones, delante, ganan el rayo); cubre tapa, cantos y hojas vacias.
            var grab = new GameObject("Agarre");
            grab.transform.SetParent(half, false);
            grab.transform.localPosition = new Vector3(sign * (coverW + 0.01f) / 2f, 0f, (StackDepth + CoverThickness) / 2f + 0.0005f);
            var box = grab.AddComponent<BoxCollider>();
            box.size = new Vector3(coverW + 0.03f, coverH + 0.02f, StackDepth + CoverThickness);
            box.isTrigger = true;
            grabColliders.Add(box);
            grab.AddComponent<GrimoireGrab>().Configure(this, root, transform);
        }

        private void BuildSpine()
        {
            float h = PageHeight + 2f * CoverMargin;
            Transform spine = Primitive("Lomo", PrimitiveType.Cylinder, root, new Vector3(0f, 0f, StackDepth + CoverThickness * 0.6f),
                new Vector3(0.046f, h / 2f, 0.026f), buttonMaterial != null ? buttonMaterial : coverMaterial);
            spine.localRotation = Quaternion.identity;
            for (int k = -1; k <= 1; k++)
                Primitive("Nervio", PrimitiveType.Cylinder, root, new Vector3(0f, k * h * 0.3f, StackDepth + CoverThickness * 0.6f),
                    new Vector3(0.048f, 0.003f, 0.028f), goldMaterial);
            // Cabezadas: el canto de las hojas en el lomo, arriba y abajo.
            for (int k = -1; k <= 1; k += 2)
                Box("Cabezada", root, new Vector3(0f, k * (PageHeight / 2f + 0.002f), StackDepth * 0.5f), new Vector3(0.02f, 0.006f, StackDepth), ribbonMaterial);
        }

        /// <summary>Cinta marcapaginas que asoma por abajo del lomo, con cola en V y un sello de cera.</summary>
        private void BuildRibbon()
        {
            const float Width = 0.012f, Length = 0.085f;
            float top = -HalfH + 0.02f;
            float y = top - Length / 2f;
            Box("Cinta", root, new Vector3(0.009f, y, -0.0016f), new Vector3(Width, Length, 0.0012f), ribbonMaterial);
            float end = top - Length;
            Transform tailA = Box("ColaCinta", root, new Vector3(0.006f, end - 0.006f, -0.0016f), new Vector3(Width * 0.5f, 0.016f, 0.0012f), ribbonMaterial);
            tailA.localRotation = Quaternion.Euler(0f, 0f, -14f);
            Transform tailB = Box("ColaCinta", root, new Vector3(0.012f, end - 0.006f, -0.0016f), new Vector3(Width * 0.5f, 0.016f, 0.0012f), ribbonMaterial);
            tailB.localRotation = Quaternion.Euler(0f, 0f, 14f);
            Transform seal = Primitive("SelloCera", PrimitiveType.Cylinder, root, new Vector3(0.009f, top - Length * 0.62f, -0.0035f),
                new Vector3(0.03f, 0.0018f, 0.028f), waxMaterial);
            seal.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Transform stamp = Primitive("SelloCeraRelieve", PrimitiveType.Cylinder, root, new Vector3(0.009f, top - Length * 0.62f, -0.0052f),
                new Vector3(0.018f, 0.0009f, 0.018f), waxMaterial);
            stamp.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Tint(stamp, new Color(0.45f, 0.06f, 0.05f));
        }

        private static Transform Box(string name, Transform parent, Vector3 local, Vector3 size, Material material) =>
            Primitive(name, PrimitiveType.Cube, parent, local, size, material);

        private static Transform Primitive(string name, PrimitiveType type, Transform parent, Vector3 local, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = scale;
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            DestroyImmediate(go.GetComponent<Collider>());
            return go.transform;
        }

        private static Transform Quad(string name, Transform parent, Vector3 local, Vector2 size, Material material)
        {
            Transform t = Primitive(name, PrimitiveType.Quad, parent, local, new Vector3(size.x, size.y, 1f), material);
            t.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return t;
        }

        /// <summary>Tono distinto sin otro material (solo al construir).</summary>
        private static void Tint(Transform t, Color color)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            t.GetComponent<Renderer>().SetPropertyBlock(block);
        }
    }
}
