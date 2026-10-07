using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Glyphs;
using SpellSimulator.Interfaces;
using SpellSimulator.Player;

namespace SpellSimulator.Learning
{
    /// <summary>
    /// Pantallas del grimorio. Es un menu (hub): desde la portada se llega a cada seccion en el orden que se quiera.
    /// </summary>
    public enum GrimoireScreen { Menu, GlyphIndex, GlyphDetail, Controls, Seals, Options }

    /// <summary>
    /// Libro-menu diegetico (SYSTEMS_DESIGN 9). Tab / Y / menu izquierdo lo abre flotando frente al jugador.
    /// Es un hub: la portada tiene botones a Glifos (indice), Controles, Sellos y Opciones; cada pantalla tiene un
    /// boton "Menu" para volver. Las pantallas se navegan apuntando y haciendo clic (o con el mouse en modo fijo).
    /// No se cierra al alejarse. Con B (con el libro abierto) se fija delante de la camara para usarlo a pantalla completa.
    /// </summary>
    public class Grimoire : MonoBehaviour
    {
        public static event Action Opened;
        public static event Action Closed;
        public static event Action PageTurned;
        /// <summary>El jugador pidio reiniciar el mundo desde el libro.</summary>
        public static event Action ResetWorldRequested;

        [SerializeField] private PlayerInputReader input;
        [SerializeField] private GlyphDictionaryAsset dictionary;
        [SerializeField] private Material pageMaterial;
        [SerializeField] private Material brushMaterial;
        [SerializeField] private Material coverMaterial;
        [SerializeField] private Color parchment = new Color(0.934f, 0.875f, 0.639f);
        [SerializeField, Tooltip("Puntos de trazo dibujados por segundo en la animacion.")]
        private float strokeSpeed = 160f;
        [SerializeField] private float openDistance = 0.75f;

        private const float PageWidth = 0.24f, PageHeight = 0.32f;
        /// <summary>Fraccion de la pantalla que ocupa el libro en modo fijo.</summary>
        private const float FocusFill = 0.85f;
        /// <summary>Glifos por pagina del indice (2 columnas x 5 filas).</summary>
        private const int IndexColumns = 2, IndexRows = 5, IndexPageSize = IndexColumns * IndexRows;
        /// <summary>Botones reutilizables del libro (pool fijo creado en Build).</summary>
        private const int PoolSize = 14;

        private Transform root;
        private DrawingCanvas leftCanvas;
        private TextMeshPro rightText, leftCaption;
        private Collider handleCollider;
        private Camera viewer;

        // Pantalla actual y datos de navegacion.
        private GrimoireScreen screen;
        private string glyph;                               // glifo de la pantalla GlyphDetail
        private readonly List<string> glyphs = new List<string>(); // glifos descubiertos, en el orden del indice
        private int indexPage;                              // pagina del indice de glifos

        // Pool de botones: cada pantalla reutiliza los primeros slots y desactiva el resto.
        private GrimoireButton[] buttons;
        private TextMeshPro[] labels;
        private int slotsUsed;

        // Modo fijo (V): el libro se parenta a la camara y se guarda la pose para volver.
        private bool focused;
        private Transform savedParent;
        private Vector3 savedPosition;
        private Quaternion savedRotation;
        private bool wasUsingTool;

        // Animacion del trazo.
        private readonly List<(Vector2 uv, bool start)> strokeQueue = new List<(Vector2, bool)>();
        private int strokeIndex;
        private float strokeBudget;

        public static Grimoire Instance { get; private set; }

        public bool IsOpen => root != null && root.gameObject.activeSelf;

        /// <summary>Glifo de la pantalla de detalle abierta (null en cualquier otra). Lo usan las estampas para cargarse.</summary>
        public string CurrentGlyph => IsOpen && screen == GrimoireScreen.GlyphDetail ? glyph : null;

        /// <summary>True si el collider es parte del libro (paginas, botones).</summary>
        public bool Owns(Collider c) => c != null && root != null && c.transform.IsChildOf(root);

        private void Awake() => Instance = this;

