using UnityEngine;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Matematica pura del viento (sin estado ni MonoBehaviour, testeable en EditMode): arrastre cuadratico,
    /// corriente vertical y sesgo de propagacion del fuego.
    /// </summary>
    public static class WindMath
    {
        /// <summary>Densidad del aire "de juego" (kg/m3): ajusta cuanto empuja el viento a los cuerpos.</summary>
        public const float AirDensity = 1.2f;
        /// <summary>Coeficiente de arrastre por defecto (cuerpo romo).</summary>
        public const float DefaultCd = 1f;
        /// <summary>Masa minima para dividir (kg): evita aceleraciones infinitas.</summary>
        public const float MinMass = 0.01f;
        /// <summary>Velocidad vertical maxima de una columna de aire (m/s).</summary>
        public const float MaxUpdraftSpeed = 6f;
        /// <summary>Aceleracion maxima que una columna le imprime a un cuerpo (m/s2).</summary>
        public const float MaxUpdraftAcceleration = 40f;
        /// <summary>Acople de la columna (1/s) para un cuerpo de 1 kg.</summary>
        public const float UpdraftGain = 8f;
        /// <summary>Cuanto favorece (o frena) el viento la propagacion del fuego a favor (o en contra) de su direccion.</summary>
        public const float SpreadGain = 3f;
        /// <summary>Piso del multiplicador de propagacion contra el viento (nunca llega a 0).</summary>
        public const float MinSpreadFactor = 0.15f;

        /// <summary>
        /// Aceleracion (m/s2) de un cuerpo por arrastre: F = 0.5 * rho * Cd * A * (vViento - vCuerpo)|vViento - vCuerpo|, a = F / m,
        /// con tope <paramref name="maxAcceleration"/>. Cuadratica en la velocidad relativa; los livianos (poca masa) aceleran mucho mas.
        /// </summary>
        public static Vector3 Drag(Vector3 windVelocity, Vector3 bodyVelocity, float mass, float area, float cd, float maxAcceleration)
        {
            Vector3 relative = windVelocity - bodyVelocity;
            float speed = relative.magnitude;
            if (speed < 1e-4f || area <= 0f) return Vector3.zero;
            float force = 0.5f * AirDensity * cd * area * speed * speed;
            float acceleration = Mathf.Min(force / Mathf.Max(mass, MinMass), maxAcceleration);
            return relative * (acceleration / speed);
        }

        /// <summary>Area proyectada estimada de una caja: la cara mas grande (m2).</summary>
        public static float ProjectedArea(Vector3 size)
        {
            float xy = size.x * size.y, yz = size.y * size.z, xz = size.x * size.z;
            return Mathf.Max(xy, Mathf.Max(yz, xz));
        }

        /// <summary>
        /// Aceleracion vertical (m/s2, hacia arriba) de una columna de aire sobre un cuerpo. <paramref name="radial01"/> es la
        /// distancia al eje sobre el radio (0 centro, 1 borde): la velocidad objetivo cae hacia el borde. Tira hacia la
        /// velocidad objetivo (nunca frena una subida mas rapida) y escala con 1/masa: los livianos suben, los pesados no.
        /// </summary>
        public static float UpdraftAcceleration(float radial01, float verticalVelocity, float mass, float maxSpeed = MaxUpdraftSpeed)
        {
            float target = UpdraftTarget(radial01, maxSpeed);
            float missing = target - verticalVelocity;
            if (missing <= 0f) return 0f;
            float lift = Mathf.Min(1f / Mathf.Max(mass, 0.25f), 4f);
            return Mathf.Min(missing * UpdraftGain * lift, MaxUpdraftAcceleration);
        }

        /// <summary>Velocidad vertical objetivo de la columna a una distancia radial normalizada (0 centro, 1 borde).</summary>
        public static float UpdraftTarget(float radial01, float maxSpeed = MaxUpdraftSpeed)
        {
            float r = Mathf.Clamp01(radial01);
            return maxSpeed * (1f - 0.6f * r);
        }

        /// <summary>
        /// Multiplicador de la probabilidad de que el fuego salte de una celda a la vecina en el paso (stepX, stepZ) (enteros -1..1)
        /// con viento horizontal unitario (windX, windZ) de intensidad <paramref name="strength"/> (0..1). A favor: hasta 1 + SpreadGain;
        /// en contra: baja hasta MinSpreadFactor; de costado: 1.
        /// </summary>
        public static float SpreadBias(int stepX, int stepZ, float windX, float windZ, float strength)
        {
            float len = Mathf.Sqrt(stepX * stepX + stepZ * stepZ);
            if (len < 1e-4f) return 1f;
            float alignment = (stepX * windX + stepZ * windZ) / len;
            return Mathf.Max(MinSpreadFactor, 1f + SpreadGain * Mathf.Clamp01(strength) * alignment);
        }
    }
}
