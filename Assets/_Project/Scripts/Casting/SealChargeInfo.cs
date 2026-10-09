using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Compiler;

namespace SpellSimulator.Casting
{
    /// <summary>
    /// Datos de un sello que se esta cargando antes de lanzarse (retardo de lanzamiento). Los emite
    /// <see cref="SpellCaster.SealCharging"/> para que lo visual (brillo del anillo) no dependa de la logica.
    /// </summary>
    public struct SealChargeInfo
    {
        public DrawingCanvas canvas;
        /// <summary>Elemento compilado del hechizo (define el color del brillo: fuego rojo, agua celeste...).</summary>
        public ElementType element;
        /// <summary>Id del sigilo (p. ej. "crystal" tiene su propio aspecto).</summary>
        public string sigilId;
        /// <summary>Segundos totales del retardo (0,5 a 2 segun el tamano del anillo).</summary>
        public float seconds;
        /// <summary>Centro del anillo en UV del lienzo (0..1).</summary>
        public Vector2 ringCenterUV;
        /// <summary>Radio del anillo en metros del mundo.</summary>
        public float ringRadiusWorld;
        /// <summary>Clase de tinta dominante (para tintes futuros).</summary>
        public Core.InkKind ink;
    }
}
