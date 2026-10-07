using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Player
{
    /// <summary>
    /// Pulsos hapticos para controladores XR y gamepads. Sin dispositivo compatible no hace nada.
    /// </summary>
    public static class Haptics
    {
        public const float StrokeAmplitude = 0.08f;
        public const float CastAmplitude = 0.8f;

        public static void Pulse(HandType hand, float amplitude, float seconds)
        {
            amplitude = Mathf.Clamp01(amplitude);
            if (PlatformDetector.IsVR)
            {
                var usage = hand == HandType.Left ? UnityEngine.InputSystem.CommonUsages.LeftHand : UnityEngine.InputSystem.CommonUsages.RightHand;
                if (InputSystem.GetDevice<XRControllerWithRumble>(usage) is XRControllerWithRumble controller)
                    controller.SendImpulse(amplitude, seconds);
                return;
            }

            // Gamepad: un pulso corto (no hay API de duracion; se apaga en el proximo pulso o al soltar).
            if (Gamepad.current != null && amplitude > 0.3f) Gamepad.current.SetMotorSpeeds(amplitude * 0.5f, amplitude);
        }

        /// <summary>Apaga motores de gamepad (llamar al terminar un pulso fuerte).</summary>
        public static void Stop()
        {
            Gamepad.current?.ResetHaptics();
        }
    }
}
