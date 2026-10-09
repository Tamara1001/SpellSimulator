using UnityEngine;
using SpellSimulator.Canvas;

namespace SpellSimulator.Player
{
    /// <summary>
    /// Vista completa (V) de algo que se MIRA sin agarrarlo: un lienzo grande o una parte de una superficie del ambiente
    /// (pared de la torre, arbol...). En vez de mover el objeto, acerca la camara de forma frontal y congela la vista
    /// (<see cref="FocusMode.EnterCanvasView"/>): el mouse es el puntero y las herramientas apuntan con el rayo del cursor.
    /// Al salir (V o Esc) la camara vuelve a su pose. Solo PC. Se crea solo en la camara la primera vez que se usa.
    /// </summary>
    public class SurfaceFocus : MonoBehaviour
    {
        public const float Fill = 0.7f;
        public const float RegionWidth = 1.2f;

        private static SurfaceFocus instance;

        private Transform cam;
        private Vector3 savedLocalPosition;
        private Quaternion savedLocalRotation;
        private DrawingCanvas canvas;
        private Vector3 fixedPosition;
        private Quaternion fixedRotation;
        private float fov, aspect, near;
        private bool active;

        public static bool IsActive => instance != null && instance.active;

        /// <summary>Vista completa de un lienzo: encuadra toda la superficie, de frente por el lado donde esta el jugador.</summary>
        public static bool ShowCanvas(Transform cameraTransform, DrawingCanvas target)
        {
            if (PlatformDetector.IsVR || target == null || !Prepare(cameraTransform)) return false;
            instance.canvas = target;
            instance.Begin();
            return true;
        }

        /// <summary>Vista de una zona fija (RegionWidth m) alrededor de un punto de una superficie del ambiente.</summary>
        public static bool ShowRegion(Transform cameraTransform, Vector3 point, Vector3 normal)
        {
            if (PlatformDetector.IsVR || !Prepare(cameraTransform)) return false;
            SurfaceFocus s = instance;
            s.canvas = null;
            float tan = Mathf.Tan(s.fov * 0.5f * Mathf.Deg2Rad);
            float distance = RegionWidth / Fill / (2f * tan);
            distance = Mathf.Max(distance, Mathf.Max(0.3f, s.near * 2f));
            // Si hay algo en el medio (un techo bajo, otra pared) se acerca para no quedar adentro.
            if (Physics.Raycast(point + normal * 0.02f, normal, out RaycastHit block, distance, ~0, QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(block.distance * 0.85f, Mathf.Max(0.3f, s.near * 2f));
            Vector3 up = Mathf.Abs(normal.y) < 0.7f ? Vector3.up : cameraTransform.up;
            s.fixedPosition = point + normal * distance;
            s.fixedRotation = Quaternion.LookRotation(-normal, up);
            s.Begin();
            return true;
        }

        /// <summary>Cierra la vista y devuelve la camara a su lugar.</summary>
        public static void Close()
        {
            if (instance != null && instance.active) instance.End();
        }

        private static bool Prepare(Transform cameraTransform)
        {
            if (cameraTransform == null) return false;
            if (instance == null || instance.cam != cameraTransform)
            {
                instance = cameraTransform.GetComponent<SurfaceFocus>();
                if (instance == null) instance = cameraTransform.gameObject.AddComponent<SurfaceFocus>();
            }
            if (instance.active) return false;
            instance.cam = cameraTransform;
            Camera camera = cameraTransform.GetComponent<Camera>();
            if (camera == null) camera = Camera.main;
            instance.fov = camera != null ? camera.fieldOfView : 60f;
            instance.aspect = camera != null ? camera.aspect : 1.78f;
            instance.near = camera != null ? camera.nearClipPlane : 0.1f;
            return true;
        }

        private void Begin()
        {
            savedLocalPosition = cam.localPosition;
            savedLocalRotation = cam.localRotation;
            active = true;
            FocusMode.EnterCanvasView();
            FocusMode.FocusedCanvas = canvas;
            Apply();
        }

        private void End()
        {
            active = false;
            canvas = null;
            if (cam != null) cam.SetLocalPositionAndRotation(savedLocalPosition, savedLocalRotation);
            FocusMode.ExitCanvasView();
        }

        private void OnDisable()
        {
            if (active) End();
        }

        // Despues de PlayerLook y el movimiento: la camara queda clavada en el encuadre aunque el jugador se mueva.
        private void LateUpdate()
        {
            if (!active) return;
            if (canvas == null && fixedRotation == default) { End(); return; }
            Apply();
        }

        private void Apply()
        {
            if (canvas != null)
            {
                if (!canvas.isActiveAndEnabled) { End(); return; }
                Vector3 center = canvas.WorldCenter;
                Vector3 normal = canvas.FaceNormal;
                // Lado donde esta el jugador (la camara original), no donde esta ahora.
                Vector3 viewer = cam.parent != null ? cam.parent.TransformPoint(savedLocalPosition) : savedLocalPosition;
                if (Vector3.Dot(normal, viewer - center) < 0f) normal = -normal;

                Vector2 size = canvas.WorldSize;
                float tan = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
                float distance = Mathf.Max(size.y / Fill / (2f * tan), size.x / Fill / (2f * tan * aspect));
                distance = Mathf.Clamp(distance, Mathf.Max(0.1f, near * 2f), 60f);

                Vector3 up = Vector3.ProjectOnPlane(canvas.transform.up, normal);
                if (up.sqrMagnitude < 1e-4f) up = Vector3.up;
                cam.SetPositionAndRotation(center + normal * distance, Quaternion.LookRotation(-normal, up));
            }
            else cam.SetPositionAndRotation(fixedPosition, fixedRotation);
        }
    }
}