        private void Start()
        {
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
            if (input.IsTogglingBook)
            {
                if (IsOpen) Close();
                else Open();
            }
            if (!IsOpen) return;

            if (input.IsTogglingBookFocus)
            {
                if (focused) ExitFocus();
                else EnterFocus();
            }

            // Flanco de subida del clic: en modo fijo presiona el boton bajo el puntero.
            bool usingTool = input.IsUsingTool;
            bool pressed = usingTool && !wasUsingTool;
            wasUsingTool = usingTool;
            if (focused && pressed) PressUnderPointer();

            // Teclado: flechas / cruceta; la rueda del mouse tambien navega.
            int delta = input.PageDelta;
            if (delta == 0 && input.Scroll != 0f) delta = input.Scroll > 0f ? -1 : 1;
            if (delta != 0) Step(delta);

            AnimateStroke();
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
            root.position = cam.position + flat * openDistance + Vector3.down * 0.28f;
            // Abierto hacia el jugador, inclinado como sobre un atril.
            root.rotation = Quaternion.LookRotation(flat, Vector3.up) * Quaternion.Euler(50f, 0f, 0f);
            root.gameObject.SetActive(true);
            wasUsingTool = input != null && input.IsUsingTool;
            RefreshGlyphs();
            // Si el glifo abierto ya no esta descubierto (se olvido todo), vuelve al menu.
            GrimoireScreen s = screen;
            string g = glyph;
            if (s == GrimoireScreen.GlyphDetail && !glyphs.Contains(g)) { s = GrimoireScreen.Menu; g = null; }
            Show(s, g);
            Opened?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen) return;
            ExitFocus();
            root.gameObject.SetActive(false);
            Closed?.Invoke();
        }

        // ---------------------------------------------------------------- modo fijo (V)

        /// <summary>
        /// Fija el libro delante de la camara, centrado y del tamano que lo deja ocupar ~85% de la pantalla.
        /// Guarda la pose anterior para restaurarla al salir.
        /// </summary>
        private void EnterFocus()
        {
            if (viewer == null) viewer = Camera.main;
            if (viewer == null) return;
            savedParent = root.parent;
            savedPosition = root.position;
            savedRotation = root.rotation;
            root.SetParent(viewer.transform, false);
            // Con identity el libro queda de cara a la camara (su cara visible es -Z local, igual que los quads).
            root.localPosition = new Vector3(0f, 0f, FocusDistance(viewer));
            root.localRotation = Quaternion.identity;
            // Mientras esta fijo no se puede agarrar (E podria arrancarlo de la camara).
            if (handleCollider != null) handleCollider.enabled = false;
            focused = true;
            FocusMode.Enter();
        }

        private void ExitFocus()
        {
            if (!focused) return;
            focused = false;
            if (handleCollider != null) handleCollider.enabled = true;
            Transform parent = savedParent != null ? savedParent : transform;
            root.SetParent(parent, true);
            root.SetPositionAndRotation(savedPosition, savedRotation);
            FocusMode.Exit();
        }

        /// <summary>Distancia para que el alto (y tambien el ancho) del libro ocupe ~85% de la vista, limitada a 0.3..1.2 m.</summary>
        private static float FocusDistance(Camera cam)
        {
            float halfTan = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float byHeight = (PageHeight + 0.04f) / FocusFill / (2f * halfTan);
            float byWidth = (2f * PageWidth + 0.04f) / FocusFill / (2f * halfTan * cam.aspect);
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

        // ---------------------------------------------------------------- navegacion

        /// <summary>Flechas / rueda: en el detalle pasa glifo; en el indice pagina el indice.</summary>
        private void Step(int delta)
        {
            if (screen == GrimoireScreen.GlyphDetail)
            {
                int i = glyphs.IndexOf(glyph);
                int n = i + delta;
                if (i >= 0 && n >= 0 && n < glyphs.Count) Go(GrimoireScreen.GlyphDetail, glyphs[n]);
            }
            else if (screen == GrimoireScreen.GlyphIndex)
            {
                int next = indexPage + delta;
                if (next >= 0 && next < IndexPageCount())
                {
                    indexPage = next;
                    Go(GrimoireScreen.GlyphIndex);
                }
            }
        }

        /// <summary>Cambia de pantalla y avisa (sonido de pasar pagina).</summary>
        private void Go(GrimoireScreen next, string id = null)
        {
            Show(next, id);
            PageTurned?.Invoke();
        }

        private int IndexPageCount() => Mathf.Max(1, Mathf.CeilToInt(glyphs.Count / (float)IndexPageSize));

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
        }

