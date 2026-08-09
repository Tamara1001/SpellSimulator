using System;
using UnityEngine;

namespace SpellSimulator.DebugTools
{
    /// <summary>
    /// Bus de eventos estatico para comunicar el pipeline de inferencia y el compilador
    /// con la UI de diagnostico, sin generar acoplamiento de dependencias.
    /// </summary>
    public static class DiagnosticBus
    {
        /// <summary>
        /// Se emite cuando el motor de inferencia recorta y prepara el tensor de 96x96.
        /// Recibe una Texture2D con la imagen que se envio a la red neuronal.
        /// </summary>
        public static Action<Texture2D> OnTensorImageUpdated;

        /// <summary>
        /// Se emite cuando el motor de inferencia obtiene las predicciones.
        /// Envía el texto con el log del top 3.
        /// </summary>
        public static Action<string> OnPredictionUpdated;

        /// <summary>
        /// Se emite cuando el compilador finaliza de armar la estructura del hechizo.
        /// Envia un resumen detallado con metricas del hechizo.
        /// </summary>
        public static Action<string> OnDiagnosticsUpdated;
    }
}
