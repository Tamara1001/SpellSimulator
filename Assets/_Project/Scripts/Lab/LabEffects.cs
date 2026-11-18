using UnityEngine;
using SpellSimulator.Net;
using SpellSimulator.Output;
using SpellSimulator.Player;
using SpellSimulator.Reactions;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// «Limpiar efectos» del laboratorio: despawnea lo que dejaron los hechizos (manifestaciones, rocas, fragmentos, marcas
    /// en el suelo y fuego de pasto) sin tocar lienzos, cajas, papel ni parametros. Es un reinicio parcial de
    /// <see cref="MagicWorld.ResetWorld"/>; en red no hace nada (el anfitrion es el dueño de los efectos).
    /// </summary>
    public static class LabEffects
    {
        public static void Clear()
        {
            if (NetSession.Exists)
            {
                Hud.Notify("lab", "Laboratorio: solo en modo Libre", 2.5f);
                return;
            }
            var output = Object.FindFirstObjectByType<SpellOutputController>();
            if (output != null) output.ClearAll();
            Shatter.ClearAll();
            GroundMarks.ClearAll();
            if (GrassFire.Current != null) GrassFire.Current.ResetAll();
            Hud.Notify("lab", "Laboratorio: efectos limpiados", 2f);
        }
    }
}
