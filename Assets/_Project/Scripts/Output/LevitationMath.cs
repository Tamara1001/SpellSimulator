using UnityEngine;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Logica pura de los signos float y levitation. Float = zona suelta: lo liviano pierde peso, sube despacio y se mece.
    /// Levitation = agarre firme: lo de arriba queda suspendido a una altura fija sobre el sello. Sin MonoBehaviour: testeable en EditMode.
    /// </summary>
    public static class LevitationMath
    {
        /// <summary>Masa maxima (kg) que float levanta del todo; lo mas pesado solo se alivia.</summary>
        public const float FloatMaxMass = 40f;
        /// <summary>Masa maxima (kg) que levitation sostiene.</summary>
        public const float LevitationMaxMass = 120f;

        public const float FloatStiffness = 2.2f;
        public const float FloatDamping = 1.6f;
        public const float LevitationStiffness = 16f;
        public const float LevitationDamping = 5f;

        /// <summary>Altura de reposo (m sobre el papel): float sube mas alto; levitation queda cerca, como un orbe suspendido.</summary>
        public static float HoverHeight(bool isFloat, float scale, float length)
        {
            float len = Mathf.Clamp(length, 0.2f, 3f);
            return Mathf.Max(0.05f, scale) * (isFloat ? 1.2f + 1.1f * len : 0.8f + 0.5f * len);
        }

        /// <summary>Cuanto del peso se cancela: 1 = ingravido. Los cuerpos pesados reciben menos.</summary>
        public static float GravityCancel(bool isFloat, float mass)
        {
            float max = isFloat ? FloatMaxMass : LevitationMaxMass;
            if (mass <= max) return isFloat ? 0.96f : 1f;
            return Mathf.Clamp01(max / Mathf.Max(mass, 0.01f)) * 0.8f;
        }

        /// <summary>
        /// Aceleracion vertical (m/s2, sobre el eje del sello): resorte amortiguado hacia la altura de reposo sin termino de gravedad (FIS-11).
        /// </summary>
        public static float VerticalAcceleration(bool isFloat, float height, float verticalSpeed, float target, float bob = 0f)
        {
            float k = isFloat ? FloatStiffness : LevitationStiffness;
            float c = isFloat ? FloatDamping : LevitationDamping;
            float sway = isFloat ? bob * 0.6f : 0f;
            return (target - height) * k - verticalSpeed * c + sway;
        }

        /// <summary>
        /// Aceleracion vertical (m/s2, sobre el eje del sello): cancela la gravedad y empuja un resorte amortiguado hacia la altura de reposo.
        /// <paramref name="bob"/> es un vaiven (-1..1) que solo usa float.
        /// Sobrecarga conservada por compatibilidad con tests y MagicField (FIS-11).
        /// </summary>
        public static float VerticalAcceleration(bool isFloat, float height, float verticalSpeed, float target, float gravity, float cancel, float bob)
        {
            return gravity * cancel + VerticalAcceleration(isFloat, height, verticalSpeed, target, bob);
        }

        /// <summary>Atraccion lateral al eje del sello (resorte amortiguado): float la deja libre, levitation la ancla.</summary>
        public static float LateralAcceleration(bool isFloat, float offset, float speed)
        {
            float k = isFloat ? 0.5f : 7f;
            float c = isFloat ? 0.8f : 3.5f;
            return -offset * k - speed * c;
        }
    }
}
