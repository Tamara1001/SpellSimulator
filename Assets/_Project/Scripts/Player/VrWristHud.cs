using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpellSimulator.Player
{
    /// <summary>
    /// [VR] Panel flotante junto a la mano izquierda con la barra de objetos (3 slots, el equipado marcado), la tinta y los avisos.
    /// La HUD de pantalla no se ve en el visor. Aparece cuando mirás la mano (o unos segundos tras un cambio) y siempre mira
    /// a la cabeza. Se arma en codigo (Canvas de mundo + un solo texto TMP); el texto se rehace solo cuando algo cambia,
    /// no por cuadro. Lo crea <see cref="PlayerInteractor"/> en VR.
    /// </summary>
    public class VrWristHud : MonoBehaviour
    {
        private const float PanelWidth = 440f, PanelHeight = 250f; // pixeles del canvas
        private const float PixelSize = 0.00038f;                   // metros por pixel (~17 x 9,5 cm)
        private const float LookCos = 0.8f;                          // ~37 grados del centro de la vista
        private const float ShowSecondsAfterChange = 3f;
        private const float HandReach = 1.1f;
        private const float RefreshSeconds = 0.25f;

        private PlayerInteractor interactor;
        private Transform hand;
        private UnityEngine.Canvas canvas; // «Canvas» solo choca con el namespace SpellSimulator.Canvas
        private TextMeshProUGUI label;
        private readonly StringBuilder text = new StringBuilder(256);
        private bool dirty = true;
        private float showUntil;
        private float nextRefresh;
        private int lastInkPercent = -2;
        private Camera head;

        public void Init(PlayerInteractor owner, Transform leftHand)
        {
            interactor = owner;
            hand = leftHand;
            Build();
            interactor.InventoryChanged += OnChanged;
            Hud.Changed += OnChanged;
        }

        private void OnDestroy()
        {
            if (interactor != null) interactor.InventoryChanged -= OnChanged;
            Hud.Changed -= OnChanged;
        }

        private void OnChanged()
        {
            dirty = true;
            showUntil = Time.unscaledTime + ShowSecondsAfterChange;
        }

        private void Build()
        {
            var go = new GameObject("VrWristHudCanvas");
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<UnityEngine.Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            rect.localScale = Vector3.one * PixelSize;

            var bg = new GameObject("Fondo").AddComponent<Image>();
            bg.transform.SetParent(go.transform, false);
            bg.color = new Color(0.07f, 0.05f, 0.1f, 0.82f);
            bg.raycastTarget = false;
            var bgRect = bg.rectTransform;
            bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one; bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;

            var textGo = new GameObject("Texto");
            textGo.transform.SetParent(go.transform, false);
            label = textGo.AddComponent<TextMeshProUGUI>();
            label.fontSize = 30f;
            label.color = new Color(0.95f, 0.9f, 0.8f);
            label.alignment = TextAlignmentOptions.TopLeft;
            label.raycastTarget = false;
            label.richText = true;
            label.enableWordWrapping = true;
            var tRect = label.rectTransform;
            tRect.anchorMin = Vector2.zero; tRect.anchorMax = Vector2.one;
            tRect.offsetMin = new Vector2(16f, 10f); tRect.offsetMax = new Vector2(-16f, -10f);
            canvas.enabled = false;
        }

        private void LateUpdate()
        {
            if (canvas == null || hand == null) return;
            if (head == null) head = Camera.main;
            if (head == null) return;

            Vector3 eye = head.transform.position;
            Vector3 toHand = hand.position - eye;
            float distance = toHand.magnitude;
            bool looking = distance < HandReach && distance > 0.05f && Vector3.Dot(head.transform.forward, toHand / distance) > LookCos;
            bool show = looking || Time.unscaledTime < showUntil;
            if (canvas.enabled != show) canvas.enabled = show;
            if (!show) return;

            // Sobre la mano, mirando a la cabeza (el texto se lee de frente).
            Vector3 pos = hand.position + Vector3.up * 0.14f;
            canvas.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(pos - eye, Vector3.up));

            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + RefreshSeconds;
                Hud.Prune();
                int ink = InkPercent();
                if (ink != lastInkPercent) { lastInkPercent = ink; dirty = true; }
            }
            if (dirty) Rebuild();
        }

        private int InkPercent()
        {
            GameObject main = interactor.MainItem;
            IHudInfo info = main != null ? main.GetComponentInParent<IHudInfo>() : null;
            return info != null && info.HudGauge >= 0f ? Mathf.RoundToInt(info.HudGauge * 100f) : -1;
        }

        private void Rebuild()
        {
            dirty = false;
            text.Clear();
            text.Append("<color=#c9b88a><b>Barra</b></color>\n");
            int active = interactor.ActiveHotbar;
            for (int i = 0; i < interactor.SlotCount; i++)
            {
                GameObject item = interactor.HotbarItem(i);
                bool on = i == active && interactor.MainItem != null;
                text.Append(on ? "<color=#ffd966><b>> " : "<color=#cfc7b8>  ").Append(i + 1).Append("  ");
                text.Append(item != null ? NameOf(item) : "-");
                text.Append(on ? "</b></color>\n" : "</color>\n");
            }
            if (lastInkPercent >= 0) text.Append("Tinta ").Append(lastInkPercent).Append("%\n");
            GameObject held = interactor.SecondaryItem;
            if (held != null) text.Append("<color=#9fd3ff>Mano: ").Append(NameOf(held)).Append("</color>\n");
            var notices = Hud.Notices;
            for (int i = Mathf.Max(0, notices.Count - 2); i < notices.Count; i++)
                text.Append("<size=80%><color=#e8c48a>").Append(notices[i].Text).Append("</color></size>\n");
            label.text = text.ToString();
        }

        private static string NameOf(GameObject item)
        {
            IHudInfo info = item.GetComponentInParent<IHudInfo>();
            string name = info != null ? info.HudName : item.name;
            int colon = name.IndexOf(':');
            return colon > 0 ? name.Substring(colon + 1).Trim() : name;
        }
    }
}
