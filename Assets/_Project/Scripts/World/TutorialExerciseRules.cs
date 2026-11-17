using UnityEngine;

namespace SpellSimulator.World
{
    /// <summary>Reglas puras de los ejercicios opcionales del piso 2 (dejar flotando, girar el lienzo, arrojar).</summary>
    public static class TutorialExerciseRules
    {
        /// <summary>Segundos que el lienzo debe quedar quieto dentro del marco para contar.</summary>
        public const float HoldSeconds = 0.4f;

        /// <summary>Velocidad relativa minima (m/s) de un golpe para que cuente como «arrojado».</summary>
        public const float ThrowSpeed = 3f;

        /// <summary>Tolerancia: cuanto puede inclinarse la normal del lienzo respecto de la horizontal para estar «parado».</summary>
        public const float UprightTolerance = 0.35f;

        /// <summary>El lienzo esta parado (de canto al suelo) si su normal es casi horizontal.</summary>
        public static bool Upright(Vector3 normal) => Mathf.Abs(normal.y) < UprightTolerance;

        /// <summary>El golpe fue lo bastante fuerte para ser un tiro y no un roce.</summary>
        public static bool HardEnough(float speed) => speed >= ThrowSpeed;
    }
}
