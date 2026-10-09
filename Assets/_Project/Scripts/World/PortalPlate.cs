using UnityEngine;
using SpellSimulator.Interfaces;

namespace SpellSimulator.World
{
    /// <summary>
    /// Marca la placa de un portal como lienzo con disparador: el anillo cerrado deja el sello ARMADO en vez de lanzarlo, y
    /// es el <see cref="Portal"/> quien decide (abre el portal si coincide con su receta; si no, lanza el hechizo normal).
    /// Lo agrega el Portal solo; va en el mismo objeto que el DrawingCanvas de la placa.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortalPlate : MonoBehaviour, ISealTrigger
    {
    }
}
