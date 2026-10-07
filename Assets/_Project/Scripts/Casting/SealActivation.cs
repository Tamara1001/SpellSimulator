using SpellSimulator.Parsing;

namespace SpellSimulator.Casting
{
    /// <summary>Fase de un sello dibujado en una superficie.</summary>
    public enum SealPhase
    {
        /// <summary>Sin anillo reconocible.</summary>
        Drawing,
        /// <summary>Anillo abierto: el sello espera a que lo cierren.</summary>
        Prepared,
        /// <summary>El hechizo ya se lanzo. No vuelve a lanzarse hasta que el anillo se abra o se borre.</summary>
        Spent
    }

    /// <summary>
    /// Regla canon: el hechizo se lanza al cerrar el anillo. Decide la fase del sello y si corresponde intentar el cast.
    /// Logica pura; SpellCaster la aplica al terminar cada trazo.
    /// </summary>
    public static class SealActivation
    {
        /// <param name="ring">Anillo detectado tras el cambio.</param>
        /// <param name="current">Fase antes del cambio.</param>
        /// <param name="castAllowed">False para ediciones que no pueden cerrar un anillo (borrar, deshacer).</param>
        /// <param name="shouldCast">True si hay que compilar y lanzar ahora.</param>
        public static SealPhase Evaluate(RingInfo ring, SealPhase current, bool castAllowed, out bool shouldCast)
        {
            shouldCast = false;
            if (!ring.Found) return SealPhase.Drawing;
            if (!ring.Complete) return SealPhase.Prepared;

            // Anillo cerrado. Un sello gastado no se relanza con trazos nuevos.
            if (current == SealPhase.Spent) return SealPhase.Spent;

            // Si el sello cerrado fue invalido (por ejemplo, anillo dibujado primero), cada trazo nuevo reintenta.
            shouldCast = castAllowed;
            return current;
        }

        /// <summary>Fase tras un intento de cast con el anillo cerrado.</summary>
        public static SealPhase AfterCast(bool launched) => launched ? SealPhase.Spent : SealPhase.Prepared;
    }
}
