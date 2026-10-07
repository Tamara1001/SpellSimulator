using TMPro;
using UnityEngine;
using SpellSimulator.Learning;

namespace SpellSimulator.Player
{
    /// <summary>
    /// Texto bajo la mira: que se puede hacer con lo que se mira ("Leer ...", "Pagina siguiente", "Agarrar Lapiz") y
    /// avisos breves ("Descubriste: Agua"). Se crea solo dentro del Canvas de la HUD. En VR la HUD de pantalla no se ve:
    /// el aviso de descubrimiento tambien sale por el panel de diagnostico.
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private RectTransform hud;
        [SerializeField] private float toastSeconds = 3.5f;

        private TextMeshProUGUI prompt, toast;
        private float toastUntil;
        private string lastPrompt;

        private void Awake()
        {
            if (hud == null) return;
            prompt = Make("Prompt", new Vector2(0f, -40f), 22f, new Color(1f, 1f, 1f, 0.9f));
            toast = Make("Toast", new Vector2(0f, 120f), 30f, new Color(1f, 0.9f, 0.55f));
            toast.text = "";
        }

        private void OnEnable() => Discovery.Discovered += OnDiscovered;
        private void OnDisable() => Discovery.Discovered -= OnDiscovered;

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
            if (prompt == null) return;
            string current = interactor != null ? interactor.HoverPrompt : "";
            // Solo se reasigna el texto cuando cambia (sin GC por frame).
            if (!ReferenceEquals(current, lastPrompt))
            {
                lastPrompt = current;
                prompt.text = string.IsNullOrEmpty(current) ? "" : (interactor.HoverUsesClick ? $"[Clic] {current}" : $"[E] {current}");
            }
            if (toast.text.Length > 0 && Time.time > toastUntil) toast.text = "";
        }

        private void OnDiscovered(string glyph, string source)
        {
            if (toast == null || glyph == null || Discovery.Instance == null) return;
            string name = Discovery.Instance.Lore.Name(glyph);
            // Varios descubrimientos juntos (un libro) se suman en el mismo aviso.
            bool recent = toast.text.StartsWith("Descubriste") && Time.time < toastUntil - toastSeconds + 1f;
            toast.text = recent ? $"{toast.text}, {name}" : $"Descubriste: {name}";
            toastUntil = Time.time + toastSeconds;
        }
    }
}
