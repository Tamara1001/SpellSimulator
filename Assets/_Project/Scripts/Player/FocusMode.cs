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

        /// <summary>True si la vista completa (V) de un lienzo/superficie esta activa; Esc la cierra antes que el grimorio.</summary>
        public static bool CanvasView { get; private set; }

        public static void EnterCanvasView()
        {
            if (PlatformDetector.IsVR) return;
            CanvasView = true;
            Enter();
        }

        /// <summary>Frame en que se cerro la vista completa (el Esc de ese frame no debe abrir el grimorio).</summary>
        public static int CanvasViewExitFrame { get; private set; } = -1;

        public static void ExitCanvasView()
        {
            if (CanvasView) CanvasViewExitFrame = Time.frameCount;
            CanvasView = false;
            Exit();
        }

        public static void Exit()
        {
            CanvasView = false;
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
