using System;

namespace SpellSimulator.Learning
{
    /// <summary>
    /// Logica pura del menu de pausa del grimorio (pagina "Pausa"): confirmacion con vencimiento del reinicio del mundo
    /// y ciclado de opciones. Los pasos de sensibilidad/volumen viven en <see cref="GrimoireOptions"/> y la calidad grafica
    /// en Rendering.GraphicsQuality (no se duplican aca). Tests EditMode: PauseMenuMathTests.
    /// </summary>
    public static class PauseMenuMath
    {
        /// <summary>Segundos que dura armada la confirmacion («¿Seguro? Clic de nuevo»).</summary>
        public const float ConfirmSeconds = 3f;

        /// <summary>Valor de "no armado" (un instante muy anterior a cualquier tiempo de juego).</summary>
        public const float Disarmed = float.NegativeInfinity;

        /// <summary>True si la confirmacion se armo en armedAt y todavia no vencio en now.</summary>
        public static bool IsArmed(float armedAt, float now) =>
            !float.IsNegativeInfinity(armedAt) && now >= armedAt && now - armedAt <= ConfirmSeconds;

        /// <summary>Rotulo del boton de reinicio segun si esta armado.</summary>
        public static string ResetLabel(bool armed) => armed ? "¿Seguro? Clic de nuevo" : "Reiniciar el mundo";

        /// <summary>Siguiente (dir &gt; 0) o anterior indice de una lista circular de count elementos.</summary>
        public static int Cycle(int index, int count, int dir)
        {
            if (count <= 0) return 0;
            int next = (index + Math.Sign(dir)) % count;
            return next < 0 ? next + count : next;
        }
    }
}