        private void OnDiscovered(string glyphId, string source)
        {
            if (!IsOpen) return;
            RefreshGlyphs();
            Show(screen, glyph);
        }

        // ---------------------------------------------------------------- pantallas

        /// <summary>Dibuja una pantalla: limpia la pagina izquierda, rotula el texto y reasigna los botones.</summary>
        private void Show(GrimoireScreen s, string id = null)
        {
            screen = s;
            glyph = id;
            leftCanvas.Clear();
            strokeQueue.Clear();
            strokeIndex = 0;
            HideButtons();
            var discovery = Discovery.Instance;
            int total = discovery != null ? CountLore(discovery.Lore) : glyphs.Count;

            // Por defecto: titulo abajo de la pagina izquierda y texto ocupa toda la derecha.
            SetCaption(-0.02f, PageHeight * 0.9f);
            SetRightText(0f, PageHeight * 0.88f);

            switch (s)
            {
                case GrimoireScreen.Menu:
                    // Portada: titulo arriba a la izquierda y el anillo animado debajo.
                    leftCaption.text = "<size=140%><b>Grimorio</b></size>";
                    SetCaption(0.1f, 0.06f);
                    rightText.text = "";
                    QueueRing();
                    float x = PageWidth / 2f, w = PageWidth * 0.82f, h = 0.05f;
                    Place($"Glifos ({glyphs.Count} de {total})", x, 0.105f, w, h, 0.085f, () => Go(GrimoireScreen.GlyphIndex));
                    Place("Controles", x, 0.035f, w, h, 0.085f, () => Go(GrimoireScreen.Controls));
                    Place("Sellos y estampas", x, -0.035f, w, h, 0.085f, () => Go(GrimoireScreen.Seals));
                    Place("Opciones", x, -0.105f, w, h, 0.085f, () => Go(GrimoireScreen.Options));
                    return;

                case GrimoireScreen.GlyphIndex:
                    ShowIndex(discovery);
                    break;

                case GrimoireScreen.GlyphDetail:
                    ShowDetail(discovery, id);
                    break;

                case GrimoireScreen.Controls:
                    leftCaption.text = "<size=130%>Controles</size>" + NL + NL + "<size=70%>PC / VR</size>";
                    rightText.text = "<size=56%><b>Moverse</b>: WASD, Shift correr, C o Ctrl agacharse, Z tumbarse, Espacio saltar, X zoom (mantener)" + NL +
                        "<b>Objetos</b>: 1-2 slots de la mano derecha, 3-4 de la izquierda (la barra de abajo)" + NL +
                        "<b>Agarrar</b>: E herramientas, lienzos y estampas a la mano; E sobre cajas y pesas las sostiene a distancia (rueda: acercar o alejar, clic der. + mouse: girar, Shift + rueda: inclinar, clic de la rueda: dejar flotando, E: soltar)" + NL +
                        "<b>Mano</b>: Q arrojar, T dejar el lienzo flotando, F dar vuelta la hoja, V lienzo a pantalla" + NL +
                        "<b>Dibujar</b>: clic izq. dibuja, clic der. deshace, 5 a 0 grosor de la pluma, R recarga tinta" + NL +
                        "<b>Menu</b>: Tab grimorio, B grimorio a pantalla, F3 depuracion, Retroceso reiniciar el mundo" + NL +
                        "<b>VR</b>: gatillo dibuja, agarre agarra, A deshace, B arroja, click del stick izq. corre, click del stick der. da vuelta la hoja, Y reinicia</size>";
                    break;

                case GrimoireScreen.Seals:
                    leftCaption.text = "<size=130%>Sellos</size>";
                    rightText.text = "<size=62%>Dibuja un sigilo, signos alrededor y cierra el anillo: el hechizo sale por la cara CON TINTA. Da vuelta la hoja (F) para apuntar." + NL + NL +
                        "Se puede dibujar sobre el piso, la torre y las cajas: el sello se pega ahi y sale hacia afuera de la superficie. Un crater, fuego o agua encima lo rompen." + NL + NL +
                        "Estampas (mesa del campo y escritorio): agarra una estampa (E), abri un glifo en el grimorio y hace clic en Cargar en la estampa. Despues clic izq. sobre un papel o el piso estampa el glifo (5 usos)." + NL + NL +
                        "Piedras de impacto y placa de presion: dibuja el sello cerrado y quedan armados; se lanzan al golpe o con peso encima.</size>";
                    break;

                case GrimoireScreen.Options:
                    leftCaption.text = "<size=140%>Opciones</size>";
                    rightText.text = "<size=80%>Reiniciar el mundo devuelve todo a su lugar.\n\n\n\nOlvidar lo aprendido vuelve al grimorio inicial.</size>";
                    Place("Reiniciar el mundo", PageWidth / 2f, 0.05f, PageWidth * 0.75f, 0.035f, 0.08f, () =>
                    {
                        ResetWorldRequested?.Invoke();
                        Close();
                    });
                    Place("Olvidar lo aprendido", PageWidth / 2f, -0.06f, PageWidth * 0.75f, 0.035f, 0.08f, () =>
                    {
                        Discovery.Instance?.ResetProgress();
                        RefreshGlyphs();
                        Go(GrimoireScreen.Menu);
                    });
                    break;
            }

            // Resto de las pantallas: boton para volver al menu, abajo a la izquierda.
            PlaceMenuButton();
        }

