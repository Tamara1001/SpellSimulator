using System;
using TMPro;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Core;
using SpellSimulator.DebugTools;
using SpellSimulator.Glyphs;
using SpellSimulator.Interfaces;
using SpellSimulator.Learning;
using SpellSimulator.Parsing;
using SpellSimulator.Player;

namespace SpellSimulator.World
{
    /// <summary>
    /// Estampa (GAME_DESIGN, objetos). Se carga apuntando al grimorio abierto en la pagina de un glifo y usando el
    /// secundario; despues el primario sobre un papel deja ese glifo:
    /// - un sigilo con su anillo ABIERTO del tamaño de la estampa (el sello queda preparado; se cierra a mano);
    /// - un signo del tamaño del anillo que ya hay, apuntando a su centro.
    /// Cargas limitadas por carga (decision de diseño): al agotarse hay que volver a cargarla.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public class Stamp : MonoBehaviour, IInteractable, IUsable, IResettable, IHudInfo
    {
        /// <summary>(estampa, lienzo) al estampar.</summary>
        public static event Action<Stamp, DrawingCanvas> Stamped;
        /// <summary>(estampa, glifo) al cargarla.</summary>
        public static event Action<Stamp, string> Loaded;

        [SerializeField] private string sizeName = "M";
        [SerializeField, Tooltip("Diametro del sello que estampa (m). El tamaño es la potencia (ley extensiva).")]
        private float diameter = 0.2f;
        [SerializeField] private int chargesPerLoad = 5;
        [SerializeField] private float pcReach = 5f, vrReach = 10f;
        [SerializeField] private LayerMask canvasLayer = ~0;
        [SerializeField] private Transform raycastOrigin;
        [SerializeField] private SpellCaster caster;
        [SerializeField] private GlyphDictionaryAsset dictionary;
        [SerializeField, Tooltip("Etiqueta con el glifo cargado y las cargas.")]
        private TextMeshPro label;

        /// <summary>La estampa que se lleva en la mano (la usa el boton del grimorio para cargarla).</summary>
        public static Stamp Held { get; private set; }

        private Rigidbody rb;
        private Collider col;
        private Camera cam;
        private string glyph;
        private int charges;
        private string prompt;

        public string Glyph => glyph;

        // ---- Interfaz (HUD de abajo)
        public string HudName => string.IsNullOrEmpty(glyph) ? $"Estampa {sizeName}" : $"Estampa {sizeName}: {(Discovery.Instance != null ? Discovery.Instance.Lore.Name(glyph) : glyph)} ({charges})";
        public Color HudColor => new Color(0.65f, 0.4f, 0.85f);
        public string HudHints => string.IsNullOrEmpty(glyph) ? "Vacia: abri un glifo en el grimorio (Tab) y 'Cargar en la estampa' | Q: arrojar" : "Clic: estampar | Q: arrojar";
        public float HudGauge => -1f;

        // En la mano una estampa enorme taparia la camara: se ve a un tamaño comodo (estampa igual al tamaño real).
        private const float HeldVisualRadius = 0.07f;
        private float HeldScale => Mathf.Min(1f, HeldVisualRadius / Mathf.Max(0.01f, diameter * 0.5f));
        public int Charges => charges;
        public HandType PreferredHand => HandType.Right;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
            if (raycastOrigin == null) raycastOrigin = transform;
            RefreshLabel();
        }

        public void Configure(string size, float meters, SpellCaster spellCaster, GlyphDictionaryAsset glyphs, TextMeshPro text)
        {
            sizeName = size;
            diameter = meters;
            caster = spellCaster;
            dictionary = glyphs;
            label = text;
            prompt = null;
        }

        // ---------------------------------------------------------------- agarrar

        public string GetInteractionPrompt() => prompt ??= $"Agarrar estampa {sizeName}";

        public void OnInteract(Transform holdPoint)
        {
            Held = this;
            rb.isKinematic = true;
            col.enabled = false;
            transform.SetParent(holdPoint);
            transform.localPosition = Vector3.zero;
            transform.localScale = Vector3.one * HeldScale;
            // En PC se sostiene con la base hacia adelante, como para estampar; en VR sigue al control.
            transform.localRotation = PlatformDetector.IsVR ? Quaternion.identity : Quaternion.Euler(-70f, 0f, 0f);
        }

        /// <summary>Reinicio del mundo: la estampa vuelve a quedar vacia.</summary>
        public void ResetToInitial()
        {
            glyph = null;
            charges = 0;
            RefreshLabel();
        }

        public void OnDrop()
        {
            if (Held == this) Held = null;
            transform.SetParent(null);
            transform.localScale = Vector3.one;
            rb.isKinematic = false;
            col.enabled = true;
        }

        // ---------------------------------------------------------------- usar

        public void OnUseStart()
        {
            if (!AimAtCanvas(out RaycastHit hit, out DrawingCanvas canvas)) return;
            if (string.IsNullOrEmpty(glyph) || charges <= 0)
            {
                Say("La estampa esta vacia. Abri el grimorio en un glifo y cargala (clic derecho / A apuntando al libro).");
                return;
            }
            var entry = dictionary != null ? dictionary.Dictionary.Get(glyph) : null;
            if (entry == null || entry.TemplateStrokes.Count == 0) return;

            Vector2 uv = canvas.GetUVFromWorldPosition(hit.point);
            canvas.NoteInkViewer(cam != null ? cam.transform.position : hit.point + hit.normal);
            Vector2 size = canvas.WorldSize;
            float width = Mathf.Max(0.004f, diameter * 0.03f);
            var strokes = entry.Kind == GlyphKind.Sigil
                ? StampPattern.Sigil(entry.TemplateStrokes, uv, diameter, size, width)
                : SignStrokes(entry, canvas, uv, size, width);
            if (strokes == null) return;

            foreach (var stroke in strokes)
            {
                canvas.Session.AddStroke(stroke);
                for (int i = 0; i < stroke.Points.Count; i++) canvas.DrawAt(stroke.Points[i], i == 0, stroke.Width);
            }
            charges--;
            RefreshLabel();
            Stamped?.Invoke(this, canvas);
            // Un sigilo estampado deja el anillo abierto: queda preparado. Un signo puede completar un sello ya cerrado.
            if (caster != null) caster.OnSealEdited(canvas);
        }

