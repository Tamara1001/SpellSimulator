using UnityEngine;

namespace SpellSimulator.World.Map
{
    /// <summary>
    /// Reglas puras de la terraza-tutorial (Docs/MAPA_FLUJO_PLAN.md, §17 y v2): una viga baja (hay que agacharse) y un muro bajo
    /// (hay que saltar) cruzan la terraza de borde a borde, pasando por el centro hasta la baranda del hueco, asi que no se les da la vuelta.
    /// Una reja magica tapa la bajada hasta que se hicieron las dos cosas. Posiciones relativas al pie de la torre.
    /// </summary>
    public static class TerraceRules
    {
        /// <summary>Angulos (grados, 0 = este) de la viga y del muro bajo.</summary>
        public const float BeamAngle = 25f, JumpWallAngle = 325f;
        /// <summary>Radio de la terraza, medio ancho de la franja que cuenta como «bajo la viga» / «sobre el muro» y altura de la terraza.</summary>
        public const float Radius = 8.2f, StripHalfWidth = 0.55f, TerraceY = 13.5f;
        /// <summary>Angulo de la reja que tapa la rampa (entre las barandas del hueco).</summary>
        public const float GateAngle = 195f;

        /// <summary>True si el punto esta sobre la linea de ese angulo (a cualquiera de los dos lados del centro), dentro de la franja.</summary>
        public static bool OnLine(Vector3 local, float angle, float halfWidth)
        {
            if (local.y < TerraceY - 0.5f || local.y > TerraceY + 4f) return false;
            float a = angle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
            float along = local.x * cos + local.z * sin;
            float lateral = local.x * sin - local.z * cos;
            return Mathf.Abs(lateral) <= halfWidth && Mathf.Abs(along) <= Radius;
        }

        /// <summary>Esta pasando agachado bajo la viga.</summary>
        public static bool Crouched(Vector3 local, bool low) => low && OnLine(local, BeamAngle, StripHalfWidth);

        /// <summary>Esta pasando por el aire sobre el muro bajo.</summary>
        public static bool Jumped(Vector3 local, bool grounded) => !grounded && OnLine(local, JumpWallAngle, StripHalfWidth + 0.2f);
    }
}
