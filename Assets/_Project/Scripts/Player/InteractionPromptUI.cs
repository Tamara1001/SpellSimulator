using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using SpellSimulator.DebugTools;
using SpellSimulator.Learning;

namespace SpellSimulator.Player
{
    /// <summary>
    /// Texto bajo la mira: que se puede hacer con lo que se mira ("Leer ...", "Pagina siguiente", "Agarrar Lapiz") y
    /// avisos breves ("Descubriste: Agua") y mensajes del juego (estampa, sello roto, tinta...) que llegan por
    /// DiagnosticBus. Los avisos se apilan arriba del prompt (hasta 3, cada uno dura unos segundos y se desvanece).
    /// Se crea solo dentro del Canvas de la HUD.
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private RectTransform hud;
        [SerializeField] private float toastSeconds = 3.5f;

        private const int ToastSlots = 3;
        private const float ToastFadeSeconds = 0.5f;
        private const float ToastBaseY = -4f;
        private const float ToastStepY = 36f;
        private const int ToastMaxRawLength = 200;
        private const string HechizoTag = "<b>[Hechizo]";
        private const string DiscoveredPrefix = "Descubriste";
        private static readonly Color ToastColor = new Color(1f, 0.9f, 0.55f);
        private static readonly Regex RichTextTag = new Regex("<[^>]+>", RegexOptions.Compiled);

        private TextMeshProUGUI prompt;
        // Pool preasignado. Indice 0 = aviso mas nuevo (el mas cercano al prompt); los anteriores suben.
        private TextMeshProUGUI[] toastViews;
        private readonly float[] toastUntil = new float[ToastSlots];
        private string lastPrompt;

        private void Awake()
        {
            if (hud == null) return;
            prompt = Make("Prompt", new Vector2(0f, -40f), 22f, new Color(1f, 1f, 1f, 0.9f));
            toastViews = new TextMeshProUGUI[ToastSlots];
            for (int i = 0; i < ToastSlots; i++)
            {
                toastViews[i] = Make("Toast" + i, new Vector2(0f, ToastBaseY + i * ToastStepY), 24f, ToastColor);
                toastViews[i].text = "";
                toastViews[i].enabled = false;
            }
        }

        private void OnEnable()
        {
            Discovery.Discovered += OnDiscovered;
            DiagnosticBus.OnDiagnosticsUpdated += HandleDiagnostics;
        }

        private void OnDisable()
        {
            Discovery.Discovered -= OnDiscovered;
            DiagnosticBus.OnDiagnosticsUpdated -= HandleDiagnostics;
        }

        private TextMeshProUGUI Make(string name, Vector2 offset, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(hud, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(900f, 80f);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.alignment = TextAlignmentOptions.Center;
            t.fontSize = size;
            t.color = color;
            t.outlineWidth = 0.25f;
            t.outlineColor = new Color32(0, 0, 0, 200);
            t.raycastTarget = false;
            return t;
        }

        private void Update()
        {
            if (prompt != null)
            {
                string current = interactor != null ? interactor.HoverPrompt : "";
                // Solo se reasigna el texto cuando cambia (sin GC por frame).
                if (!ReferenceEquals(current, lastPrompt))
                {
                    lastPrompt = current;
                    prompt.text = string.IsNullOrEmpty(current) ? "" : (interactor.HoverUsesClick ? $"[Clic] {current}" : $"[E] {current}");
                }
            }
            if (toastViews != null) UpdateToasts();
        }

        /// <summary>Oculta los avisos vencidos y aplica el desvanecido de los ultimos segundos. Sin allocations.</summary>
        private void UpdateToasts()
        {
            float now = Time.time;
            for (int i = 0; i < ToastSlots; i++)
            {
                TextMeshProUGUI view = toastViews[i];
                float left = toastUntil[i] - now;
                if (left <= 0f)
                {
                    if (view.enabled) view.enabled = false;
                    continue;
                }
                if (!view.enabled) view.enabled = true;
                float alpha = left < ToastFadeSeconds ? left / ToastFadeSeconds : 1f;
                view.color = new Color(ToastColor.r, ToastColor.g, ToastColor.b, alpha);
            }
        }

        /// <summary>Muestra un aviso arriba del prompt. Si es igual al mas nuevo y sigue visible, solo renueva su tiempo.</summary>
        private void ShowToast(string message)
        {
            float now = Time.time;
            if (toastUntil[0] > now && toastViews[0].text == message)
            {
                toastUntil[0] = now + toastSeconds;
                return;
            }
            // Los avisos suben una posicion; el mas viejo del tope se descarta.
            for (int i = ToastSlots - 1; i > 0; i--)
            {
                toastViews[i].text = toastViews[i - 1].text;
                toastUntil[i] = toastUntil[i - 1];
            }
            toastViews[0].text = message;
            toastUntil[0] = now + toastSeconds;
        }

        private void HandleDiagnostics(string log)
        {
            if (toastViews == null || string.IsNullOrEmpty(log)) return;
            // Los logs del hechizo son para el panel de depuracion, no para el jugador.
            if (log.StartsWith(HechizoTag, StringComparison.Ordinal) || log.Length >= ToastMaxRawLength) return;
            string clean = RichTextTag.Replace(log, "").Trim().Replace("\r", "").Replace("\n", " - ");
            if (clean.Length == 0) return;
            ShowToast(clean);
        }

        private void OnDiscovered(string glyph, string source)
        {
            if (toastViews == null || glyph == null || Discovery.Instance == null) return;
            string name = Discovery.Instance.Lore.Name(glyph);
            // Varios descubrimientos juntos (un libro) se suman en el mismo aviso.
            bool recent = toastViews[0].text.StartsWith(DiscoveredPrefix, StringComparison.Ordinal)
                          && Time.time < toastUntil[0] - toastSeconds + 1f;
            if (recent)
            {
                toastViews[0].text = $"{toastViews[0].text}, {name}";
                toastUntil[0] = Time.time + toastSeconds;
            }
            else
            {
                ShowToast($"{DiscoveredPrefix}: {name}");
            }
        }
    }
}
