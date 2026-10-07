using UnityEngine;
using UnityEngine.InputSystem;

namespace SpellSimulator.Player
{
    /// <summary>
    /// Modo dibujo de escritorio (PC): un lienzo queda fijo delante de la camara, la vista se congela y el mouse
    /// mueve un puntero sobre la pantalla. Las herramientas apuntan con <see cref="GetRay"/> en vez de la mira central.
    /// En VR no existe: el lienzo se sostiene en la mano.
    /// </summary>
    public static class FocusMode
    {
        public static bool Active { get; private set; }

        public static void Enter()
        {
            if (PlatformDetector.IsVR) return;
            Active = true;
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = true;
        }

        public static void Exit()
        {
            if (!Active) return;
            Active = false;
            if (PlatformDetector.IsVR) return;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>Rayo de puntería de PC: la mira central, o el puntero del mouse en modo dibujo de escritorio.</summary>
        public static Ray GetRay(Camera camera)
        {
            if (Active && Mouse.current != null) return camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            return new Ray(camera.transform.position, camera.transform.forward);
        }
    }
}
