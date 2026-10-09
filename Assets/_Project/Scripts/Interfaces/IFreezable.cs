using UnityEngine;

namespace SpellSimulator.Interfaces
{
    /// <summary>
    /// Agua que el hechizo de cristal puede congelar (charcos, zonas inundadas, estanques): reaccion agua + cristal = hielo.
    /// El cristal busca estos componentes con OverlapSphere (GetComponentInParent) y les pide congelarse.
    /// </summary>
    public interface IFreezable
    {
        /// <summary>Congela lo que cae dentro de la esfera (centro, radio en metros) durante <paramref name="seconds"/>. Devuelve true si congelo algo.</summary>
        bool Freeze(Vector3 center, float radius, float seconds);
    }
}
