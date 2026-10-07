using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Chequeos de configuracion del proyecto (reemplaza CheckURP y VFXDiagnostics). Solo reporta: no cambia nada.
    /// </summary>
    public static class HarnessProjectChecks
    {
        [MenuItem("SpellSimulator/Harness/Check Project Settings")]
        public static string Run()
        {
            var report = new StringBuilder();
            int problems = 0;

            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null)
            {
                report.AppendLine("ERROR no hay Render Pipeline Asset asignado (se requiere URP).");
                problems++;
            }
            else report.AppendLine($"OK SRP: {pipeline.name}");

            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                report.AppendLine($"ERROR VFX Graph requiere Color Space Linear (actual: {PlayerSettings.colorSpace}).");
                problems++;
            }
            else report.AppendLine("OK Color Space Linear");

            string result = $"{(problems == 0 ? "PASS" : "FAIL")} problemas={problems}\n{report}";
            Debug.Log($"[Harness] Project: {result}");
            return result;
        }
    }
}
