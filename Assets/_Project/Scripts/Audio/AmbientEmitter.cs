using UnityEngine;

namespace SpellSimulator.Audio
{
    /// <summary>Sonido ambiente fijo en un lugar (cascada, arroyo, chimenea): un loop 3D que arranca solo.</summary>
    public class AmbientEmitter : MonoBehaviour
    {
        [SerializeField] private string cue = "waterfall";
        [SerializeField, Range(0f, 2f)] private float volume = 1f;

        private void Start()
        {
            if (AudioDirector.Instance != null) AudioDirector.Instance.StartLoop(this, cue, transform, volume);
        }

        private void OnDestroy()
        {
            if (AudioDirector.Instance != null) AudioDirector.Instance.StopLoop(this);
        }
    }
}
