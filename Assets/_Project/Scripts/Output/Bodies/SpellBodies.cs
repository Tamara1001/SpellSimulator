using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Compiler;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Punto unico donde cada elemento agrega sus cuerpos fisicos propios al lanzar un hechizo (bloque 3 del plan):
    /// roca (tierra), cristal, agua y viento. Se llama desde <see cref="SpellOutputController.SpawnSpellEffect"/> (con <paramref name="visualOnly"/> = true en un cliente de red: sin cuerpos de tierra) despues de crear
    /// la manifestacion visual (particulas) y su <see cref="MagicField"/>. Cada modulo vive en su propio archivo.
    /// </summary>
    public static class SpellBodies
    {
        /// <summary>
        /// Agrega el cuerpo fisico del hechizo. <paramref name="spellGo"/> es el objeto del hechizo (lleva SpellManifestation y MagicField):
        /// los modulos pueden colgarle componentes o crear objetos propios en la escena.
        /// </summary>
        public static void Spawn(SpellIR spell, SealFrame frame, ElementLook look, DrawingCanvas source, GameObject spellGo, bool visualOnly = false)
        {
            switch (spell.element)
            {
                case ElementType.Earth:
                    // El cristal es un efecto de una sola vez sobre el mundo y corre en todas las maquinas; la roca es un cuerpo nuevo:
                    // la crea el anfitrion (y se replica), un cliente solo ve el efecto.
                    if (spell.sigilId == SpellOutputController.CrystalSigil) CrystalSpell.Spawn(spell, frame, look, source, spellGo);
                    else if (!visualOnly) RockSpell.Spawn(spell, frame, look, source, spellGo);
                    break;
                case ElementType.Water:
                    WaterSpell.Spawn(spell, frame, look, source, spellGo);
                    break;
                case ElementType.Wind:
                    WindSpell.Spawn(spell, frame, look, source, spellGo);
                    break;
            }
        }
    }
}
