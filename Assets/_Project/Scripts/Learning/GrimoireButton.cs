using System;
using UnityEngine;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Learning
{
    /// <summary>Boton del grimorio que se aprieta (opciones de menu, glifos, volver...). La accion la configura el grimorio al cambiar de pantalla.</summary>
    public class GrimoireButton : MonoBehaviour, IPressable
    {
        private string prompt;
        private Action action;

        public void Configure(string text, Action onPress)
        {
            prompt = text;
            action = onPress;
        }

        public string GetPressPrompt() => prompt ?? "";

        public void Press() => action?.Invoke();
    }
}
