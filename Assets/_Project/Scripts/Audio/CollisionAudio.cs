using UnityEngine;

namespace SpellSimulator.Audio
{
    /// <summary>Golpe al chocar (cajas, piedras, muñecos): el volumen sigue a la velocidad del impacto.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CollisionAudio : MonoBehaviour
    {
        [SerializeField] private string cue = "impact_wood";
        [SerializeField, Tooltip("Velocidad relativa minima para sonar (m/s).")]
        private float minSpeed = 1.2f;
        [SerializeField] private float cooldown = 0.12f;

        private float next;

        public void SetCue(string id) => cue = id;

        private void OnCollisionEnter(Collision collision)
        {
            if (Time.time < next || AudioDirector.Instance == null) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < minSpeed) return;
            next = Time.time + cooldown;
            Vector3 at = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            AudioDirector.Instance.Play(cue, at, Mathf.Clamp01(speed / 8f));
        }
    }
}
