using UnityEngine;

namespace SpellSimulator.Reactions
{
    /// <summary>
    /// Cuentas puras del agua fisica (octava tanda, seccion 3): charcos (volumen, radio, evaporacion, fusion, deslizamiento
    /// por la pendiente), zonas inundadas (flotacion, nivel en el tiempo), recipientes (masa extra, vuelco) y cuanto
    /// multiplica la tinta poderosa. Sin MonoBehaviour: se prueba en WaterMathTests.
    /// </summary>
    public static class WaterMath
    {
        // ---------------------------------------------------------------- charcos

        /// <summary>Espesor de la lamina de agua de un charco (m): fija cuanto se extiende un volumen dado.</summary>
        public const float FilmDepth = 0.01f;
        /// <summary>Radio minimo visible de un charco con agua (m).</summary>
        public const float MinPuddleRadius = 0.08f;
        /// <summary>Volumen maximo de un charco (litros). Con 12 L el radio queda en ~0.62 m.</summary>
        public const float PuddleMaxLiters = 12f;
        /// <summary>Litros que aporta cada gota que choca (con tinta normal).</summary>
        public const float DropLiters = 0.02f;
        /// <summary>Vida de un charco lleno con tinta normal (s) y limites (20-40 s, decision cerrada del plan).</summary>
        public const float BasePuddleLife = 24f, MinPuddleLife = 20f, MaxPuddleLife = 40f;
        /// <summary>Fusion: dos charcos se unen si sus centros estan a menos de (rA + rB) x este factor.</summary>
        public const float MergeOverlap = 0.8f;
        /// <summary>Pendiente (dh/dx) por debajo de la cual el charco no se mueve.</summary>
        public const float MinSlope = 0.03f;
        /// <summary>Velocidad de deslizamiento por unidad de pendiente (m/s) y tope (m/s).</summary>
        public const float SlideGain = 1.2f, MaxSlideSpeed = 0.6f;
        /// <summary>Pendiente maxima que se considera terreno (45 grados); mas es una pared o un escalon.</summary>
        public const float MaxSlope = 1f;

        /// <summary>Radio (m) de un charco de <paramref name="liters"/> litros extendido como lamina de FilmDepth. 0 si no hay agua.</summary>
        public static float PuddleRadius(float liters)
        {
            if (liters <= 0f) return 0f;
            float area = liters * 0.001f / FilmDepth;
            return Mathf.Max(MinPuddleRadius, Mathf.Sqrt(area / Mathf.PI));
        }

        /// <summary>Vida de un charco lleno (s) segun la tinta: la poderosa dura mas (20-40 s).</summary>
        public static float PuddleLifeSeconds(float inkPower)
        {
            return Mathf.Clamp(BasePuddleLife * Mathf.Sqrt(Mathf.Max(inkPower, 0f)), MinPuddleLife, MaxPuddleLife);
        }

        /// <summary>Litros por segundo que pierde un charco (evaporacion + filtracion): uno lleno dura PuddleLifeSeconds.</summary>
        public static float EvaporationRate(float inkPower) => PuddleMaxLiters / PuddleLifeSeconds(inkPower);

        /// <summary>Litros que agrega una gota segun la tinta.</summary>
        public static float DropVolume(float inkPower) => DropLiters * Mathf.Max(inkPower, 0f);

        /// <summary>Suma agua a un charco sin pasarse del maximo.</summary>
        public static float AddLiters(float liters, float added) => Mathf.Clamp(liters + added, 0f, PuddleMaxLiters);

        /// <summary>True si dos charcos (centros en el plano XZ y radios) se tocan lo suficiente para fusionarse.</summary>
        public static bool ShouldMerge(Vector2 a, float radiusA, Vector2 b, float radiusB)
        {
            if (radiusA <= 0f || radiusB <= 0f) return false;
            float reach = (radiusA + radiusB) * MergeOverlap;
            return (a - b).sqrMagnitude <= reach * reach;
        }

        /// <summary>Centro del charco fusionado: promedio pesado por volumen.</summary>
        public static Vector3 MergedCenter(Vector3 a, float litersA, Vector3 b, float litersB)
        {
            float total = litersA + litersB;
            if (total <= 1e-6f) return (a + b) * 0.5f;
            return (a * litersA + b * litersB) / total;
        }

        /// <summary>
        /// Gradiente del terreno (dh/dx, dh/dz) con alturas muestreadas a +-<paramref name="spacing"/> en X (este/oeste) y
        /// Z (norte/sur). Cada componente se recorta a MaxSlope: un escalon o una pared no es una pendiente.
        /// </summary>
        public static Vector2 Gradient(float east, float west, float north, float south, float spacing)
        {
            if (spacing <= 1e-4f) return Vector2.zero;
            float gx = Mathf.Clamp((east - west) / (2f * spacing), -MaxSlope, MaxSlope);
            float gz = Mathf.Clamp((north - south) / (2f * spacing), -MaxSlope, MaxSlope);
            return new Vector2(gx, gz);
        }