        private void ShowIndex(Discovery discovery)
        {
            int pages = IndexPageCount();
            indexPage = Mathf.Clamp(indexPage, 0, pages - 1);
            leftCaption.text = $"<size=130%>Glifos</size>\n\n<size=70%>Pagina {indexPage + 1} de {pages}</size>";
            if (glyphs.Count == 0)
            {
                rightText.text = "<size=80%>Aun no descubriste ningun glifo.\n\nLee los libros de la biblioteca o dibuja bien un glifo nuevo para descubrirlo.</size>";
                return;
            }
            rightText.text = "";
            int first = indexPage * IndexPageSize;
            for (int k = 0; k < IndexPageSize && first + k < glyphs.Count; k++)
            {
                string id = glyphs[first + k];
                // Dos columnas, de arriba abajo.
                float x = k % IndexColumns == 0 ? 0.065f : 0.175f;
                float y = 0.11f - k / IndexColumns * 0.05f;
                string name = discovery != null ? discovery.Lore.Name(id) : id;
                Place(name, x, y, 0.1f, 0.035f, 0.065f, () => Go(GrimoireScreen.GlyphDetail, id));
            }
            if (pages > 1)
            {
                if (indexPage > 0) Place("<- Antes", 0.065f, -0.135f, 0.1f, 0.035f, 0.065f, () => { indexPage--; Go(GrimoireScreen.GlyphIndex); });
                if (indexPage < pages - 1) Place("Mas ->", 0.175f, -0.135f, 0.1f, 0.035f, 0.065f, () => { indexPage++; Go(GrimoireScreen.GlyphIndex); });
            }
        }

        private void ShowDetail(Discovery discovery, string id)
        {
            leftCaption.text = "";
            var entry = dictionary.Dictionary.Get(id);
            string kind = entry.Kind == GlyphKind.Sigil ? "Sigilo" : "Signo";
            string name = discovery != null ? discovery.Lore.Name(id) : id;
            string text = discovery != null ? discovery.Lore.Text(id) : "";
            int i = glyphs.IndexOf(id);
            string footer = i >= 0 ? $"\n\n<size=60%><color=#7a5a3a>Glifo {i + 1} de {glyphs.Count}</color></size>" : "";
            SetRightText(0.05f, PageHeight * 0.7f);
            rightText.text = $"<size=140%><b>{name}</b></size>\n<color=#7a5a3a>{kind}</color>\n\n<size=85%>{text}</size>{footer}";
            QueueTemplate(entry.TemplateStrokes);

            // Anterior / siguiente entre los glifos descubiertos, en el orden del indice.
            if (i > 0) Place("<- Anterior", 0.065f, -0.135f, 0.1f, 0.035f, 0.065f, () => Go(GrimoireScreen.GlyphDetail, glyphs[i - 1]));
            if (i >= 0 && i < glyphs.Count - 1) Place("Siguiente ->", 0.175f, -0.135f, 0.1f, 0.035f, 0.065f, () => Go(GrimoireScreen.GlyphDetail, glyphs[i + 1]));
            // Cargar el glifo que se ve en la estampa que se lleva en la mano.
            Place("Cargar en la estampa", 0.12f, -0.085f, 0.2f, 0.04f, 0.07f, () =>
            {
                if (World.Stamp.Held != null) World.Stamp.Held.Load(id);
                else DebugTools.DiagnosticBus.OnDiagnosticsUpdated?.Invoke("<b>[Estampa]</b> Agarra una estampa (E) y volve a apretar el boton.");
            });
            Place("Indice", -0.07f, -0.135f, 0.08f, 0.035f, 0.065f, () =>
            {
                indexPage = i >= 0 ? i / IndexPageSize : 0;
                Go(GrimoireScreen.GlyphIndex);
            });
        }

