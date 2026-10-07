using UnityEngine;

namespace SpellSimulator.Compiler
{
    /// <summary>
    /// Direccion 3D en espacio-papel a partir de la direccion de superficie de los signos.
    /// Port de compiler/spellDirection.ts: la fuerza inclina el vector desde la normal (z) hacia la superficie.
    /// </summary>
    public static class SpellDirection
    {
        private const float ForceTiltMaxDeg = 76f;
        private const float MinSurfaceMagnitude = 0.001f;

        public static Vector3 FromSurface(Vector2 surface, float force, out float xTiltDeg, out float yTiltDeg)
        {
            float magnitude = surface.magnitude;
            if (magnitude < MinSurfaceMagnitude)
            {
                xTiltDeg = 0f;
                yTiltDeg = 0f;
                return Vector3.forward;
            }

            float tilt = Mathf.Clamp01(force) * ForceTiltMaxDeg * Mathf.Deg2Rad;
            float surfaceScale = Mathf.Sin(tilt) / magnitude;
            var direction = new Vector3(surface.x * surfaceScale, surface.y * surfaceScale, Mathf.Cos(tilt));
            xTiltDeg = Mathf.Round(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg);
            yTiltDeg = Mathf.Round(Mathf.Atan2(direction.y, direction.z) * Mathf.Rad2Deg);
            return direction;
        }
    }
}
