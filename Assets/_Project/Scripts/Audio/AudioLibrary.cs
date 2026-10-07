using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.Audio
{
    public enum SoundCategory { Sfx, Ambience, Music }

    /// <summary>Un sonido con nombre: uno o varios clips (se elige uno al azar), volumen, tono y espacializacion.</summary>
    [Serializable]
    public class SoundCue
    {
        public string id;
        public AudioClip[] clips = new AudioClip[0];
        public SoundCategory category = SoundCategory.Sfx;
        [Range(0f, 1f)] public float volume = 0.8f;
        public Vector2 pitch = new Vector2(0.95f, 1.05f);
        [Tooltip("0 = 2D (musica, interfaz), 1 = 3D (en el mundo).")]
        [Range(0f, 1f)] public float spatialBlend = 1f;
        public float minDistance = 1.5f;
        public float maxDistance = 30f;

        public AudioClip Pick() => clips == null || clips.Length == 0 ? null : clips[UnityEngine.Random.Range(0, clips.Length)];
    }

    /// <summary>
    /// Biblioteca de sonidos del juego (datos): el codigo pide sonidos por id ("cast_fire", "splash"...) y aca se decide
    /// que clips suenan. Cambiar un sintetizado por uno de un paquete libre es cambiar el clip, no el codigo.
    /// </summary>
    [CreateAssetMenu(menuName = "SpellSimulator/Audio Library", fileName = "AudioLibrary")]
    public class AudioLibrary : ScriptableObject
    {
        public List<SoundCue> cues = new List<SoundCue>();

        private Dictionary<string, SoundCue> index;

        public SoundCue Get(string id)
        {
            if (index == null || index.Count != cues.Count)
            {
                index = new Dictionary<string, SoundCue>();
                foreach (var cue in cues) if (cue != null && !string.IsNullOrEmpty(cue.id)) index[cue.id] = cue;
            }
            return index.TryGetValue(id, out var found) ? found : null;
        }
    }
}