        private void PlaceMenuButton() =>
            Place("Menu", -0.19f, -0.135f, 0.08f, 0.035f, 0.065f, () => Go(GrimoireScreen.Menu));

        private void SetCaption(float y, float height)
        {
            leftCaption.rectTransform.localPosition = new Vector3(-PageWidth / 2f, y, -0.002f);
            leftCaption.rectTransform.sizeDelta = new Vector2(PageWidth * 0.85f, height);
        }

        private void SetRightText(float y, float height)
        {
            rightText.rectTransform.localPosition = new Vector3(PageWidth / 2f, y, -0.002f);
            rightText.rectTransform.sizeDelta = new Vector2(PageWidth * 0.82f, height);
        }

        private static int CountLore(GlyphLore lore)
        {
            int n = 0;
            foreach (var _ in lore.Ids) n++;
            return n;
        }

        private const string NL = "\n";

        private void QueueTemplate(List<List<Vector2>> strokes)
        {
            // Plantilla en [0,1] (Y arriba) dentro de la pagina con margen.
            foreach (var stroke in strokes)
                for (int i = 0; i < stroke.Count; i++)
                    strokeQueue.Add((new Vector2(0.12f + stroke[i].x * 0.76f, 0.2f + stroke[i].y * 0.6f * PageWidth / PageHeight * 1.33f), i == 0));
        }

        private void QueueRing()
        {
            for (int i = 0; i <= 48; i++)
            {
                float a = i / 48f * Mathf.PI * 2f;
                strokeQueue.Add((new Vector2(0.5f + Mathf.Cos(a) * 0.3f, 0.42f + Mathf.Sin(a) * 0.3f * PageWidth / PageHeight), i == 0));
            }
        }

        private void AnimateStroke()
        {
            if (strokeIndex >= strokeQueue.Count) return;
            strokeBudget += strokeSpeed * Time.deltaTime;
            while (strokeBudget >= 1f && strokeIndex < strokeQueue.Count)
            {
                var (uv, start) = strokeQueue[strokeIndex++];
                leftCanvas.DrawAt(uv, start, 0.006f);
                strokeBudget -= 1f;
            }
        }

        // ---------------------------------------------------------------- botones (pool)

