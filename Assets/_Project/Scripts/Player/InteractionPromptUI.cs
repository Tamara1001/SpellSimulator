using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using SpellSimulator.DebugTools;
using SpellSimulator.Learning;

namespace SpellSimulator.Player
{
    /// <summary>
    /// Avisos del juego arriba a la derecha (cambio de grosor, falta de tinta, sello roto, "Descubriste: ...") y creacion de la
    /// HUD de abajo (<see cref="PlayerHud"/>). Los temporales se apagan solos (se desvanecen el ultimo medio segundo) y los
    /// permanentes (por ejemplo "sin tinta") quedan hasta que se resuelven. Los textos solo se reasignan al cambiar la lista
    /// (sin allocations por frame). Los mensajes de DiagnosticBus que no son del hechizo pasan a ser avisos; el informe del
    /// hechizo va al panel de depuracion (F3).
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private RectTransform hud;

        private const int Slots = 6;
        private const float FadeSeconds = 0.5f;
        private const float PruneInterval = 0.25f;
        private const int MaxRawLength = 200;
        private const string HechizoTag = "<b>[Hechizo]";
        private const string DiscoveredPrefix = "Descubriste";
        private static readonly Color TemporaryColor = new Color(1f, 0.93f, 0.6f);
        private static readonly Color PermanentColor = new Color(1f, 0.6f, 0.25f);
        private static readonly Regex RichTextTag = new Regex("<[^>]+>", RegexOptions.Compiled);

        private TextMeshProUGUI[] views;
        private readonly float[] until = new float[Slots];
        private readonly Color[] baseColor = new Color[Slots];
        private bool dirty = true;
        private float pruneTimer;
        private string lastDiscovered;
        private float lastDiscoveredAt;

        private void Awake()
        {
            if (hud == null) return;
            views = new TextMeshProUGUI[Slots];
            for (int i = 0; i < Slots; i++) views[i] = MakeNotice(i);
            var bar = gameObject.GetComponent<PlayerHud>() ?? gameObject.AddComponent<PlayerHud>();
            bar.Init(interactor, hud);
        }

        private TextMeshProUGUI MakeNotice(int index)
        {
            var go = new GameObject("Aviso" + index, typeof(RectTransform));
            go.transform.SetParent(hud, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-24f, -24f - index * 30f);
            rt.sizeDelta = new Vector2(620f, 28f);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.alignment = TextAlignmentOptions.TopRight;
            t.fontSize = 20f;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.outlineWidth = 0.25f;
            t.outlineColor = new Color32(0, 0, 0, 200);
            t.raycastTarget = false;
            t.text = "";
            t.enabled = false;
            return t;
        }

        private void OnEnable()
        {
            Discovery.Discovered += OnDiscovered;
            DiagnosticBus.OnDiagnosticsUpdated += HandleDiagnostics;
            Hud.Changed += MarkDirty;
        }

        private void OnDisable()
        {
            Discovery.Discovered -= OnDiscovered;
            DiagnosticBus.OnDiagnosticsUpdated -= HandleDiagnostics;
            Hud.Changed -= MarkDirty;
        }

        private void MarkDirty() => dirty = true;

        private void Update()
        {
            if (views == null) return;
            pruneTimer += Time.unscaledDeltaTime;
            if (pruneTimer >= PruneInterval)
            {
                pruneTimer = 0f;
                Hud.Prune();
            }
            if (dirty) Rebuild();
            Fade();
        }

        /// <summary>Reasigna los textos desde la lista de avisos (solo cuando cambia). El mas nuevo va arriba.</summary>
        private void Rebuild()
        {
            dirty = false;
            var notices = Hud.Notices;
            int shown = Mathf.Min(notices.Count, Slots);
            for (int i = 0; i < Slots; i++)
            {
                TextMeshProUGUI view = views[i];
                if (i >= shown)
                {
                    if (view.enabled) view.enabled = false;
                    until[i] = -1f;
                    continue;
                }
                Hud.Notice notice = notices[notices.Count - 1 - i];
                bool permanent = notice.Until <= 0f;
                view.text = permanent ? "• " + notice.Text : notice.Text;
                baseColor[i] = permanent ? PermanentColor : TemporaryColor;
                until[i] = permanent ? float.MaxValue : notice.Until;
                view.color = baseColor[i];
                view.enabled = true;
            }
        }

        /// <summary>Desvanece los temporales en el ultimo medio segundo. Sin tocar textos ni allocations.</summary>
        private void Fade()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < Slots; i++)
            {
                if (!views[i].enabled || until[i] == float.MaxValue) continue;
                float left = until[i] - now;
                if (left > FadeSeconds) continue;
                Color c = baseColor[i];
                c.a = Mathf.Clamp01(left / FadeSeconds);
                views[i].color = c;
            }
        }

        private void HandleDiagnostics(string log)
        {
            if (views == null || string.IsNullOrEmpty(log)) return;
            // El informe del hechizo es para el panel de depuracion, no para el jugador.
            if (log.StartsWith(HechizoTag, StringComparison.Ordinal) || log.Length >= MaxRawLength) return;
            string clean = RichTextTag.Replace(log, "").Trim().Replace("\r", "").Replace("\n", " - ");
            if (clean.Length == 0) return;
            Hud.Notify("diag:" + clean, clean, 3.5f);
        }

        private void OnDiscovered(string glyph, string source)
        {
            if (views == null || glyph == null || Discovery.Instance == null) return;
            string name = Discovery.Instance.Lore.Name(glyph);
            // Varios descubrimientos juntos (un libro) se suman en el mismo aviso.
            bool recent = lastDiscovered != null && Time.unscaledTime - lastDiscoveredAt < 1f;
            lastDiscovered = recent ? lastDiscovered + ", " + name : DiscoveredPrefix + ": " + name;
            lastDiscoveredAt = Time.unscaledTime;
            Hud.Notify("disc", lastDiscovered, 4f);
        }
    }
}
