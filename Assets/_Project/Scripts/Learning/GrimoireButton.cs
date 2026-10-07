using System;
using UnityEngine;
using SpellSimulator.Interfaces;

namespace SpellSimulator.Learning
{
    /// <summary>Zona del grimorio que se aprieta (pasar pagina, reiniciar...). La accion la configura el grimorio.</summary>
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