        private System.Collections.Generic.List<Stroke> SignStrokes(GlyphEntry entry, DrawingCanvas canvas, Vector2 uv, Vector2 size, float width)
        {
            // Busca el anillo que ya hay en el papel para dimensionar y orientar el signo.
            Vector2 iso = canvas.UvToIsotropic;
            var ring = canvas.Session.IsEmpty ? RingInfo.NotFound()
                : RingDetector.Detect(StrokeCleaner.CleanSession(canvas.Session.Scaled(iso), StrokeCleaner.DefaultTolerance, StrokeCleaner.DefaultSplineResolution));
            if (!ring.Found)
                return StampPattern.Template(entry.TemplateStrokes, uv, diameter * 0.5f, 0f, size, width);
            Vector2 centerUv = new Vector2(ring.Center.x / iso.x, ring.Center.y / iso.y);
            float radiusM = ring.Radius * Mathf.Max(size.x, size.y);
            return StampPattern.Sign(entry.TemplateStrokes, uv, radiusM, centerUv, size, width);
        }

        public void OnUseHold() { }
        public void OnUseEnd() { }

        /// <summary>Secundario: cargar desde la pagina abierta del grimorio.</summary>
        public void OnSecondaryUseStart()
        {
            var book = Grimoire.Instance;
            if (book == null || !book.IsOpen) { Say("Abri el grimorio (Tab) en un glifo y apreta el boton Cargar en la estampa."); return; }
            if (!Aim(out RaycastHit hit, ~0) || !book.Owns(hit.collider)) { Say("Apunta al grimorio para cargar la estampa."); return; }
            string g = book.CurrentGlyph;
            if (g == null) { Say("Abri un glifo en el grimorio y apreta el boton Cargar en la estampa."); return; }
            Load(g);
        }

        /// <summary>Carga el glifo (5 usos). Lo llama el boton del grimorio o el clic derecho apuntando al libro.</summary>
        public void Load(string g)
        {
            glyph = g;
            charges = chargesPerLoad;
            RefreshLabel();
            Loaded?.Invoke(this, g);
            Say($"Estampa {sizeName} cargada: {Discovery.Instance?.Lore.Name(g) ?? g} ({charges} usos).");
        }

        private bool Aim(out RaycastHit hit) => Aim(out hit, canvasLayer);

        /// <summary>Lienzo bajo la mira; si no hay, una superficie del entorno (piso, caja, pared) donde crear una calcomania.</summary>
        private bool AimAtCanvas(out RaycastHit hit, out DrawingCanvas canvas)
        {
            canvas = null;
            if (Aim(out hit) && hit.collider.TryGetComponent(out canvas))
            {
                // Sobre una calcomania enorme del piso la estampa no se leeria: se pone otra a su medida encima.
                var big = canvas.GetComponent<InscribedDecal>();
                if (big == null || big.Owner == null || canvas.WorldSize.x <= diameter * 5f) return true;
                var small = big.Owner.CreateDecal(hit, cam != null ? cam.transform.forward : -hit.normal, Mathf.Max(0.6f, diameter * 3f));
                if (small == null) return true;
                canvas = small;
                return true;
            }
            // Un boton (del grimorio) delante no deja estampar en lo que tiene detras.
            if (Aim(out RaycastHit front, ~0) && front.collider.GetComponentInParent<IPressable>() != null) return false;
            if (!Aim(out hit, ~0, QueryTriggerInteraction.Ignore)) return false;
            var surface = hit.collider.GetComponentInParent<Inscribable>();
            if (surface == null) return false;
            Vector3 view = cam != null ? cam.transform.forward : -hit.normal;
            canvas = surface.CreateDecal(hit, view, Mathf.Max(0.6f, diameter * 3f));
            return canvas != null;
        }

        private bool Aim(out RaycastHit hit, LayerMask mask, QueryTriggerInteraction triggers = QueryTriggerInteraction.Collide)
        {
            Ray ray;
            float reach;
            if (PlatformDetector.IsVR)
            {
                ray = new Ray(raycastOrigin.position, raycastOrigin.forward);
                reach = vrReach;
            }
            else
            {
                if (cam == null) cam = Camera.main;
                if (cam == null) { hit = default; return false; }
                ray = FocusMode.GetRay(cam);
                reach = pcReach;
            }
            return Physics.Raycast(ray, out hit, reach, mask, triggers);
        }

        private void RefreshLabel()
        {
            if (label == null) return;
            string name = string.IsNullOrEmpty(glyph) ? "vacia" : (Discovery.Instance != null ? Discovery.Instance.Lore.Name(glyph) : glyph);
            label.text = string.IsNullOrEmpty(glyph) ? $"{sizeName}\nvacia" : $"{sizeName}\n{name}\n{charges}";
        }

        private static void Say(string text) => DiagnosticBus.OnDiagnosticsUpdated?.Invoke($"<b>[Estampa]</b> {text}");
    }
}
