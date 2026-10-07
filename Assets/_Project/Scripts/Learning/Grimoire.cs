using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Glyphs;
using SpellSimulator.Player;

namespace SpellSimulator.Learning
{
    /// <summary>
    /// Libro-menu diegetico (SYSTEMS_DESIGN 9). Tab / Y / menu izquierdo lo abre flotando frente al jugador (no sigue
    /// a la camara: asi se puede apuntar a sus paginas en PC y en VR). Paginas: portada, un glifo descubierto por
    /// pagina (el glifo se dibuja solo en la pagina izquierda; nombre y explicacion en la derecha) y opciones.
    /// Se pasa de pagina apuntando a una pagina y apretando agarrar (E / grip). Se cierra con la misma tecla o alejandose.
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
        [SerializeField] private float closeDistance = 3.5f;

        private const float PageWidth = 0.24f, PageHeight = 0.32f;

        private Transform root;
        private DrawingCanvas leftCanvas;
        private TextMeshPro rightText, leftCaption;
        private GrimoireButton resetButton, resetProgressButton;
        private readonly List<string> pages = new List<string>();
        private int page;
        private Camera viewer;

        // Animacion del trazo.
        private readonly List<(Vector2 uv, bool start)> strokeQueue = new List<(Vector2, bool)>();
        private int strokeIndex;
        private float strokeBudget;

        public static Grimoire Instance { get; private set; }

        public bool IsOpen => root != null && root.gameObject.activeSelf;

