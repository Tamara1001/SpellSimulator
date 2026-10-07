using UnityEngine;

namespace SpellSimulator.World
{
    /// <summary>Hace que una etiqueta 3D (TextMeshPro) mire siempre a la camara principal.</summary>
    public class FaceCamera : MonoBehaviour
    {
        private Camera cam;

        private void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            Vector3 away = transform.position - cam.transform.position;
            if (away.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
        }
    }
}
