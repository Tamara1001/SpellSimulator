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

        /// <summary>Piso (0 planta baja, 1 piso 1, 2 piso 2) al que pertenece una altura local; las herramientas apoyadas en un mueble cuentan como el piso de abajo.</summary>
        public static int FloorOf(float localY) => Mathf.Clamp(Mathf.FloorToInt((localY + 0.5f) / FloorHeight), 0, Levels - 2);

        /// <summary>
        /// Indice del farol de nivel (0 piso 2, 1 piso 1, 2 planta baja, 3 puerta) que enciende una pluma agarrada en ese punto local,
        /// o -1 si no enciende ninguno: el farol del piso 2 sigue encendiendose al llegar y una herramienta fuera de la torre no cuenta.
        /// </summary>
        public static int ToolLamp(Vector3 local)
        {
            if (new Vector2(local.x, local.z).magnitude > InnerRadius) return -1;
            int floor = FloorOf(local.y);
            return floor == Levels - 2 ? -1 : Levels - 2 - floor;
        }

        /// <summary>True si el jugador esta en el vano de la puerta (desde el espesor del muro hasta la barrera, en la planta baja).</summary>
        public static bool InDoorway(Vector3 local)
        {
            if (local.y > ArrivalMargin + 0.6f) return false;
            float radius = new Vector2(local.x, local.z).magnitude;
            if (radius < InnerRadius + 0.35f) return false;
            if (radius > BarrierRadius - 0.3f) return false; // el vano termina en la barrera: mas afuera es la pradera (MAP-04)
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

        private int toolLamps;   // mascara de faroles de nivel encendidos por agarrar una pluma (piso 1 y planta baja)

        /// <summary>
        /// ¿Esta encendido el farol de nivel <paramref name="lamp"/> (0 piso 2, 1 piso 1, 2 planta baja, 3 puerta)? El del piso 2 y el de la puerta
        /// siguen a los niveles pasados; los del piso 1 y la planta baja, a la pluma agarrada en su sala.
        /// </summary>
        public bool LampLit(int lamp)
        {
            if (lamp == 1 || lamp == 2) return (toolLamps & (1 << lamp)) != 0;
            return lamp < Passed;
        }

        /// <summary>Se agarro una pluma en <paramref name="local"/> (relativo al pie de la torre). Devuelve el farol que se enciende, o -1.</summary>
        public int ToolPickedUp(Vector3 local)
        {
            if (!Active) return -1;
            int lamp = TutorialRules.ToolLamp(local);
            if (lamp < 0 || (toolLamps & (1 << lamp)) != 0) return -1;
            toolLamps |= 1 << lamp;
            return lamp;
        }

        /// <summary>Empieza (o vuelve a empezar) el tutorial desde la terraza.</summary>
        public void Begin()
        {
            Active = true;
            Passed = 0;
            toolLamps = 0;
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
