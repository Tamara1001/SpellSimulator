using UnityEngine;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Player
{
    /// <summary>
    /// Parte de <see cref="PlayerInteractor"/>: MANIPULAR perillas, deslizadores y selectores (<see cref="IManipulable"/>).
    /// Clic o gatillo (y grip en VR) sobre lo apuntado lo captura hasta soltar; mientras tanto la vista de PC queda congelada
    /// (PlayerLook) y el mouse o la mano mueven el control. Un giro de rueda sobre el control, sin captura, sube o baja un paso.
    /// </summary>
    public partial class PlayerInteractor
    {
        private IManipulable activeManipulable;
        private bool manipulatedByGrip;
        private bool wasManipClick, wasManipGrip;
        private RaycastHit hoverHit;

        /// <summary>True mientras un control esta capturado: la vista de PC no gira.</summary>
        public bool IsManipulating => activeManipulable != null;

        /// <summary>True si la rueda es del control apuntado o capturado (no cambia grosor ni distancia de lo arrastrado).</summary>
        public bool WheelOwnedByManipulable => activeManipulable != null || hoveredManipulable != null;

        private void HandleManipulation()
        {
            bool click = PointerHeld;
            // VR: el grip de la mano del rayo se mantiene (antes solo valia el frame en que se apretaba y la perilla se soltaba al instante).
            bool grip = PlatformDetector.IsVR && inputReader.VrGripHeld(rayRight);
            bool clickPressed = click && !wasManipClick;
            bool gripPressed = grip && !wasManipGrip;
            wasManipClick = click;
            wasManipGrip = grip;

            if (activeManipulable != null)
            {
                bool destroyed = activeManipulable is Object o && o == null;
                bool held = manipulatedByGrip ? grip : click;
                if (destroyed || !held || FocusMode.Active) { StopManipulation(); return; }
                activeManipulable.UpdateManipulation(BuildInput(inputReader.Scroll));
                // El clic o gatillo sigue siendo de la perilla: la herramienta en mano no se usa.
                if (!manipulatedByGrip) clickConsumedByPressable = true;
                return;
            }

            if (hoveredManipulable == null || FocusMode.Active || IsDragging) return;

            if (clickPressed || gripPressed)
            {
                activeManipulable = hoveredManipulable;
                manipulatedByGrip = !clickPressed;
                activeManipulable.BeginManipulation(hoverHit);
                if (clickPressed) clickConsumedByPressable = true;
                return;
            }

            // Rueda sin captura: un paso, en el mismo frame.
            float scroll = inputReader.Scroll;
            if (scroll == 0f) return;
            var book = Learning.Grimoire.Instance;
            if (book != null && book.IsOpen) return;
            IManipulable target = hoveredManipulable;
            target.BeginManipulation(hoverHit);
            target.UpdateManipulation(BuildInput(scroll));
            target.EndManipulation();
        }

        /// <summary>Suelta el control capturado (tambien si la vista completa o el lector lo interrumpen).</summary>
        private void StopManipulation()
        {
            if (activeManipulable == null) return;
            IManipulable target = activeManipulable;
            activeManipulable = null;
            if (!(target is Object o) || o != null) target.EndManipulation();
        }

        private ManipulationInput BuildInput(float wheel)
        {
            Transform vrHand = VrRayController;
            bool vr = vrHand != null;
            if (vr)
            {
                Transform hand = vrHand;
                Vector3 dir = Quaternion.AngleAxis(vrRayPitchOffset, hand.right) * hand.forward;
                // Giro de la muñeca: angulo de "la derecha" del controlador alrededor del rayo, medido contra la horizontal.
                float roll = 0f;
                Vector3 reference = Vector3.Cross(Vector3.up, dir);
                if (reference.sqrMagnitude > 1e-4f) roll = Vector3.SignedAngle(reference.normalized, hand.right, dir);
                // Stick der. (analogico) = giro de perillas sin torcer la muñeca: ~1 s a fondo recorre casi todo el rango.
                Vector2 stick = inputReader.VrStickRight;
                var stickDelta = new Vector2(Mathf.Abs(stick.x) > 0.25f ? stick.x * 300f * Time.deltaTime : 0f, 0f);
                return new ManipulationInput(stickDelta, hand.position, roll, hand.position, dir, hand.right, wheel, true);
            }
            Transform cam = cameraTransform;
            return new ManipulationInput(inputReader.LookValue, cam.position, 0f, cam.position, cam.forward, cam.right, wheel, false);
        }
    }
}
