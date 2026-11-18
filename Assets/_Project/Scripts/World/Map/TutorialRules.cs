using UnityEngine;

namespace SpellSimulator.World.Map
{
    /// <summary>
    /// Reglas puras de la torre-tutorial (Docs/MAPA_FLUJO_PLAN.md, §17). Cuatro niveles, de arriba hacia abajo:
    /// 0 terraza (movilidad), 1 piso 2 (mover objetos), 2 piso 1 (objetos magicos), 3 planta baja (abrir la puerta).
    /// Un nivel se da por pasado cuando el jugador llega al piso de abajo; el ultimo, cuando cruza el vano de la puerta.
    /// Nada de esto bloquea: solo enciende faroles y, al salir, apaga la barrera magica. Posiciones relativas al pie de la torre.
    /// </summary>
    public static class TutorialRules
    {
        public const int Levels = 4;

        /// <summary>Medidas de la torre (iguales a las de TowerBuilder; lo fija un test).</summary>
        public const float FloorHeight = 5f, InnerRadius = 5.8f, OuterRadius = 6.4f;

        /// <summary>Angulo de la puerta (grados, 0 = este, 90 = norte) y medio ancho angular del vano.</summary>
        public const float DoorAngle = 270f, DoorHalfAngle = 12f;

        /// <summary>Radio de la barrera magica: un prisma alrededor de la torre, por fuera del balcon de la terraza (8,2 m) y del talud de la base (8,2 m).</summary>
        public const float BarrierRadius = 8.7f;

        /// <summary>Margen sobre el piso de abajo: por debajo de esa altura del pivote el jugador ya bajo (el pivote va a 1-1,7 m del piso).</summary>
        private const float ArrivalMargin = 2.6f;

        /// <summary>Niveles pasados (0..4) segun donde esta el jugador. No baja nunca por si solo: lo acumula <see cref="TutorialProgress"/>.</summary>
        public static int PassedAt(Vector3 local)
        {
            if (InDoorway(local)) return Levels;
            int passed = 0;
            for (int level = 0; level < Levels - 1; level++)
            {
                float floorBelow = (Levels - 2 - level) * FloorHeight;
                if (local.y < floorBelow + ArrivalMargin) passed = level + 1;
            }
            return passed;
        }

        /// <summary>True si el jugador esta en el vano de la puerta (dentro del espesor del muro, en la planta baja).</summary>
        public static bool InDoorway(Vector3 local)
        {
            if (local.y > ArrivalMargin + 0.6f) return false;
            float radius = new Vector2(local.x, local.z).magnitude;
            if (radius < InnerRadius + 0.35f) return false;
            float angle = Mathf.Atan2(local.z, local.x) * Mathf.Rad2Deg;
            return Mathf.Abs(Mathf.DeltaAngle(angle, DoorAngle)) <= DoorHalfAngle;
        }
    }

    /// <summary>Estado de una pasada por el tutorial. Sin escena: lo maneja TowerTutorial y lo cubren los tests.</summary>
    public sealed class TutorialProgress
    {
        /// <summary>El jugador esta haciendo el tutorial (la barrera esta puesta).</summary>
        public bool Active { get; private set; }
        /// <summary>Niveles pasados en esta pasada (0..<see cref="TutorialRules.Levels"/>).</summary>
        public int Passed { get; private set; }
        public bool Completed => Passed >= TutorialRules.Levels;

        /// <summary>Empieza (o vuelve a empezar) el tutorial desde la terraza.</summary>
        public void Begin()
        {
            Active = true;
            Passed = 0;
        }

        /// <summary>Lo corta sin terminarlo (salteo): la barrera se apaga y los faroles quedan como estaban.</summary>
        public void Skip() => Active = false;

        /// <summary>Actualiza con la posicion del jugador (relativa al pie de la torre). Devuelve cuantos niveles nuevos paso.</summary>
        public int Advance(Vector3 local)
        {
            if (!Active) return 0;
            int now = TutorialRules.PassedAt(local);
            if (now <= Passed) return 0;
            int gained = now - Passed;
            Passed = now;
            if (Completed) Active = false;
            return gained;
        }
    }
}