        /// <summary>Glifo de la pagina abierta (null en portada u opciones). Lo usan las estampas para cargarse.</summary>
        public string CurrentGlyph => IsOpen && page < pages.Count && !pages[page].StartsWith("#") ? pages[page] : null;

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
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (input != null && input.IsTogglingBook)
            {
                if (IsOpen) Close();
                else Open();
            }
            if (!IsOpen) return;
            if (input != null)
            {
                // Teclado: flechas / cruceta; la rueda del mouse tambien pasa paginas.
                int delta = input.PageDelta;
                if (delta == 0 && input.Scroll != 0f) delta = input.Scroll > 0f ? -1 : 1;
                if (delta != 0) Turn(delta);
            }
            if (viewer != null && Vector3.Distance(viewer.transform.position, root.position) > closeDistance) Close();
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
            Rebuild();
            Show(Mathf.Clamp(page, 0, pages.Count - 1));
            Opened?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen) return;
            root.gameObject.SetActive(false);
            Closed?.Invoke();
        }

        // ---------------------------------------------------------------- paginas

        private const string Cover = "#portada", Options = "#opciones", HelpControls = "#controles", HelpSeals = "#sellos";

        private void Rebuild()
        {
            pages.Clear();
            pages.Add(Cover);
            pages.Add(HelpControls);
            pages.Add(HelpSeals);
            var discovery = Discovery.Instance;
            if (discovery != null && dictionary != null)
            {
                // Sigilos primero y despues signos, en el orden del diccionario.
                foreach (var kind in new[] { GlyphKind.Sigil, GlyphKind.Sign })
                    foreach (var entry in dictionary.Dictionary.Entries)
                        if (entry.Kind == kind && discovery.State.Knows(entry.Id) && entry.TemplateStrokes.Count > 0)
                            pages.Add(entry.Id);
            }
            pages.Add(Options);
        }

        private static readonly string NL = "\n";

        private void Turn(int delta)
        {
            int next = Mathf.Clamp(page + delta, 0, pages.Count - 1);
            if (next == page) return;
            Show(next);
            PageTurned?.Invoke();
        }

        private void Show(int index)
        {
            page = index;
            string id = pages[index];
            leftCanvas.Clear();
            strokeQueue.Clear();
            strokeIndex = 0;
            resetButton.gameObject.SetActive(id == Options);
            resetProgressButton.gameObject.SetActive(id == Options);
            var discovery = Discovery.Instance;
            int known = pages.FindAll(p => !p.StartsWith("#")).Count;
            int total = discovery != null ? CountLore(discovery.Lore) : known;
            string footer = $"\n\n<size=60%><color=#7a5a3a>Pagina {index + 1} de {pages.Count}</color></size>";

            if (id == Cover)
            {
                leftCaption.text = "<size=140%>Grimorio</size>\n\n<size=75%>Flechas o rueda: pasar pagina\nApuntar + E: pasar pagina\nE en el borde de abajo: moverlo\nTab: cerrar</size>";
                rightText.text = $"<b>Glifos descubiertos</b>\n{known} de {total}\n\n<size=80%>Lee los libros de la biblioteca o dibuja bien un glifo nuevo para descubrirlo.</size>{footer}";
                QueueRing();
                return;
            }
            if (id == HelpControls)
            {
                leftCaption.text = "<size=130%>Controles</size>" + NL + NL + "<size=70%>PC / VR</size>";
                rightText.text = "<size=62%>WASD mover   Shift correr   Ctrl o C agacharse" + NL + "Espacio saltar" + NL + NL +
                    "E agarrar y soltar (tambien objetos: cajas, pesas)" + NL + "Rueda: acercar o alejar lo que llevas" + NL + "Q arrojar   F dar vuelta la hoja" + NL +
                    "V fijar el lienzo a la camara y dibujar con el mouse" + NL + NL +
                    "Clic izq. dibujar / usar   Clic der. deshacer" + NL + "R recargar tinta   Tab grimorio   Retroceso reiniciar" + NL + NL +
                    "VR: gatillo dibujar, agarre agarrar, A deshacer, click del stick correr, Y reiniciar, girar la mano da vuelta la hoja</size>" + footer;
                return;
            }
            if (id == HelpSeals)
            {
                leftCaption.text = "<size=130%>Sellos</size>";
                rightText.text = "<size=62%>Dibuja un sigilo, signos alrededor y cierra el anillo: el hechizo sale por la cara CON TINTA. Da vuelta la hoja (F) para apuntar." + NL + NL +
                    "Se puede dibujar sobre el piso, la torre y las cajas: el sello se pega ahi y sale hacia afuera de la superficie. Un crater, fuego o agua encima lo rompen." + NL + NL +
                    "Estampas (mesa del campo y escritorio): con el grimorio abierto en un glifo, apunta al libro con la estampa en mano y clic derecho. Clic izq. sobre un papel estampa el glifo (5 usos)." + NL + NL +
                    "Piedras de impacto y placa de presion: dibuja el sello cerrado y quedan armados; se lanzan al golpe o con peso encima.</size>" + footer;
                return;
            }
            if (id == Options)
            {
                leftCaption.text = "<size=140%>Opciones</size>";
                rightText.text = $"<size=80%>Reiniciar el mundo devuelve todo a su lugar.\n\n\n\nOlvidar lo aprendido vuelve al grimorio inicial.</size>{footer}";
                return;
            }

            leftCaption.text = "";
            var entry = dictionary.Dictionary.Get(id);
            string kind = entry.Kind == GlyphKind.Sigil ? "Sigilo" : "Signo";
            string name = discovery != null ? discovery.Lore.Name(id) : id;
            string text = discovery != null ? discovery.Lore.Text(id) : "";
            rightText.text = $"<size=140%><b>{name}</b></size>\n<color=#7a5a3a>{kind}</color>\n\n<size=85%>{text}</size>{footer}";
            QueueTemplate(entry.TemplateStrokes);
        }

        private static int CountLore(GlyphLore lore)
        {
            int n = 0;
            foreach (var _ in lore.Ids) n++;
            return n;
        }

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

        private void OnDiscovered(string glyph, string source)
        {
            if (!IsOpen) return;
            string current = pages.Count > 0 ? pages[page] : Cover;
            Rebuild();
            int keep = pages.IndexOf(current);
            Show(keep >= 0 ? keep : 0);
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

            // Pagina izquierda: lienzo donde se dibuja el glifo.
            var left = GameObject.CreatePrimitive(PrimitiveType.Quad);
            left.name = "PageLeft";
            left.transform.SetParent(root, false);
            left.transform.localPosition = new Vector3(-PageWidth / 2f, 0f, 0f);
            left.transform.localScale = new Vector3(PageWidth, PageHeight, 1f);
            left.GetComponent<Renderer>().sharedMaterial = pageMaterial;
            DestroyImmediate(left.GetComponent<Collider>());
            leftCanvas = left.AddComponent<DrawingCanvas>();
            leftCanvas.Configure(brushMaterial, parchment, 0.02f);
            leftCaption = Text("Caption", left.transform.parent, new Vector3(-PageWidth / 2f, -0.02f, -0.002f), new Vector2(PageWidth * 0.85f, PageHeight * 0.9f), 0.14f, TextAlignmentOptions.Center);

            // Pagina derecha: texto.
            var right = GameObject.CreatePrimitive(PrimitiveType.Quad);
            right.name = "PageRight";
            right.transform.SetParent(root, false);
            right.transform.localPosition = new Vector3(PageWidth / 2f, 0f, 0f);
            right.transform.localScale = new Vector3(PageWidth, PageHeight, 1f);
            right.GetComponent<Renderer>().sharedMaterial = pageMaterial;
            DestroyImmediate(right.GetComponent<Collider>());
            rightText = Text("Text", root, new Vector3(PageWidth / 2f, 0f, -0.002f), new Vector2(PageWidth * 0.82f, PageHeight * 0.88f), 0.16f, TextAlignmentOptions.TopLeft);

            // Zonas para pasar pagina (mitad de cada pagina) y botones de opciones.
            Zone("Prev", new Vector3(-PageWidth / 2f, 0f, -0.005f), new Vector3(PageWidth, PageHeight, 0.01f)).Configure("Pagina anterior", () => Turn(-1));
            Zone("Next", new Vector3(PageWidth / 2f, -PageHeight * 0.3f, -0.005f), new Vector3(PageWidth, PageHeight * 0.4f, 0.01f)).Configure("Pagina siguiente", () => Turn(1));
            Zone("NextTop", new Vector3(PageWidth / 2f, PageHeight * 0.3f, -0.005f), new Vector3(PageWidth, PageHeight * 0.4f, 0.01f)).Configure("Pagina siguiente", () => Turn(1));
            // Agarradera: tira bajo las paginas para llevar el libro.
            var handle = new GameObject("Handle");
            handle.transform.SetParent(root, false);
            handle.transform.localPosition = new Vector3(0f, -PageHeight / 2f - 0.03f, 0f);
            var handleBox = handle.AddComponent<BoxCollider>();
            handleBox.size = new Vector3(PageWidth * 2f, 0.05f, 0.03f);
            handleBox.isTrigger = true;
            handle.AddComponent<GrimoireGrab>().Configure(this, root, transform);
            Box("Ribbon", new Vector3(0f, -PageHeight / 2f - 0.012f, 0.012f), new Vector3(PageWidth * 2f + 0.04f, 0.012f, 0.022f), coverMaterial);

            resetButton = Button("ResetWorld", new Vector3(PageWidth / 2f, 0.05f, -0.01f), "Reiniciar el mundo", () =>
            {
                ResetWorldRequested?.Invoke();
                Close();
            });
            resetProgressButton = Button("ResetProgress", new Vector3(PageWidth / 2f, -0.06f, -0.01f), "Olvidar lo aprendido", () =>
            {
                Discovery.Instance?.ResetProgress();
            });
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

        private GrimoireButton Zone(string name, Vector3 local, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = local;
            var col = go.AddComponent<BoxCollider>();
            col.size = size;
            col.isTrigger = true;
            return go.AddComponent<GrimoireButton>();
        }

        private GrimoireButton Button(string name, Vector3 local, string label, Action action)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(root, false);
            go.transform.localPosition = local;
            go.transform.localScale = new Vector3(PageWidth * 0.75f, 0.035f, 0.01f);
            if (coverMaterial != null) go.GetComponent<Renderer>().sharedMaterial = coverMaterial;
            go.GetComponent<Collider>().isTrigger = true;
            var text = Text("Label", root, local + new Vector3(0f, 0f, -0.007f), new Vector2(PageWidth * 0.75f, 0.035f), 0.1f, TextAlignmentOptions.Center);
            text.text = label;
            text.color = new Color(0.98f, 0.92f, 0.75f);
            text.transform.SetParent(go.transform, true);
            var button = go.AddComponent<GrimoireButton>();
            button.Configure(label, action);
            return button;
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
