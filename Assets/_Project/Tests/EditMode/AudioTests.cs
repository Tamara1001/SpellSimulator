using NUnit.Framework;
using UnityEditor;
using SpellSimulator.Audio;
using SpellSimulator.Compiler;

namespace SpellSimulator.Tests
{
    /// <summary>Audio: cada evento pide un sonido que existe en la biblioteca y tiene clips.</summary>
    public class AudioTests
    {
        private const string LibraryPath = "Assets/_Project/Data/AudioLibrary.asset";

        [Test]
        public void EveryCueUsedByCode_ExistsWithClips()
        {
            var lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            Assert.IsNotNull(lib, "Falta la biblioteca: SpellSimulator > Audio > Setup Current Scene.");
            string[] used =
            {
                "seal_prepared", "cast_fizzle", "seal_break", "pen_loop", "eraser_loop", "rag_wipe", "ink_refill", "pickup",
                "ignite", "fire_loop", "hiss", "splash", "crack_wood", "crack_stone", "thud", "rock_rise", "reset_chime",
                "impact_wood", "impact_stone", "impact_soft", "step_grass", "step_wood", "step_stone",
                "music", "wind", "waterfall", "bird"
            };
            foreach (string id in used)
            {
                var cue = lib.Get(id);
                Assert.IsNotNull(cue, $"Falta el cue '{id}'.");
                Assert.IsNotEmpty(cue.clips, $"El cue '{id}' no tiene clips.");
            }
            foreach (ElementType e in new[] { ElementType.Fire, ElementType.Water, ElementType.Wind, ElementType.Earth, ElementType.Light })
                Assert.IsNotNull(lib.Get(GameAudio.CastCue(e)), $"Falta el sonido de lanzar {e}.");
        }

        [Test]
        public void Footsteps_DependOnTheSurface()
        {
            Assert.AreEqual("step_grass", FootstepAudio.SurfaceCue("Terrain"));
            Assert.AreEqual("step_wood", FootstepAudio.SurfaceCue("TowerFloor_1"));
            Assert.AreEqual("step_stone", FootstepAudio.SurfaceCue("TowerFloor_0"));
            Assert.AreEqual("step_stone", FootstepAudio.SurfaceCue("TowerRamp_2"));
        }
    }
}
