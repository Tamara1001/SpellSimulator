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
    public class Stamp : MonoBehaviour, IInteractable, IUsable
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

        private Rigidbody rb;
        private Collider col;
        private Camera cam;
        private string glyph;
        private int charges;
        private string prompt;

        public string Glyph => glyph;
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
            rb.isKinematic = true;
            col.enabled = false;
            transform.SetParent(holdPoint);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        public void OnDrop()
        {
            transform.SetParent(null);
            rb.isKinematic = false;
            col.enabled = true;
        }

        // ---------------------------------------------------------------- usar

        public void OnUseStart()
        {
            if (!Aim(out RaycastHit hit)) return;
            if (!hit.collider.TryGetComponent(out DrawingCanvas canvas)) return;
            if (string.IsNullOrEmpty(glyph) || charges <= 0)
            {
                Say("La estampa esta vacia. Abri el grimorio en un glifo y cargala (clic derecho / A apuntando al libro).");
                return;
            }
            var entry = dictionary != null ? dictionary.Dictionary.Get(glyph) : null;
            if (entry == null || entry.TemplateStrokes.Count == 0) return;

            Vector2 uv = canvas.GetUVFromWorldPosition(hit.point);
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
            if (book == null || !book.IsOpen) { Say("Abri el grimorio (Tab) en la pagina de un glifo para cargar la estampa."); return; }
            if (!Aim(out RaycastHit hit, ~0) || !book.Owns(hit.collider)) { Say("Apunta al grimorio para cargar la estampa."); return; }
            string g = book.CurrentGlyph;
            if (g == null) { Say("Esa pagina no tiene un glifo."); return; }
            glyph = g;
            charges = chargesPerLoad;
            RefreshLabel();
            Loaded?.Invoke(this, g);
            Say($"Estampa {sizeName} cargada: {Discovery.Instance?.Lore.Name(g) ?? g} ({charges} usos).");
        }

        private bool Aim(out RaycastHit hit) => Aim(out hit, canvasLayer);

        private bool Aim(out RaycastHit hit, LayerMask mask)
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
                ray = new Ray(cam.transform.position, cam.transform.forward);
                reach = pcReach;
            }
            return Physics.Raycast(ray, out hit, reach, mask, QueryTriggerInteraction.Collide);
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
