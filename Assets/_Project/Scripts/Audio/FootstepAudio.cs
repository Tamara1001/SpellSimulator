using UnityEngine;

namespace SpellSimulator.Audio
{
    /// <summary>
    /// Pasos del jugador segun la superficie que pisa: pasto (terreno), madera (pisos de la torre) o piedra (rampas y
    /// planta baja). Un paso cada strideLength metros recorridos en el suelo. Sin GC.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class FootstepAudio : MonoBehaviour
    {
        [SerializeField] private float strideLength = 1.6f;
        [SerializeField] private float minSpeed = 0.8f;
        [SerializeField] private float groundProbe = 1.4f;

        private Rigidbody rb;
        private float travelled;

        private void Awake() => rb = GetComponent<Rigidbody>();

        private void FixedUpdate()
        {
            var director = AudioDirector.Instance;
            if (director == null) return;
            Vector3 v = rb.linearVelocity;
            v.y = 0f;
            float speed = v.magnitude;
            if (speed < minSpeed) { travelled = strideLength * 0.6f; return; }
            if (!Physics.Raycast(rb.position, Vector3.down, out RaycastHit hit, groundProbe, ~0, QueryTriggerInteraction.Ignore)) return;
            travelled += speed * Time.fixedDeltaTime;
            if (travelled < strideLength) return;
            travelled = 0f;
            director.Play(SurfaceCue(hit.collider.name), hit.point, Mathf.Clamp01(speed / 6f) * 0.6f + 0.4f);
        }

        public static string SurfaceCue(string colliderName)
        {
            if (colliderName.StartsWith("TowerFloor_0") || colliderName.StartsWith("TowerRamp") || colliderName.StartsWith("Rock")) return "step_stone";
            if (colliderName.StartsWith("TowerFloor") || colliderName.StartsWith("Table")) return "step_wood";
            return "step_grass";
        }
    }
}
