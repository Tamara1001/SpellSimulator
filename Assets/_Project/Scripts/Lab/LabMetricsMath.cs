using UnityEngine;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Estado del detector de reposo de un cuerpo (un segmento de movimiento). <c>default</c> = esperando que empiece.
    /// Lo avanza <see cref="LabMetricsMath.SettleStep"/>; para medir otro segmento, volver a <c>default</c>.
    /// </summary>
    public struct SettleState
    {
        /// <summary>True desde que la velocidad supero el umbral por primera vez.</summary>
        public bool Started;
        /// <summary>True si en el ultimo paso la velocidad estaba sobre el umbral (dentro del segmento).</summary>
        public bool Moving;
        /// <summary>True cuando el segmento termino (reposo sostenido o tope de tiempo).</summary>
        public bool Finished;
        /// <summary>True si termino por el tope de <see cref="LabMetricsMath.MaxSegmentSeconds"/> y no por reposo.</summary>
        public bool TimedOut;
        /// <summary>Segundos acumulados desde que empezo el segmento.</summary>
        public float Elapsed;
        /// <summary>Segundos seguidos bajo el umbral (se reinicia si vuelve a moverse).</summary>
        public float StillTime;

        /// <summary>True mientras el segmento esta abierto (empezo y no termino).</summary>
        public bool Active => Started && !Finished;
    }

    /// <summary>
    /// Matematica pura de las mediciones del laboratorio (sin estado ni MonoBehaviour, testeable en EditMode): area quemada,
    /// desplazamiento horizontal y detector de reposo. Sin allocations.
    /// </summary>
    public static class LabMetricsMath
    {
        /// <summary>Area de una celda de la grilla de fuego (m2): celdas de 2 x 2 m.</summary>
        public const float CellAreaM2 = 4f;
        /// <summary>Velocidad (m/s) que abre un segmento de movimiento y bajo la cual se cuenta el reposo.</summary>
        public const float StartSpeed = 0.3f;
        /// <summary>Segundos seguidos bajo <see cref="StartSpeed"/> para dar el segmento por terminado.</summary>
        public const float SettleSeconds = 1f;
        /// <summary>Duracion maxima de un segmento (s): un cuerpo que rueda sin parar se corta aca.</summary>
        public const float MaxSegmentSeconds = 15f;
        /// <summary>Tolerancia para las sumas de dt (10 x 0.1 da 0.99999994).</summary>
        private const float TimeEpsilon = 1e-4f;

        /// <summary>Area quemada (m2) a partir de la cantidad de celdas quemadas.</summary>
        public static float AreaBurnedM2(int cells) => cells <= 0 ? 0f : cells * CellAreaM2;

        /// <summary>Distancia en el plano XZ entre dos posiciones (ignora la altura).</summary>
        public static float HorizontalDisplacement(Vector3 a, Vector3 b)
        {
            float dx = b.x - a.x, dz = b.z - a.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Avanza el detector de reposo un paso. El segmento empieza cuando <paramref name="speed"/> supera
        /// <see cref="StartSpeed"/> y termina tras <see cref="SettleSeconds"/> seguidos por debajo, o al llegar a
        /// <see cref="MaxSegmentSeconds"/>. Devuelve true solo en el paso en que termina.
        /// </summary>
        public static bool SettleStep(ref SettleState state, float speed, float dt)
        {
            if (state.Finished) return false;

            if (!state.Started)
            {
                if (speed > StartSpeed)
                {
                    state.Started = true;
                    state.Moving = true;
                    state.Elapsed = 0f;
                    state.StillTime = 0f;
                }
                return false;
            }

            if (dt > 0f) state.Elapsed += dt;
            if (speed < StartSpeed)
            {
                state.Moving = false;
                if (dt > 0f) state.StillTime += dt;
            }
            else
            {
                state.Moving = true;
                state.StillTime = 0f;
            }

            if (state.StillTime >= SettleSeconds - TimeEpsilon)
            {
                state.Finished = true;
                state.TimedOut = false;
                state.Moving = false;
                return true;
            }
            if (state.Elapsed >= MaxSegmentSeconds - TimeEpsilon)
            {
                state.Finished = true;
                state.TimedOut = true;
                state.Moving = false;
                return true;
            }
            return false;
        }
    }
}
