using System;
using System.IO;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Compiler;

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
            if (Instance == this) Instance = null;
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
                if (File.Exists(SavePath)) return DiscoveryState.FromJson(File.ReadAllText(SavePath));
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
