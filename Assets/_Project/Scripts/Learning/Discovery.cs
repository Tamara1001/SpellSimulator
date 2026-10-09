using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Compiler;
using SpellSimulator.Player;

namespace SpellSimulator.Learning
{
    /// <summary>
    /// Progreso de aprendizaje (SYSTEMS_DESIGN 7). Carga y guarda el DiscoveryState en persistentDataPath, descubre
    /// glifos al leer libros y por experimentacion: si un sello lanzado reconoce con confianza un glifo nuevo, se agrega.
    /// </summary>
    public class Discovery : MonoBehaviour
    {
        public static Discovery Instance { get; private set; }

        /// <summary>(glifo, fuente) al descubrir uno nuevo. Fuente: titulo del libro o "experimentando".</summary>
        public static event Action<string, string> Discovered;

        /// <summary>Un hechizo se descubrio lanzandolo (experimentacion).</summary>
        public static event Action<SpellDef> SpellDiscovered;

        [SerializeField] private TextAsset loreJson;
        [SerializeField] private SpellCaster caster;
        [SerializeField, Tooltip("Confianza minima para descubrir un glifo dibujandolo.")]
        private float experimentConfidence = 0.8f;

        public GlyphLore Lore { get; private set; }
        public DiscoveryState State { get; private set; }

        public static string SavePath => Path.Combine(Application.persistentDataPath, "descubrimientos.json");

        private void Awake()
        {
            Instance = this;
            SpellCatalog.DiscoveredCheck = IsSpellDiscovered;
            Lore = loreJson != null ? GlyphLore.FromJson(loreJson.text) : new GlyphLore();
            State = Load();
        }

        private void OnEnable()
        {
            if (caster != null) caster.SealAnalyzed += OnSealAnalyzed;
        }

        private void OnDisable()
        {
            if (caster != null) caster.SealAnalyzed -= OnSealAnalyzed;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                SpellCatalog.DiscoveredCheck = null;
            }
        }

        /// <summary>Lee un libro: descubre sus glifos. Devuelve cuantos eran nuevos.</summary>
        public int Read(BookInfo book)
        {
            if (book == null) return 0;
            State.MarkRead(book.Id);
            int count = 0;
            foreach (var g in book.Glyphs)
                if (Discover(g, book.Title)) count++;
            Save();
            return count;
        }

        public bool Discover(string glyph, string source)
        {
            if (!State.Discover(glyph)) return false;
            Debug.Log($"[Aprendizaje] Descubierto '{glyph}' ({source}).");
            Discovered?.Invoke(glyph, source);
            Save();
            return true;
        }

        private bool IsSpellDiscovered(string spellId) => State != null && State.KnowsSpell(spellId);

        /// <summary>
        /// Logica pura del descubrimiento por experimentacion: si lo lanzado coincide con un hechizo del catalogo que el
        /// jugador aun no conoce (ni inicial, ni de un libro leido, ni descubierto), lo marca en state y lo devuelve; si no, null.
        /// twistsDeg (opcional) desempata hechizos con la misma composicion.
        /// </summary>
        public static SpellDef OnCast(DiscoveryState state, SpellCatalog catalog, string sigilId, IList<string> signIds, IList<float> twistsDeg = null)
        {
            if (state == null || catalog == null) return null;
            SpellDef def = catalog.MatchCast(sigilId, signIds, twistsDeg);
            if (def == null || SpellCatalog.IsUnlocked(def, state.HasRead, state.KnowsSpell)) return null;
            state.DiscoverSpell(def.Id);
            return def;
        }

        /// <summary>Registra un cast real: si descubre un hechizo, avisa en la HUD, guarda y emite <see cref="SpellDiscovered"/>.</summary>
        public SpellDef OnCast(string sigilId, IList<string> signIds, IList<float> twistsDeg = null)
        {
            SpellDef def = OnCast(State, SpellCatalog.Shared, sigilId, signIds, twistsDeg);
            if (def == null) return null;
            Debug.Log($"[Aprendizaje] Hechizo descubierto '{def.Id}' (experimentando).");
            Hud.Notify("discovery", $"Descubriste: {def.Name}", 4f);
            Save();
            SpellDiscovered?.Invoke(def);
            return def;
        }

        /// <summary>Vuelve al estado inicial (opcion del grimorio).</summary>
        public void ResetProgress()
        {
            State = DiscoveryState.Starting(Lore.Starting);
            Save();
            Discovered?.Invoke(null, "reinicio");
        }

        private void OnSealAnalyzed(GlyphAST ast, SpellIR spell, DrawingCanvas canvas)
        {
            if (ast == null) return;
            Consider(ast.PrimarySigil);
            foreach (var sign in ast.Signs) Consider(sign);
        }

        private void Consider(GlyphRecognition r)
        {
            if (r == null || r.Confidence < experimentConfidence || !Lore.Has(r.Id)) return;
            Discover(r.Id, "experimentando");
        }

        private DiscoveryState Load()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    // Lo inicial siempre esta al alcance, aunque el progreso guardado sea de una version anterior.
                    var saved = DiscoveryState.FromJson(File.ReadAllText(SavePath));
                    foreach (var g in Lore.Starting) saved.Discover(g);
                    return saved;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Aprendizaje] No se pudo leer el progreso: {e.Message}");
            }
            return DiscoveryState.Starting(Lore.Starting);
        }

        private void Save()
        {
            try { File.WriteAllText(SavePath, State.ToJson()); }
            catch (Exception e) { Debug.LogWarning($"[Aprendizaje] No se pudo guardar el progreso: {e.Message}"); }
        }
    }
}
