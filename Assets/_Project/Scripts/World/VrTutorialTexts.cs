using System.Collections.Generic;
using TMPro;
using UnityEngine;
using SpellSimulator.Player;

namespace SpellSimulator.World
{
    /// <summary>
    /// [VR] Los carteles del tutorial se hornean con los controles de teclado y mouse. En VR se les cambia el texto por los de los
    /// controles Touch (misma regla: 2 lineas cortas). Se aplica una sola vez, al empezar el tutorial. La tabla se indexa por el
    /// nombre del objeto del cartel (<c>Note_Tutorial_*</c>, ver Editor/Map/MapTutorial.cs); si cambian los controles de VR,
    /// actualizar tambien Docs/GAME_DESIGN.md y Learning/GrimoireTexts.cs.
    /// </summary>
    public static class VrTutorialTexts
    {
        private static readonly Dictionary<string, string> Texts = new Dictionary<string, string>
        {
            { "Note_Tutorial_Skip", "<b>Grip</b> · Saltear tutorial\nIr directo al Nexo" },
            { "Note_Tutorial_Move", "<b>Stick izq.</b> · Caminar\n<b>Stick der.</b> · Girar" },
            { "Note_Tutorial_Crouch", "<b>Clic stick izq.</b> · Agacharse\nO pasá por el hueco del medio" },
            { "Note_Tutorial_Jump", "<b>A</b> / <b>X</b> · Saltar\n(el botón de la mano libre)" },
            { "Note_Tutorial_Practice", "<b>Grip</b> · Agarrar y soltar\nProbá con lo que haya" },
            { "Note_Tutorial_Pile", "<b>Grip</b> · Mover las cajas\nDespejá la bajada" },
            { "Note_Tutorial_Float", "<b>1</b> · Lienzo en el aire\nSoltá el <b>grip</b> sin mover la mano" },
            { "Note_Tutorial_Upright", "<b>2</b> · Lienzo vertical\nGirá la muñeca y soltá" },
            { "Note_Tutorial_Target", "<b>3</b> · Tirá una caja\nSoltá con un envión a la diana" },
            { "Note_Tutorial_Ink", "<b>Grip</b> · Tomá la pluma\n<b>Y</b> · Cambiar tinta" },
            { "Note_Tutorial_Book", "<b>Grip</b> · Leé el Tomo II\nEn la mesa: es la tierra" },
            { "Note_Tutorial_Cloth", "Calcá la tierra marrón\n<b>Gatillo</b> y apuntá a la puerta" },
        };

        private static bool applied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => applied = false;

        /// <summary>Cambia los textos de los carteles por los de VR. No hace nada en PC ni si ya se aplico.</summary>
        public static void Apply()
        {
            if (applied || !PlatformDetector.IsVR) return;
            applied = true;
            var labels = Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < labels.Length; i++)
            {
                Transform note = labels[i].transform.parent;
                if (note != null && Texts.TryGetValue(note.name, out string text)) labels[i].text = text;
            }
        }
    }
}
