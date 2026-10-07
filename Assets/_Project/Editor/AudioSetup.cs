using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SpellSimulator.Audio;
using SpellSimulator.Casting;
using SpellSimulator.Reactions;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Arma la biblioteca de audio (sintetizados de Tools/Audio/synth_audio.py + Kenney CC0) y equipa una escena:
    /// AudioDirector + GameAudio en SystemManagers, pasos en el jugador y golpes en los cuerpos fisicos.
    /// Menu: SpellSimulator > Audio > Setup Current Scene.
    /// </summary>
    public static class AudioSetup
    {
        public const string LibraryPath = "Assets/_Project/Data/AudioLibrary.asset";
        private const string Synth = "Assets/_Project/Audio/Synth";
        private const string Kenney = "Assets/_Project/Audio/Kenney";

        private static AudioClip[] Clips(string folder, string prefix)
        {
            return Directory.GetFiles(folder)
                .Where(f => (f.EndsWith(".wav") || f.EndsWith(".ogg")) && Path.GetFileNameWithoutExtension(f).StartsWith(prefix))
                .OrderBy(f => f)
                .Select(f => AssetDatabase.LoadAssetAtPath<AudioClip>(f.Replace('\\', '/')))
                .Where(c => c != null)
                .ToArray();
        }

        private static SoundCue Cue(string id, AudioClip[] clips, float volume = 0.8f, SoundCategory category = SoundCategory.Sfx,
            float spatial = 1f, float maxDistance = 30f, float pitchSpread = 0.05f)
        {
            return new SoundCue
            {
                id = id, clips = clips, volume = volume, category = category, spatialBlend = spatial,
                maxDistance = maxDistance, minDistance = 1.5f, pitch = new Vector2(1f - pitchSpread, 1f + pitchSpread)
            };
        }

        private static AudioClip[] Join(params AudioClip[][] sets) => sets.SelectMany(s => s).ToArray();

        public static AudioLibrary BuildLibrary()
        {
            var lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<AudioLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }
            string rpg = $"{Kenney}/RPG", impact = $"{Kenney}/Impact", ui = $"{Kenney}/Interface";
            lib.cues = new List<SoundCue>
            {
                // Sello y hechizos (sintetizados).
                Cue("seal_prepared", Clips(Synth, "seal_prepared"), 0.6f),
                Cue("cast_fire", Clips(Synth, "cast_fire"), 0.9f, maxDistance: 40f),
                Cue("cast_water", Clips(Synth, "cast_water"), 0.9f, maxDistance: 40f),
                Cue("cast_wind", Clips(Synth, "cast_wind"), 0.9f, maxDistance: 40f),
                Cue("cast_earth", Clips(Synth, "cast_earth"), 1f, maxDistance: 45f),
                Cue("cast_light", Clips(Synth, "cast_light"), 0.8f, maxDistance: 40f),
                Cue("cast_fizzle", Clips(Synth, "cast_fizzle"), 0.7f),
                Cue("seal_break", Join(Clips(Synth, "seal_break"), Clips(ui, "scratch_")), 0.7f),
                // Herramientas.
                Cue("pen_loop", Clips(Synth, "pen_loop"), 0.35f, maxDistance: 8f),
                Cue("eraser_loop", Clips(Synth, "eraser_loop"), 0.35f, maxDistance: 8f),
                Cue("rag_wipe", Join(Clips(Synth, "rag_wipe"), Clips(rpg, "cloth")), 0.5f, maxDistance: 10f, pitchSpread: 0.1f),
                Cue("ink_refill", Clips(Synth, "ink_refill"), 0.6f, maxDistance: 10f),
                Cue("pickup", Join(Clips(rpg, "handleSmallLeather"), Clips(rpg, "clothBelt")), 0.5f, maxDistance: 10f, pitchSpread: 0.1f),
                // Mundo.
                Cue("ignite", Clips(Synth, "ignite"), 0.8f),
                Cue("fire_loop", Clips(Synth, "fire_loop"), 0.45f, SoundCategory.Ambience, maxDistance: 18f),
                Cue("hiss", Clips(Synth, "hiss"), 0.7f),
                Cue("splash", Clips(Synth, "splash"), 0.8f, pitchSpread: 0.12f),
                Cue("crack_wood", Join(Clips(Synth, "crack_wood"), Clips(impact, "impactWood_heavy")), 0.9f),
                Cue("crack_stone", Join(Clips(Synth, "crack_stone"), Clips(impact, "impactMining")), 0.9f),
                Cue("thud", Join(Clips(Synth, "thud"), Clips(impact, "impactSoft_heavy")), 0.8f),
                Cue("rock_rise", Clips(Synth, "rock_rise"), 0.9f, maxDistance: 40f),
                Cue("reset_chime", Clips(Synth, "reset_chime"), 0.6f, spatial: 0f),
                Cue("impact_wood", Join(Clips(impact, "impactWood_light"), Clips(impact, "impactWood_medium"), Clips(impact, "impactPlank_medium")), 0.7f, pitchSpread: 0.1f),
                Cue("impact_stone", Clips(impact, "impactMining"), 0.7f, pitchSpread: 0.1f),
                Cue("impact_soft", Clips(impact, "impactSoft_medium"), 0.6f, pitchSpread: 0.1f),
                // Pasos.
                Cue("step_grass", Clips(impact, "footstep_grass"), 0.35f, maxDistance: 12f, pitchSpread: 0.08f),
                Cue("step_wood", Clips(impact, "footstep_wood"), 0.4f, maxDistance: 12f, pitchSpread: 0.08f),
                Cue("step_stone", Clips(impact, "footstep_concrete"), 0.35f, maxDistance: 12f, pitchSpread: 0.08f),
                // Libro (M6) e interfaz.
                Cue("book_open", Clips(rpg, "bookOpen"), 0.6f),
                Cue("stamp", Join(Clips(rpg, "bookPlace"), Clips(impact, "impactSoft_medium")), 0.7f, pitchSpread: 0.08f),
                Cue("book_close", Clips(rpg, "bookClose"), 0.6f),
                Cue("book_flip", Clips(rpg, "bookFlip"), 0.6f, pitchSpread: 0.1f),
                Cue("ui_select", Clips(ui, "select_"), 0.5f, spatial: 0f),
                Cue("ui_confirm", Clips(ui, "confirmation_"), 0.5f, spatial: 0f),
                // Fondo.
                Cue("music", Clips(Synth, "music_pad"), 0.5f, SoundCategory.Music, spatial: 0f, pitchSpread: 0f),
                Cue("wind", Clips(Synth, "wind_loop"), 0.35f, SoundCategory.Ambience, spatial: 0f, pitchSpread: 0f),
                Cue("waterfall", Clips(Synth, "waterfall_loop"), 0.9f, SoundCategory.Ambience, maxDistance: 45f, pitchSpread: 0f),
                Cue("bird", Clips(Synth, "bird_"), 0.35f, SoundCategory.Ambience, maxDistance: 40f, pitchSpread: 0.1f),
            };
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            return lib;
        }

        [MenuItem("SpellSimulator/Audio/Setup Current Scene")]
        public static string SetupCurrentScene()
        {
            var lib = BuildLibrary();
            var scene = EditorSceneManager.GetActiveScene();
            var sys = GameObject.Find("SystemManagers");
            if (sys == null) return "Sin SystemManagers";

            var director = sys.GetComponent<AudioDirector>() ?? sys.AddComponent<AudioDirector>();
            var dso = new SerializedObject(director);
            dso.FindProperty("library").objectReferenceValue = lib;
            dso.ApplyModifiedProperties();

            var game = sys.GetComponent<GameAudio>() ?? sys.AddComponent<GameAudio>();
            var gso = new SerializedObject(game);
            gso.FindProperty("caster").objectReferenceValue = sys.GetComponent<SpellCaster>();
            gso.ApplyModifiedProperties();

            var player = GameObject.Find("Player");
            if (player != null && player.GetComponent<FootstepAudio>() == null) player.AddComponent<FootstepAudio>();

            int bodies = 0;
            foreach (var rb in Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
            {
                if (rb.gameObject == player) continue;
                var reactive = rb.GetComponent<ElementReactive>();
                if (reactive == null || reactive.Profile == null) continue;
                var audio = rb.GetComponent<CollisionAudio>() ?? rb.gameObject.AddComponent<CollisionAudio>();
                string cue = reactive.Profile.displayName == "Piedra" ? "impact_stone" : reactive.Profile.displayName == "Tela" ? "impact_soft" : "impact_wood";
                audio.SetCue(cue);
                EditorUtility.SetDirty(audio);
                bodies++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return $"Audio en {scene.name}: {lib.cues.Count} cues, {bodies} cuerpos con golpes.";
        }
    }
}
