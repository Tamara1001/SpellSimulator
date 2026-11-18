using UnityEngine;

namespace SpellSimulator.World.Map
{
    /// <summary>
    /// Donde aparece el jugador (Docs/MAPA_FLUJO_PLAN.md, D19, D20 y D23). Logica pura: solo depende de <see cref="ValleyShape"/>.
    /// - Primer spawn (tutorial): la terraza de la torre, mirando al sur (el nexo y el anillo de campos).
    /// - Nexo: el cruce de caminos al pie de la loma (<see cref="ValleyShape.Spawn"/>). Ahi se reaparece y ahi esta el tablero
    ///   con el mapa; tambien es donde se aparece si se saltea el tutorial.
    /// </summary>
    public static class SpawnRules
    {
        /// <summary>Altura del piso de la terraza sobre el pie de la torre. Igual a TowerBuilder.TerraceY (lo fija un test).</summary>
        public const float TerraceHeight = 15f;

        /// <summary>Altura del pivote del jugador sobre el piso al ubicarlo (la misma que usa MapBuilder).</summary>
        public const float BodyLift = 1.7f;

        /// <summary>Por debajo de esta altura el jugador se cayo del mundo y vuelve al nexo (igual que Respawnable).</summary>
        public const float FallHeight = -8f;

        /// <summary>Punto de la terraza donde arranca el tutorial, relativo al centro de la torre (XZ): en el balcon, al norte (r = 6,6 m, 90 grados; el hueco de la rampa va de 61 a 180 grados y queda por dentro del balcon): se mira al este-sureste (rumbo 115), por donde sigue el recorrido en sentido horario.</summary>
        public static readonly Vector2 TerraceOffset = new Vector2(0f, 6.6f);

        /// <summary>Rumbo (grados, 0 = norte, 90 = este) con el que se aparece en la terraza y en el nexo.</summary>
        public const float TerraceYaw = 115f, NexusYaw = 45f;

        /// <summary>Tablero del mapa: al noreste del nexo, entre el camino a la torre y el camino al laboratorio.</summary>
        public static readonly Vector2 MapBoard = new Vector2(3.6f, -2.4f);

        public static Vector3 Nexus =>
            new Vector3(ValleyShape.Spawn.x, ValleyShape.Height(ValleyShape.Spawn.x, ValleyShape.Spawn.y) + BodyLift, ValleyShape.Spawn.y);

        public static Vector3 Terrace
        {
            get
            {
                Vector2 tower = ValleyShape.TowerCenter;
                float floor = ValleyShape.Height(tower.x, tower.y) + TerraceHeight;
                return new Vector3(tower.x + TerraceOffset.x, floor + BodyLift, tower.y + TerraceOffset.y);
            }
        }

        /// <summary>Posicion y rumbo al empezar una partida: la terraza (tutorial) o el nexo (tutorial salteado).</summary>
        public static Vector3 Start(bool skipTutorial, out float yaw)
        {
            yaw = skipTutorial ? NexusYaw : TerraceYaw;
            return skipTutorial ? Nexus : Terrace;
        }

        /// <summary>True si el jugador se cayo del mundo y hay que devolverlo al nexo.</summary>
        public static bool FellOut(float y) => y < FallHeight;
    }
}
