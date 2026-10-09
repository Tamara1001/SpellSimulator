using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Compiler;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Punto unico donde cada elemento agrega sus cuerpos fisicos propios al lanzar un hechizo (bloque 3 del plan):
    /// roca (tierra), cristal, agua y viento. Se llama desde <see cref="SpellOutputController.SpawnSpellEffect"/> despues de crear
    /// la manifestacion visual (particulas) y su <see cref="MagicField"/>. Cada modulo vive en su propio archivo.
    /// </summary>
    public static class SpellBodies
    {
        /// <summary>
        /// Agrega el cuerpo fisico del hechizo. <paramref name="spellGo"/> es el objeto del hechizo (lleva SpellManifestation y MagicField):
        /// los modulos pueden colgarle componentes o crear objetos propios en la escena.
        /// </summary>
        public static void Spawn(SpellIR spell, SealFrame frame, ElementLook look, DrawingCanvas source, GameObject spellGo)
        {
            switch (spell.element)
            {
                case ElementType.Earth:
                    if (spell.sigilId == SpellOutputController.CrystalSigil) CrystalSpell.Spawn(spell, frame, look, source, spellGo);
                    else RockSpell.Spawn(spell, frame, look, source, spellGo);
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
