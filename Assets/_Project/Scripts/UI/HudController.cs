using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SpellSimulator.DebugTools;
using SpellSimulator.Interfaces;
using SpellSimulator.Learning;
using SpellSimulator.Player;

namespace SpellSimulator.UI
{
    /// <summary>
    /// HUD de juego en UI Toolkit (reemplaza la HUD uGUI): barra de la mano principal (3 slots, teclas 1-3) y slot de la
    /// mano secundaria con miniaturas 3D (<see cref="ItemIcons"/>), panel de contexto con keycaps, selector de grosor,
    /// tinta y pagina, la tecla [E] junto a la mira y los avisos (<see cref="Hud"/>) arriba a la derecha. Nada toma el
    /// mouse (salvo el slot del grimorio, a la izquierda de la barra, que abre/cierra el libro con clic o TAB). Se oculta
    /// (salvo avisos) con el grimorio a pantalla completa o el lector de libros abiertos.
    /// Rendimiento: el arbol solo se reconstruye ante <see cref="PlayerInteractor.InventoryChanged"/> o
    /// <see cref="Hud.Changed"/>; por frame solo se comparan valores cuantizados (tinta al 1%, grosor, pagina) sin GC.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class HudController : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor interactor;

        private const float PruneInterval = 0.25f;
        private const float NamePollInterval = 0.5f;
        private const float FadeLead = 0.6f;
        private const float LowInk = 0.15f;
        private const float BlinkInterval = 0.45f;
        private const int OutMilliseconds = 300;

        private static readonly string[] Percents = BuildPercents();
        private static readonly string[] SlotKeys = { "1", "2", "3" };

        private sealed class Slot
        {
            public VisualElement Root, Frame, Icon;
            public Label Name;
            public GameObject Item;
            public string ShownName;
        }

        private sealed class NoticeCard
        {
            public string Id;
            public float Until;
            public bool Leaving;
            public bool Fading;
            /// <summary>Momento de quitar la clase de entrada / de sacarla del arbol (tiempo real; -1 = nada pendiente).</summary>
            public float ShowAt = -1f, RemoveAt = -1f;
            public VisualElement Root;
            public Label Text;
        }

        private UIDocument document;
        private VisualElement hud, hoverKey, context, ctxMain, ctxSec, mainHints, secHints, widths, ink, inkFill, noticesBox;
        private Label mainName, secName, inkPct, pageLabel, widthLabel;
        private readonly Slot[] mainSlots = new Slot[PlayerInteractor.HotbarSlots];
        private Slot secondarySlot, bookSlot;
        private bool bookSlotActive;
        private readonly List<VisualElement> widthDots = new List<VisualElement>(8);
        private readonly List<HudText.Hint> hintBuffer = new List<HudText.Hint>(10);
        private readonly List<NoticeCard> cards = new List<NoticeCard>(8);

        private bool bound;
        private bool inventoryDirty = true;
        private bool noticesDirty = true;
        private bool hidden;
        private bool hoverShown;
        private float pruneTimer, nameTimer, blinkTimer;
        private bool blinkOn;

        // Estado cacheado para comparar por frame.
        private GameObject mainItem, secondaryItem;
        private IHudInfo mainInfo, secondaryInfo;
        private IWidthSelectable mainWidths;
        private IPageable secondaryPages;
        private string mainHintsSource, secHintsSource;
        private int lastInk = int.MinValue, lastWidth = -1, lastPage = -1, lastPageCount = -1;
        private string lastDiscovered;
        private float lastDiscoveredAt;

        private void Awake()
        {
            document = GetComponent<UIDocument>();
            if (interactor == null) interactor = FindFirstObjectByType<PlayerInteractor>();
        }

        private void OnEnable()
        {
            Hud.Changed += MarkNotices;
            PlayerSettings.Changed += MarkInventory;
            DiagnosticBus.OnDiagnosticsUpdated += HandleDiagnostics;
            Discovery.Discovered += OnDiscovered;
            if (interactor != null) interactor.InventoryChanged += MarkInventory;
            bound = false;
        }

        private void OnDisable()
        {
            Hud.Changed -= MarkNotices;
            PlayerSettings.Changed -= MarkInventory;
            DiagnosticBus.OnDiagnosticsUpdated -= HandleDiagnostics;
            Discovery.Discovered -= OnDiscovered;
            if (interactor != null) interactor.InventoryChanged -= MarkInventory;
        }

        private void MarkInventory() => inventoryDirty = true;
        private void MarkNotices() => noticesDirty = true;

