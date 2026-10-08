using System.Collections.Generic;
using UnityEngine;

namespace SpellSimulator.Audio
{
    /// <summary>
    /// Reproduce los sonidos de AudioLibrary: one-shots desde un pool fijo de AudioSources, loops ligados a un objeto
    /// (fuego en una caja, rasgueo de la pluma), musica y viento de fondo, y pajaros al azar cerca del oyente.
    /// Volumen por categoria (musica, ambiente, efectos). Sin GC en Update.
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        [SerializeField] private AudioLibrary library;
        [SerializeField, Range(0f, 1f)] private float master = 1f;
        [SerializeField, Range(0f, 1f)] private float music = 0.5f;
        [SerializeField, Range(0f, 1f)] private float ambience = 0.7f;
        [SerializeField, Range(0f, 1f)] private float sfx = 0.9f;
        [SerializeField] private int poolSize = 24;

        [Header("Fondo")]
        [SerializeField] private string musicCue = "music";
        [SerializeField] private string windCue = "wind";
        [SerializeField] private string birdCue = "bird";
        [SerializeField, Tooltip("Segundos entre pajaros (min, max).")]
        private Vector2 birdInterval = new Vector2(5f, 14f);

        private AudioSource[] pool;
        private int next;
        private readonly Dictionary<Object, AudioSource> loops = new Dictionary<Object, AudioSource>();
        private AudioSource musicSource, windSource;
        private float nextBird;
        private Transform listener;

        public AudioLibrary Library => library;

        private void Awake()
        {
            Instance = this;
            pool = new AudioSource[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject($"Sfx_{i}");
                go.transform.SetParent(transform, false);
                pool[i] = go.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
                pool[i].rolloffMode = AudioRolloffMode.Linear;
            }
            nextBird = Time.time + birdInterval.x;
        }

        private void OnEnable() => Learning.GrimoireOptions.VolumesChanged += OnVolumesChanged;

        private void OnDisable() => Learning.GrimoireOptions.VolumesChanged -= OnVolumesChanged;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Volumenes de musica/efectos de las Opciones del grimorio: refresca la musica y el viento de fondo al instante.</summary>
        private void OnVolumesChanged(int category)
        {
            RefreshBackground(musicSource, musicCue);
            RefreshBackground(windSource, windCue);
        }

        private void RefreshBackground(AudioSource source, string id)
        {
            var cue = library != null ? library.Get(id) : null;
            if (source != null && cue != null) source.volume = cue.volume * VolumeOf(cue.category);
        }

        private void Start()
        {
            musicSource = Background(musicCue);
            windSource = Background(windCue);
        }

        private void Update()
        {
            if (listener == null)
            {
                var l = FindAnyObjectByType<AudioListener>();
                if (l != null) listener = l.transform;
            }
            if (listener != null && Time.time >= nextBird)
            {
                nextBird = Time.time + Random.Range(birdInterval.x, birdInterval.y);
                Vector2 r = Random.insideUnitCircle.normalized * Random.Range(8f, 25f);
                Play(birdCue, listener.position + new Vector3(r.x, Random.Range(3f, 8f), r.y));
            }
        }

        public float VolumeOf(SoundCategory category)
        {
            switch (category)
            {
                // El volumen general lo aplica AudioListener.volume (GrimoireOptions.MasterVolume); aca solo musica y efectos.
                case SoundCategory.Music: return master * music * Learning.GrimoireOptions.MusicVolume;
                case SoundCategory.Ambience: return master * ambience * Learning.GrimoireOptions.EffectsVolume;
                default: return master * sfx * Learning.GrimoireOptions.EffectsVolume;
            }
        }

        /// <summary>Sonido suelto en un punto del mundo (o 2D si el cue lo es).</summary>
        public void Play(string id, Vector3 position, float volumeScale = 1f)
        {
            var cue = library != null ? library.Get(id) : null;
            var clip = cue?.Pick();
            if (clip == null) return;
            var source = pool[next];
            next = (next + 1) % pool.Length;
            Configure(source, cue, volumeScale);
            source.transform.position = position;
            source.clip = clip;
            source.loop = false;
            source.Play();
        }

        /// <summary>Loop pegado a un objeto (uno por clave). Si ya suena, no hace nada.</summary>
        public void StartLoop(Object key, string id, Transform follow, float volumeScale = 1f)
        {
            if (key == null || loops.TryGetValue(key, out var existing) && existing != null) return;
            var cue = library != null ? library.Get(id) : null;
            var clip = cue?.Pick();
            if (clip == null || follow == null) return;
            var go = new GameObject($"Loop_{id}");
            go.transform.SetParent(follow, false);
            var source = go.AddComponent<AudioSource>();
            Configure(source, cue, volumeScale);
            source.clip = clip;
            source.loop = true;
            source.time = Random.Range(0f, clip.length);
            source.Play();
            loops[key] = source;
        }

        public void StopLoop(Object key)
        {
            if (key == null || !loops.TryGetValue(key, out var source)) return;
            loops.Remove(key);
            if (source != null) Destroy(source.gameObject);
        }

        private AudioSource Background(string id)
        {
            var cue = library != null ? library.Get(id) : null;
            var clip = cue?.Pick();
            if (clip == null) return null;
            var go = new GameObject($"Background_{id}");
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            Configure(source, cue, 1f);
            source.clip = clip;
            source.loop = true;
            source.Play();
            return source;
        }

        private void Configure(AudioSource source, SoundCue cue, float volumeScale)
        {
            source.volume = cue.volume * volumeScale * VolumeOf(cue.category);
            source.pitch = Random.Range(cue.pitch.x, cue.pitch.y);
            source.spatialBlend = cue.spatialBlend;
            source.minDistance = cue.minDistance;
            source.maxDistance = cue.maxDistance;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.dopplerLevel = 0f;
        }
    }
}