        private void HideButtons()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                buttons[i].gameObject.SetActive(false);
                labels[i].gameObject.SetActive(false);
            }
            slotsUsed = 0;
        }

        /// <summary>
        /// Toma el siguiente boton libre del pool, lo posiciona (coordenadas locales del libro), lo rotula y le asigna la accion.
        /// Solo se llama al cambiar de pantalla, no por frame.
        /// </summary>
        private void Place(string label, float x, float y, float w, float h, float fontSize, Action action)
        {
            if (slotsUsed >= PoolSize) return;
            int i = slotsUsed++;
            var b = buttons[i];
            var t = labels[i];
            b.transform.localPosition = new Vector3(x, y, -0.01f);
            b.transform.localScale = new Vector3(w, h, 0.01f);
            b.Configure(label, action);
            b.gameObject.SetActive(true);

            // La etiqueta es hija del libro (no del boton) para no heredar su escala.
            t.rectTransform.sizeDelta = new Vector2(w, h);
            t.transform.localPosition = new Vector3(x, y, -0.017f);
            t.enableAutoSizing = true;
            t.fontSizeMax = fontSize;
            t.fontSizeMin = fontSize * 0.5f;
            t.text = label;
            t.gameObject.SetActive(true);
        }

        // ---------------------------------------------------------------- construccion

        private void Build()
        {
            root = new GameObject("Grimoire").transform;
            root.SetParent(transform, false);

            // Tapas (un poco mas grandes que las paginas) y lomo.
            Box("CoverLeft", new Vector3(-PageWidth / 2f - 0.005f, 0f, 0.012f), new Vector3(PageWidth + 0.02f, PageHeight + 0.02f, 0.012f), coverMaterial);
            Box("CoverRight", new Vector3(PageWidth / 2f + 0.005f, 0f, 0.012f), new Vector3(PageWidth + 0.02f, PageHeight + 0.02f, 0.012f), coverMaterial);
            Box("Spine", new Vector3(0f, 0f, 0.016f), new Vector3(0.02f, PageHeight + 0.02f, 0.02f), coverMaterial);

            // Pagina izquierda: lienzo donde se dibuja el glifo (o el anillo de la portada).
            var left = GameObject.CreatePrimitive(PrimitiveType.Quad);
            left.name = "PageLeft";
            left.transform.SetParent(root, false);
            left.transform.localPosition = new Vector3(-PageWidth / 2f, 0f, 0f);
            left.transform.localScale = new Vector3(PageWidth, PageHeight, 1f);
            left.GetComponent<Renderer>().sharedMaterial = pageMaterial;
            DestroyImmediate(left.GetComponent<Collider>());
            leftCanvas = left.AddComponent<DrawingCanvas>();
            leftCanvas.Configure(brushMaterial, parchment, 0.02f);
            leftCaption = Text("Caption", root, new Vector3(-PageWidth / 2f, -0.02f, -0.002f), new Vector2(PageWidth * 0.85f, PageHeight * 0.9f), 0.14f, TextAlignmentOptions.Center);

            // Pagina derecha: texto (y, en algunas pantallas, botones).
            var right = GameObject.CreatePrimitive(PrimitiveType.Quad);
            right.name = "PageRight";
            right.transform.SetParent(root, false);
            right.transform.localPosition = new Vector3(PageWidth / 2f, 0f, 0f);
            right.transform.localScale = new Vector3(PageWidth, PageHeight, 1f);
            right.GetComponent<Renderer>().sharedMaterial = pageMaterial;
            DestroyImmediate(right.GetComponent<Collider>());
            rightText = Text("Text", root, new Vector3(PageWidth / 2f, 0f, -0.002f), new Vector2(PageWidth * 0.82f, PageHeight * 0.88f), 0.16f, TextAlignmentOptions.TopLeft);

            // Agarradera: tira bajo las paginas para llevar el libro.
            var handle = new GameObject("Handle");
            handle.transform.SetParent(root, false);
            handle.transform.localPosition = new Vector3(0f, -PageHeight / 2f - 0.03f, 0f);
            var handleBox = handle.AddComponent<BoxCollider>();
            handleBox.size = new Vector3(PageWidth * 2f, 0.05f, 0.03f);
            handleBox.isTrigger = true;
            handleCollider = handleBox;
            handle.AddComponent<GrimoireGrab>().Configure(this, root, transform);
            Box("Ribbon", new Vector3(0f, -PageHeight / 2f - 0.012f, 0.012f), new Vector3(PageWidth * 2f + 0.04f, 0.012f, 0.022f), coverMaterial);

            // Pool fijo de botones (cubo + etiqueta). Cada pantalla los posiciona y rotula.
            buttons = new GrimoireButton[PoolSize];
            labels = new TextMeshPro[PoolSize];
            for (int i = 0; i < PoolSize; i++) CreateSlot(i);
        }

        private void CreateSlot(int i)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Boton" + i;
            go.transform.SetParent(root, false);
            if (coverMaterial != null) go.GetComponent<Renderer>().sharedMaterial = coverMaterial;
            go.GetComponent<Collider>().isTrigger = true;
            buttons[i] = go.AddComponent<GrimoireButton>();

            var text = Text("Label" + i, root, Vector3.zero, new Vector2(0.1f, 0.035f), 0.1f, TextAlignmentOptions.Center);
            text.color = new Color(0.98f, 0.92f, 0.75f);
            labels[i] = text;

            go.SetActive(false);
            text.gameObject.SetActive(false);
        }

        private void Box(string name, Vector3 local, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(root, false);
            go.transform.localPosition = local;
            go.transform.localScale = size;
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            DestroyImmediate(go.GetComponent<Collider>());
        }

        private static TextMeshPro Text(string name, Transform parent, Vector3 local, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var t = go.AddComponent<TextMeshPro>();
            t.rectTransform.sizeDelta = size;
            t.fontSize = fontSize;
            t.alignment = alignment;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.color = new Color(0.25f, 0.12f, 0.08f);
            return t;
        }
    }
}