        /// <summary>Velocidad (m/s, plano XZ) con la que el charco escurre cuesta abajo. Cero en terreno casi plano.</summary>
        public static Vector2 SlideVelocity(Vector2 gradient)
        {
            float slope = gradient.magnitude;
            if (slope < MinSlope) return Vector2.zero;
            float speed = Mathf.Min(MaxSlideSpeed, SlideGain * slope);
            return -gradient / slope * speed;
        }

        // ---------------------------------------------------------------- flotacion y zonas inundadas

        /// <summary>Solo flota lo liviano en una zona inundada (kg).</summary>
        public const float MaxFloatMass = 8f;
        /// <summary>Vida de una zona inundada: 12-20 s (decision cerrada del plan).</summary>
        public const float MinFloodSeconds = 12f, MaxFloodSeconds = 20f;
        /// <summary>Tiempos de llenado y de drenaje de la zona (s).</summary>
        public const float FloodFillSeconds = 2f, FloodDrainSeconds = 3f;
        /// <summary>Requisitos de un hechizo de agua para inundar: radio del anillo (m), fuerza y cuanto apunta hacia abajo.</summary>
        public const float FloodMinRingRadius = 0.35f, FloodMinForce = 0.5f, FloodMinDownDot = 0.5f;

        /// <summary>Fraccion (0-1) de un objeto de base <paramref name="bottomY"/> y alto <paramref name="height"/> bajo la superficie.</summary>
        public static float SubmergedFraction(float bottomY, float height, float surfaceY)
        {
            return Mathf.Clamp01((surfaceY - bottomY) / Mathf.Max(height, 1e-3f));
        }

        /// <summary>Aceleracion hacia arriba (m/s2) del empuje en una zona inundada; 0 si el cuerpo es pesado (>= MaxFloatMass).</summary>
        public static float BuoyantAcceleration(float mass, float submerged, float buoyancy, float gravity)
        {
            if (mass >= MaxFloatMass || submerged <= 0f) return 0f;
            return Mathf.Abs(gravity) * Mathf.Max(buoyancy, 0f) * Mathf.Clamp01(submerged);
        }

        /// <summary>True si el hechizo de agua es lo bastante grande, fuerte y apuntado al suelo como para inundar.</summary>
        public static bool IsFloodSpell(float ringRadius, float force, float downDot)
        {
            return ringRadius >= FloodMinRingRadius && force >= FloodMinForce && downDot >= FloodMinDownDot;
        }

        /// <summary>Duracion de la zona inundada (s): mas fuerza y tinta poderosa la alargan, siempre dentro de 12-20 s.</summary>
        public static float FloodSeconds(float force, float inkPower)
        {
            float baseSeconds = MinFloodSeconds + 4f * Mathf.Clamp01(force);
            return Mathf.Clamp(baseSeconds * Mathf.Sqrt(Mathf.Max(inkPower, 0f)), MinFloodSeconds, MaxFloodSeconds);
        }

        /// <summary>Nivel (0-1) de la zona inundada a los <paramref name="t"/> s de una vida de <paramref name="life"/> s: llena, se mantiene, drena.</summary>
        public static float FloodLevel(float t, float life)
        {
            if (t <= 0f || t >= life || life <= 0f) return 0f;
            float fill = Mathf.SmoothStep(0f, 1f, t / FloodFillSeconds);
            float drain = Mathf.SmoothStep(0f, 1f, (life - t) / FloodDrainSeconds);
            return Mathf.Min(fill, drain);
        }

        // ---------------------------------------------------------------- recipientes y tinta

        /// <summary>Angulo (grados) a partir del cual un recipiente volcado derrama.</summary>
        public const float SpillAngle = 100f;

        /// <summary>Masa que suma el agua de un recipiente (1 kg por litro), con tope.</summary>
        public static float ContainerExtraMass(float liters, float maxExtra) => Mathf.Clamp(liters, 0f, Mathf.Max(maxExtra, 0f));

        /// <summary>True si el recipiente esta tan inclinado que se derrama.</summary>
        public static bool ShouldSpill(float tiltDegrees) => tiltDegrees > SpillAngle;

        /// <summary>Multiplicador del caudal de particulas segun la tinta (normal = 1, poderosa = 2.5; recortado a 0.5-3).</summary>
        public static float FlowMultiplier(float inkPower) => Mathf.Clamp(inkPower, 0.5f, 3f);
    }
}
