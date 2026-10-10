using UnityEngine;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Marco del sello en el mundo: origen en el centro del anillo, ejes del papel y normal de emision.
    /// Convierte espacio-sello (x derecha, y arriba, z afuera; 1 unidad = radio) a mundo.
    /// </summary>
    public struct SealFrame
    {
        public Vector3 Origin;
        public Vector3 Right;
        public Vector3 Up;
        /// <summary>Hacia donde sale la magia ("afuera del papel").</summary>
        public Vector3 Normal;
        /// <summary>Radio del anillo en metros.</summary>
        public float Radius;
        /// <summary>Escala visual del efecto en metros (radio x effectScale).</summary>
        public float Scale;

        public Vector3 Direction(Vector3 seal)
        {
            Vector3 world = Right * seal.x + Up * seal.y + Normal * seal.z;
            return world.sqrMagnitude > 1e-8f ? world.normalized : Normal;
        }

        /// <summary>
        /// Hacia donde "sale" el hechizo sobre la superficie: en el piso, lejos de quien dibuja (el borde de arriba del dibujo, horizontal);
        /// en una pared, hacia afuera del papel.
        /// </summary>
        public Vector3 Outward
        {
            get
            {
                if (Normal.y <= 0.5f) return Normal;
                var away = new Vector3(Up.x, 0f, Up.z);
                return away.sqrMagnitude < 1e-4f ? Vector3.forward : away.normalized;
            }
        }

        /// <summary>Punto en espacio-sello (unidades de radio) a mundo.</summary>
        public Vector3 Point(Vector3 seal) => Origin + (Right * seal.x + Up * seal.y + Normal * seal.z) * Radius;

        public Quaternion Facing(Vector3 forward)
        {
            Vector3 up = Mathf.Abs(Vector3.Dot(forward, Up)) > 0.99f ? Right : Up;
            return Quaternion.LookRotation(forward, up);
        }
    }
}