        // ---------------------------------------------------------------- armado

        /// <summary>Busca los elementos del UXML y crea los slots. UIDocument rehace el arbol en cada OnEnable.</summary>
        private bool Bind()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null) return false;
            hud = root.Q("hud");
            if (hud == null) return false;
            hoverKey = root.Q("hover-key");
            context = root.Q("context");
            ctxMain = root.Q("ctx-main");
            ctxSec = root.Q("ctx-sec");
            mainHints = root.Q("ctx-main-hints");
            secHints = root.Q("ctx-sec-hints");
            widths = root.Q("widths");
            ink = root.Q("ink");
            inkFill = root.Q("ink-fill");
            noticesBox = root.Q("notices");
            mainName = root.Q<Label>("ctx-main-name");
            secName = root.Q<Label>("ctx-sec-name");
            inkPct = root.Q<Label>("ink-pct");
            pageLabel = root.Q<Label>("page");

            VisualElement mainTray = root.Q("main-slots");
            mainTray.Clear();
            for (int i = 0; i < mainSlots.Length; i++) mainSlots[i] = MakeSlot(mainTray, SlotKeys[i]);
            VisualElement secTray = root.Q("sec-slots");
            secTray.Clear();
            secondarySlot = MakeSlot(secTray, "E");
            VisualElement bookTray = root.Q("book-slots");
            bookTray.Clear();
            bookSlot = MakeSlot(bookTray, "TAB");
            bookSlot.Icon.AddToClassList("slot-icon--book");
            bookSlot.Name.text = "Grimorio";
            bookSlot.Root.EnableInClassList("slot--empty", false);
            bookSlotActive = false;

            noticesBox.Clear();
            cards.Clear();
            IgnorePicking(hud);
            IgnorePicking(noticesBox);
            // Unico elemento de la HUD que recibe el mouse (cuando el cursor esta libre): abre/cierra el grimorio.
            bookSlot.Root.pickingMode = PickingMode.Position;
            bookSlot.Root.RegisterCallback<ClickEvent>(OnBookSlotClicked);
            inventoryDirty = noticesDirty = true;
            hidden = false;
            hoverShown = false;
            lastInk = int.MinValue;
            return true;
        }

        private static Slot MakeSlot(VisualElement parent, string key)
        {
            var slot = new Slot { Root = new VisualElement() };
            slot.Root.AddToClassList("slot");
            slot.Frame = new VisualElement();
            slot.Frame.AddToClassList("slot-frame");
            var glow = new VisualElement();
            glow.AddToClassList("slot-glow");
            slot.Icon = new VisualElement();
            slot.Icon.AddToClassList("slot-icon");
            var keycap = new Label(key);
            keycap.AddToClassList("keycap");
            keycap.AddToClassList("keycap--small");
            keycap.AddToClassList("slot-key");
            slot.Frame.Add(glow);
            slot.Frame.Add(slot.Icon);
            slot.Frame.Add(keycap);
            slot.Name = new Label();
            slot.Name.AddToClassList("slot-name");
            slot.Root.Add(slot.Frame);
            slot.Root.Add(slot.Name);
            parent.Add(slot.Root);
            return slot;
        }

        private static void OnBookSlotClicked(ClickEvent evt)
        {
            Grimoire book = Grimoire.Instance;
            if (book == null) return;
            if (book.IsOpen) book.Close();
            else book.Open();
        }

        private static void IgnorePicking(VisualElement e)
        {
            e.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < e.childCount; i++) IgnorePicking(e[i]);
        }

        // ---------------------------------------------------------------- frame

        private void Update()
        {
            if (!bound) { bound = Bind(); if (!bound) return; }

            pruneTimer += Time.unscaledDeltaTime;
            if (pruneTimer >= PruneInterval)
            {
                pruneTimer = 0f;
                Hud.Prune();
                FadeExpiring();
            }

            UpdateHidden();
            if (inventoryDirty) RefreshInventory();
            if (noticesDirty) RefreshNotices();
            AnimateCards();
            if (hidden) return;

            UpdateHover();
            UpdateInk();
            UpdateWidth();
            UpdatePage();

            nameTimer += Time.unscaledDeltaTime;
            if (nameTimer >= NamePollInterval)
            {
                nameTimer = 0f;
                PollNames();
            }
        }

        private void UpdateHidden()
        {
            Grimoire book = Grimoire.Instance;
            // Con el grimorio en modo normal la HUD sigue visible; solo se oculta a pantalla completa (fijo a la camara).
            bool hide = (book != null && book.IsFullscreen) || BookReaderUI.IsOpen;
            bool bookOpen = book != null && book.IsOpen;
            if (bookOpen != bookSlotActive && bookSlot != null)
            {
                bookSlotActive = bookOpen;
                bookSlot.Root.EnableInClassList("slot--active", bookOpen);
            }
            if (hide == hidden) return;
            hidden = hide;
            hud.EnableInClassList("hud--hidden", hide);
        }

        private void UpdateHover()
        {
            bool show = interactor != null && interactor.HoverInteractable && !FocusMode.Active;
            if (show == hoverShown) return;
            hoverShown = show;
            hoverKey.EnableInClassList("hover-key--on", show);
        }

        private void UpdateInk()
        {
            float gauge = mainInfo != null ? mainInfo.HudGauge : -1f;
            int q = gauge < 0f ? -1 : Mathf.Clamp(Mathf.RoundToInt(gauge * 100f), 0, 100);
            bool low = q >= 0 && q < LowInk * 100f;
            if (low)
            {
                blinkTimer += Time.unscaledDeltaTime;
                if (blinkTimer >= BlinkInterval)
                {
                    blinkTimer = 0f;
                    blinkOn = !blinkOn;
                    ink.EnableInClassList("ink--blink", blinkOn);
                }
            }
            if (q == lastInk) return;
            lastInk = q;
            ink.style.display = q < 0 ? DisplayStyle.None : DisplayStyle.Flex;
            if (q < 0) return;
            inkFill.style.width = Length.Percent(q);
            inkPct.text = Percents[q];
            ink.EnableInClassList("ink--low", low);
            if (!low) { blinkOn = false; ink.EnableInClassList("ink--blink", false); }
        }

        private void UpdateWidth()
        {
            if (mainWidths == null) return;
            int index = mainWidths.WidthIndex;
            if (index == lastWidth) return;
            lastWidth = index;
            for (int i = 0; i < widthDots.Count; i++) widthDots[i].EnableInClassList("width-dot--on", i == index);
            if (widthLabel != null) widthLabel.text = mainWidths.WidthLabel(index);
        }

        private void UpdatePage()
        {
            if (secondaryPages == null) return;
            int page = secondaryPages.Page, count = secondaryPages.PageCount;
            if (page == lastPage && count == lastPageCount) return;
            lastPage = page;
            lastPageCount = count;
            pageLabel.text = $"Página {page + 1} / {count}";
        }

        /// <summary>Algunos nombres cambian sin evento (cargas de una estampa): se comparan cada medio segundo.</summary>
        private void PollNames()
        {
            if (mainInfo != null && mainInfo.HudName != mainName.text) inventoryDirty = true;
        }

        // ---------------------------------------------------------------- inventario

        private void RefreshInventory()
        {
            inventoryDirty = false;
            if (interactor == null) return;

            int active = interactor.ActiveHotbar;
            for (int i = 0; i < mainSlots.Length; i++)
                FillSlot(mainSlots[i], interactor.HotbarItem(i), i == active);
            GameObject secondary = interactor.SecondaryItem;
            FillSlot(secondarySlot, secondary, secondary != null);

            // Panel de contexto: lo equipado y lo agarrado.
            GameObject main = interactor.MainItem;
            if (!ReferenceEquals(main, mainItem))
            {
                mainItem = main;
                mainInfo = main != null ? main.GetComponent<IHudInfo>() : null;
                mainWidths = main != null ? main.GetComponent<IWidthSelectable>() : null;
                BuildWidths();
                lastInk = int.MinValue;
            }
            if (!ReferenceEquals(secondary, secondaryItem))
            {
                secondaryItem = secondary;
                secondaryInfo = secondary != null ? secondary.GetComponentInParent<IHudInfo>() : null;
                secondaryPages = secondary != null ? secondary.GetComponentInParent<IPageable>() : null;
                lastPage = lastPageCount = -1;
            }

            ctxMain.style.display = main != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (main != null)
            {
                mainName.text = mainInfo != null ? mainInfo.HudName : HudText.PrettyName(main.name);
                SetHints(mainHints, mainInfo != null ? mainInfo.HudHints : null, ref mainHintsSource);
            }

            ctxSec.style.display = secondary != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (secondary != null)
            {
                secName.text = secondaryInfo != null ? secondaryInfo.HudName : HudText.PrettyName(secondary.name);
                pageLabel.style.display = secondaryPages != null ? DisplayStyle.Flex : DisplayStyle.None;
                string hints = HudText.SecondaryHints(secondaryPages != null,
                    secondary.GetComponentInParent<IFlippable>() != null,
                    !PlatformDetector.IsVR && secondary.GetComponentInParent<IFocusable>() != null,
                    true);
                SetHints(secHints, hints, ref secHintsSource);
            }
            ctxSec.EnableInClassList("ctx-row--sec", main != null);
            context.EnableInClassList("context--empty", main == null && secondary == null);
        }

        private void FillSlot(Slot slot, GameObject item, bool active)
        {
            slot.Root.EnableInClassList("slot--active", active && item != null);
            slot.Root.EnableInClassList("slot--empty", item == null);
            if (ReferenceEquals(item, slot.Item) && item != null)
            {
                string current = NameOf(item);
                if (current != slot.ShownName) { slot.ShownName = current; slot.Name.text = current; }
                return;
            }
            slot.Item = item;
            slot.Icon.RemoveFromClassList("slot-icon--color");
            slot.Icon.RemoveFromClassList("slot-icon--empty");
            slot.Icon.style.backgroundImage = StyleKeyword.Null;
            slot.Icon.style.unityBackgroundImageTintColor = StyleKeyword.Null;
            if (item == null)
            {
                slot.ShownName = "";
                slot.Name.text = "";
                slot.Icon.AddToClassList("slot-icon--empty");
                return;
            }
            slot.ShownName = NameOf(item);
            slot.Name.text = slot.ShownName;
            Texture icon = ItemIcons.Get(item);
            if (icon != null)
            {
                slot.Icon.style.backgroundImage = Background.FromRenderTexture((RenderTexture)icon);
                return;
            }
            // Sin malla: gema del color del objeto.
            slot.Icon.AddToClassList("slot-icon--color");
            IHudInfo info = item.GetComponentInParent<IHudInfo>();
            slot.Icon.style.unityBackgroundImageTintColor = info != null ? info.HudColor : new Color(0.85f, 0.75f, 0.55f);
        }

        /// <summary>Nombre corto del slot: el del objeto sin el detalle tras ":" (estampa "Estampa grande: Fuego (3)").</summary>
        private static string NameOf(GameObject item)
        {
            IHudInfo info = item.GetComponentInParent<IHudInfo>();
            string name = info != null ? info.HudName : HudText.PrettyName(item.name);
            int colon = name.IndexOf(':');
            return colon > 0 ? name.Substring(colon + 1).Trim() : name;
        }

        private void SetHints(VisualElement row, string source, ref string shown)
        {
            if (ReferenceEquals(source, shown) || source == shown) return;
            shown = source;
            row.Clear();
            HudText.ParseHints(source, hintBuffer);
            foreach (HudText.Hint h in hintBuffer)
            {
                var hint = new VisualElement { pickingMode = PickingMode.Ignore };
                hint.AddToClassList("hint");
                if (h.Key.Length > 0)
                {
                    var key = new Label(h.Key) { pickingMode = PickingMode.Ignore };
                    key.AddToClassList("keycap");
                    hint.Add(key);
                }
                var text = new Label(h.Action) { pickingMode = PickingMode.Ignore };
                text.AddToClassList("hint-text");
                hint.Add(text);
                row.Add(hint);
            }
        }

        private void BuildWidths()
        {
            widths.Clear();
            widthDots.Clear();
            widthLabel = null;
            lastWidth = -1;
            widths.style.display = mainWidths != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (mainWidths == null) return;
            int count = mainWidths.WidthCount;
            for (int i = 0; i < count; i++)
            {
                var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                dot.AddToClassList("width-dot");
                var mark = new VisualElement { pickingMode = PickingMode.Ignore };
                mark.AddToClassList("width-mark");
                float t = Mathf.Clamp01(mainWidths.WidthSample(i));
                mark.style.height = Mathf.Lerp(2f, 14f, t);
                mark.style.width = Mathf.Lerp(8f, 14f, t);
                mark.style.borderTopLeftRadius = mark.style.borderTopRightRadius =
                    mark.style.borderBottomLeftRadius = mark.style.borderBottomRightRadius = Mathf.Lerp(1f, 7f, t);
                dot.Add(mark);
                widths.Add(dot);
                widthDots.Add(dot);
            }
            widthLabel = new Label { pickingMode = PickingMode.Ignore };
            widthLabel.AddToClassList("width-label");
            widths.Add(widthLabel);
        }

        // ---------------------------------------------------------------- avisos

        private void RefreshNotices()
        {
            noticesDirty = false;
            IReadOnlyList<Hud.Notice> notices = Hud.Notices;

            // Bajas: tarjetas cuyo aviso ya no esta.
            for (int c = 0; c < cards.Count; c++)
            {
                NoticeCard card = cards[c];
                if (card.Leaving || IndexOf(notices, card.Id) >= 0) continue;
                Leave(card);
            }

            // Altas y cambios; el mas nuevo arriba (SendToBack lo deja primero).
            for (int i = 0; i < notices.Count; i++)
            {
                Hud.Notice n = notices[i];
                NoticeCard card = FindCard(n.Id);
                if (card == null) card = CreateCard(n);
                else if (card.Text.text != n.Text || card.Until != n.Until)
                {
                    card.Text.text = n.Text;
                    card.Until = n.Until;
                    card.Fading = false;
                    card.Root.RemoveFromClassList("notice--fading");
                    StyleCard(card);
                }
                card.Root.SendToBack();
            }
        }

        private static int IndexOf(IReadOnlyList<Hud.Notice> notices, string id)
        {
            for (int i = 0; i < notices.Count; i++) if (notices[i].Id == id) return i;
            return -1;
        }

        private NoticeCard FindCard(string id)
        {
            foreach (NoticeCard c in cards) if (!c.Leaving && c.Id == id) return c;
            return null;
        }

        private NoticeCard CreateCard(Hud.Notice n)
        {
            var card = new NoticeCard { Id = n.Id, Until = n.Until, Root = new VisualElement { pickingMode = PickingMode.Ignore } };
            card.Root.AddToClassList("notice");
            card.Root.AddToClassList("notice--in");
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("notice-icon");
            card.Text = new Label(n.Text) { pickingMode = PickingMode.Ignore };
            card.Text.AddToClassList("notice-text");
            card.Root.Add(icon);
            card.Root.Add(card.Text);
            StyleCard(card);
            noticesBox.Add(card.Root);
            cards.Add(card);
            // Un instante despues se quita la clase de entrada para que corra la transicion.
            card.ShowAt = Time.unscaledTime + 0.02f;
            return card;
        }

        private static void StyleCard(NoticeCard card)
        {
            bool permanent = card.Until <= 0f;
            card.Root.EnableInClassList("notice--warn", permanent);
            card.Root.EnableInClassList("notice--disc", !permanent && card.Id == "disc");
        }

        private void Leave(NoticeCard card)
        {
            card.Leaving = true;
            card.Root.AddToClassList("notice--out");
            card.RemoveAt = Time.unscaledTime + OutMilliseconds / 1000f;
        }

        /// <summary>Entrada y salida de las tarjetas (sin allocations: solo compara tiempos).</summary>
        private void AnimateCards()
        {
            float now = Time.unscaledTime;
            for (int i = cards.Count - 1; i >= 0; i--)
            {
                NoticeCard card = cards[i];
                if (card.ShowAt > 0f && now >= card.ShowAt) { card.ShowAt = -1f; card.Root.RemoveFromClassList("notice--in"); }
                if (card.RemoveAt > 0f && now >= card.RemoveAt)
                {
                    card.Root.RemoveFromHierarchy();
                    cards.RemoveAt(i);
                }
            }
        }

        /// <summary>Los temporales se desvanecen en su ultimo medio segundo (transicion de USS).</summary>
        private void FadeExpiring()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < cards.Count; i++)
            {
                NoticeCard card = cards[i];
                if (card.Leaving || card.Fading || card.Until <= 0f || card.Until - now > FadeLead) continue;
                card.Fading = true;
                card.Root.AddToClassList("notice--fading");
            }
        }

        private void HandleDiagnostics(string log)
        {
            string clean = HudText.CleanDiagnostic(log);
            if (clean == null) return;
            Hud.Notify("diag:" + clean, clean, 3.5f);
        }

        private void OnDiscovered(string glyph, string source)
        {
            if (glyph == null || Discovery.Instance == null) return;
            string name = Discovery.Instance.Lore.Name(glyph);
            // Varios descubrimientos juntos (un libro) se suman en el mismo aviso.
            bool recent = lastDiscovered != null && Time.unscaledTime - lastDiscoveredAt < 1f;
            lastDiscovered = recent ? lastDiscovered + ", " + name : "Descubriste: " + name;
            lastDiscoveredAt = Time.unscaledTime;
            Hud.Notify("disc", lastDiscovered, 4f);
        }

        private static string[] BuildPercents()
        {
            var p = new string[101];
            for (int i = 0; i <= 100; i++) p[i] = i + "%";
            return p;
        }
    }
}
