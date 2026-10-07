using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Player
{
    /// <summary>
    /// HUD de abajo, estilo Minecraft: barra de 4 slots (2 por mano: teclas 1-2 derecha, 3-4 izquierda) y, encima, una barra
    /// de contexto con lo que se mira o se sostiene, sus teclas de uso, la fila de grosores de la pluma (teclas 5 a 0) y la
    /// barra de tinta. Todo se crea una vez; los textos solo se reasignan cuando cambia lo que muestran (sin allocations por
    /// frame). Los avisos temporales van arriba a la derecha (<see cref="InteractionPromptUI"/>).
    /// </summary>
    public class PlayerHud : MonoBehaviour
    {
        private const float SlotSize = 68f;
        private const float SlotGap = 6f;
        private const float GroupGap = 40f;
        private const float BarY = 18f;
        private const int MaxWidths = 8;
        private static readonly Color Dim = new Color(0.08f, 0.08f, 0.1f, 0.62f);
        private static readonly Color Active = new Color(1f, 0.85f, 0.3f, 1f);
        private static readonly Color Idle = new Color(0.55f, 0.55f, 0.6f, 0.9f);
        private static readonly string[] WidthKeys = { "5", "6", "7", "8", "9", "0", "-", "=" };
        private static readonly string[] SlotKeys = { "1", "2", "3", "4" }; // indice = tecla; 0-1 derecha, 2-3 izquierda

        private struct Slot
        {
            public Image border, icon;
            public TextMeshProUGUI key, label;
        }

        private struct WidthCell
        {
            public RectTransform root;
            public Image border, line;
            public TextMeshProUGUI key, label;
        }

        private PlayerInteractor interactor;
        private RectTransform hud;
        private readonly Slot[] slots = new Slot[4];
        private readonly WidthCell[] widthCells = new WidthCell[MaxWidths];
        private RectTransform contextPanel;
        private TextMeshProUGUI hoverLine, rightLine, leftLine, inkLabel;
        private RectTransform widthRow, inkRow, inkFill;
        private Image inkFillImage;

        // Firma de lo que se dibuja: si no cambia, no se toca ningun texto.
        private int signature = int.MinValue;
        private string lastHover;
        private bool lastHoverClick;
        private int lastInk = -2;

        public void Init(PlayerInteractor owner, RectTransform root)
        {
            interactor = owner;
            hud = root;
            if (hud == null || interactor == null) { enabled = false; return; }
            BuildSlots();
            BuildContext();
        }

        // ---------------------------------------------------------------- construccion

        private static RectTransform Rect(string name, RectTransform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private static Image Box(string name, RectTransform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            var rt = Rect(name, parent, anchor, pivot, pos, size);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI Text(string name, RectTransform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
            float fontSize, TextAlignmentOptions align, Color color)
        {
            var rt = Rect(name, parent, anchor, pivot, pos, size);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.outlineWidth = 0.2f;
            t.outlineColor = new Color32(0, 0, 0, 200);
            return t;
        }

        private void BuildSlots()
        {
            var bottom = new Vector2(0.5f, 0f);
            float groupWidth = SlotSize * 2f + SlotGap;
            // Izquierda: teclas 3 y 4. Derecha: teclas 1 y 2.
            float[] xs =
            {
                GroupGap / 2f + SlotSize / 2f,                    // tecla 1
                GroupGap / 2f + SlotSize * 1.5f + SlotGap,        // tecla 2
                -GroupGap / 2f - groupWidth + SlotSize / 2f,      // tecla 3
                -GroupGap / 2f - groupWidth + SlotSize * 1.5f + SlotGap // tecla 4
            };
            for (int i = 0; i < 4; i++)
            {
                var root = Rect("Slot" + SlotKeys[i], hud, bottom, new Vector2(0.5f, 0f), new Vector2(xs[i], BarY), new Vector2(SlotSize, SlotSize));
                Box("Fondo", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SlotSize, SlotSize), Dim);
                slots[i].border = Box("Borde", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SlotSize + 6f, SlotSize + 6f), Idle);
                slots[i].border.transform.SetAsFirstSibling();
                slots[i].icon = Box("Icono", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(34f, 34f), Idle);
                slots[i].key = Text("Tecla", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, -2f), new Vector2(24f, 20f), 16f, TextAlignmentOptions.TopLeft, Color.white);
                slots[i].key.text = SlotKeys[i];
                slots[i].label = Text("Nombre", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(SlotSize - 4f, 16f), 11f, TextAlignmentOptions.Center, Color.white);
            }
            // Rotulos de cada mano.
            Text("ManoIzq", hud, bottom, new Vector2(0.5f, 0f), new Vector2(-GroupGap / 2f - groupWidth / 2f, BarY + SlotSize + 8f), new Vector2(140f, 18f), 13f, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.75f)).text = "Mano izquierda";
            Text("ManoDer", hud, bottom, new Vector2(0.5f, 0f), new Vector2(GroupGap / 2f + groupWidth / 2f, BarY + SlotSize + 8f), new Vector2(140f, 18f), 13f, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.75f)).text = "Mano derecha";
        }

        private void BuildContext()
        {
            var bottom = new Vector2(0.5f, 0f);
            contextPanel = Rect("Contexto", hud, bottom, new Vector2(0.5f, 0f), new Vector2(0f, BarY + SlotSize + 32f), new Vector2(980f, 120f));
            var back = contextPanel.gameObject.AddComponent<Image>();
            back.color = new Color(0.05f, 0.05f, 0.08f, 0.55f);
            back.raycastTarget = false;

            var top = new Vector2(0.5f, 1f);
            hoverLine = Text("Mirando", contextPanel, top, top, new Vector2(0f, -4f), new Vector2(960f, 24f), 19f, TextAlignmentOptions.Center, new Color(1f, 0.95f, 0.7f));
            rightLine = Text("ManoDer", contextPanel, top, top, new Vector2(0f, -28f), new Vector2(960f, 22f), 16f, TextAlignmentOptions.Center, Color.white);
            leftLine = Text("ManoIzq", contextPanel, top, top, new Vector2(0f, -50f), new Vector2(960f, 22f), 16f, TextAlignmentOptions.Center, Color.white);

            widthRow = Rect("Grosores", contextPanel, top, top, new Vector2(0f, -74f), new Vector2(960f, 44f));
            for (int i = 0; i < MaxWidths; i++)
            {
                var cell = Rect("G" + i, widthRow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 42f));
                widthCells[i].root = cell;
                widthCells[i].border = Box("Borde", cell, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 42f), Idle);
                Box("Fondo", cell, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(66f, 38f), new Color(0.07f, 0.07f, 0.09f, 0.95f));
                widthCells[i].line = Box("Muestra", cell, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(50f, 4f), Color.white);
                widthCells[i].key = Text("Tecla", cell, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, -1f), new Vector2(20f, 16f), 14f, TextAlignmentOptions.TopLeft, Active);
                widthCells[i].label = Text("Nombre", cell, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1f), new Vector2(66f, 13f), 10f, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.85f));
            }

            inkRow = Rect("Tinta", contextPanel, top, top, new Vector2(0f, -100f), new Vector2(960f, 18f));
            var inkBack = Box("Fondo", inkRow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-40f, 0f), new Vector2(220f, 10f), new Color(0.1f, 0.1f, 0.14f, 0.95f));
            inkFillImage = Box("Relleno", (RectTransform)inkBack.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(220f, 10f), new Color(0.25f, 0.4f, 0.95f));
            inkFill = (RectTransform)inkFillImage.transform;
            inkLabel = Text("Etiqueta", inkRow, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(80f, 0f), new Vector2(200f, 18f), 14f, TextAlignmentOptions.Left, Color.white);
            contextPanel.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- actualizacion

        private static IHudInfo Info(GameObject go) => go != null && go.TryGetComponent(out IHudInfo info) ? info : null;

        private void Update()
        {
            if (interactor == null || hud == null) return;
            GameObject rightItem = interactor.SlotItem(HandType.Right, interactor.ActiveSlot(HandType.Right));
            GameObject leftItem = interactor.SlotItem(HandType.Left, interactor.ActiveSlot(HandType.Left));
            GameObject remote = interactor.RemoteHeld;
            string hover = interactor.HoverPrompt;

            // Firma barata: ids de los objetos de los 4 slots, activos, remoto, grosor, y el aviso mirado.
            int sig = 17;
            for (int i = 0; i < 4; i++)
            {
                GameObject item = interactor.SlotItem(i < 2 ? HandType.Right : HandType.Left, i % 2);
                sig = sig * 31 + (item != null ? item.GetInstanceID() : 0);
            }
            sig = sig * 31 + interactor.ActiveSlot(HandType.Right) * 3 + interactor.ActiveSlot(HandType.Left);
            sig = sig * 31 + (remote != null ? remote.GetInstanceID() : 0);
            var widthSource = WidthSource(rightItem, leftItem);
            sig = sig * 31 + (widthSource != null ? widthSource.WidthIndex + 1 : 0);
            bool hoverClick = interactor.HoverUsesClick;
            bool changed = sig != signature || !ReferenceEquals(hover, lastHover) || hoverClick != lastHoverClick;

            float gauge = Gauge(rightItem, leftItem);
            int ink = gauge < 0f ? -1 : Mathf.RoundToInt(gauge * 100f);
            if (!changed && ink == lastInk) return;

            signature = sig;
            lastHover = hover;
            lastHoverClick = hoverClick;
            lastInk = ink;
            Refresh(rightItem, leftItem, remote, hover, hoverClick, widthSource, ink);
        }

        private static IWidthSelectable WidthSource(GameObject right, GameObject left)
        {
            if (right != null && right.TryGetComponent(out IWidthSelectable w)) return w;
            if (left != null && left.TryGetComponent(out IWidthSelectable w2)) return w2;
            return null;
        }

        private static float Gauge(GameObject right, GameObject left)
        {
            var a = Info(right);
            if (a != null && a.HudGauge >= 0f) return a.HudGauge;
            var b = Info(left);
            return b != null ? b.HudGauge : -1f;
        }

        private void Refresh(GameObject rightItem, GameObject leftItem, GameObject remote, string hover, bool hoverClick, IWidthSelectable widths, int ink)
        {
            // Slots.
            for (int i = 0; i < 4; i++)
            {
                HandType hand = i < 2 ? HandType.Right : HandType.Left;
                int index = i % 2;
                GameObject item = interactor.SlotItem(hand, index);
                bool active = interactor.ActiveSlot(hand) == index;
                slots[i].border.color = active ? Active : Idle;
                var info = Info(item);
                slots[i].icon.enabled = item != null;
                if (item != null) slots[i].icon.color = info != null ? info.HudColor : Idle;
                slots[i].label.text = item != null ? (info != null ? info.HudName : item.name) : "";
            }

            // Contexto.
            int rows = 0;
            float y = -4f;
            bool hasHover = !string.IsNullOrEmpty(hover);
            string hoverText = hasHover ? (hoverClick ? "[Clic] " + hover : "[E] " + hover) : null;
            if (remote != null)
            {
                hoverText = "Moviendo: " + remote.name + "  |  Rueda: distancia  |  Clic der. + mouse: girar  |  Clic rueda: flotar  |  E: soltar  |  Q: arrojar";
            }
            rows += Place(hoverLine, hoverText, ref y, 24f);

            var right = Info(rightItem);
            var left = Info(leftItem);
            rows += Place(rightLine, right != null ? "Der.: " + right.HudName + "  -  " + right.HudHints : null, ref y, 22f);
            rows += Place(leftLine, left != null ? "Izq.: " + left.HudName + "  -  " + left.HudHints : null, ref y, 22f);

            bool showWidths = widths != null;
            widthRow.gameObject.SetActive(showWidths);
            if (showWidths)
            {
                widthRow.anchoredPosition = new Vector2(0f, y - 2f);
                int count = Mathf.Min(widths.WidthCount, MaxWidths);
                float total = count * 74f;
                for (int i = 0; i < MaxWidths; i++)
                {
                    bool used = i < count;
                    widthCells[i].root.gameObject.SetActive(used);
                    if (!used) continue;
                    widthCells[i].root.anchoredPosition = new Vector2(-total / 2f + 37f + i * 74f, 0f);
                    widthCells[i].border.color = i == widths.WidthIndex ? Active : Idle;
                    widthCells[i].key.text = WidthKeys[i];
                    widthCells[i].label.text = widths.WidthLabel(i);
                    widthCells[i].line.rectTransform.sizeDelta = new Vector2(50f, Mathf.Lerp(1f, 14f, Mathf.Clamp01(widths.WidthSample(i))));
                }
                y -= 46f;
                rows++;
            }

            bool showInk = ink >= 0;
            inkRow.gameObject.SetActive(showInk);
            if (showInk)
            {
                inkRow.anchoredPosition = new Vector2(0f, y - 2f);
                inkFill.sizeDelta = new Vector2(220f * ink / 100f, 10f);
                inkFillImage.color = ink < 15 ? new Color(0.95f, 0.3f, 0.25f) : new Color(0.25f, 0.4f, 0.95f);
                inkLabel.text = ink < 15 ? "Tinta " + ink + "%  (R para recargar)" : "Tinta " + ink + "%";
                y -= 20f;
                rows++;
            }

            contextPanel.gameObject.SetActive(rows > 0);
            contextPanel.sizeDelta = new Vector2(contextPanel.sizeDelta.x, Mathf.Max(24f, -y + 6f));
        }

        private static int Place(TextMeshProUGUI line, string text, ref float y, float height)
        {
            bool has = !string.IsNullOrEmpty(text);
            line.gameObject.SetActive(has);
            if (!has) return 0;
            line.text = text;
            line.rectTransform.anchoredPosition = new Vector2(0f, y);
            y -= height;
            return 1;
        }
    }
}
