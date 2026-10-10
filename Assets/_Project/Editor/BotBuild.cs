using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Build de Windows para los bots de prueba de red (ver NetBot). Menu: SpellSimulator/Build/Windows (bots).
    /// Salida: Builds/Bot/SpellSimulator.exe. Resultado en Logs/build-bot.txt. Se rehace cuando cambia el codigo de juego o de red.
    /// Se dispara desde EditorApplication.update (no delayCall: este no corre si Unity esta en segundo plano).
    /// </summary>
    public static class BotBuild
    {
        public static readonly string OutputDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Bot"));
        public static readonly string ExePath = Path.Combine(OutputDir, "SpellSimulator.exe");
        private static readonly string ResultPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "build-bot.txt"));

        [MenuItem("SpellSimulator/Build/Windows (bots)")]
        public static void Run()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.WriteAllText(ResultPath, "RUNNING");
            EditorApplication.update += Once;
        }

        private static void Once()
        {
            EditorApplication.update -= Once;
            Directory.CreateDirectory(OutputDir);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Project/Scenes/Pradera.unity" },
                locationPathName = ExePath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.Development
            };
            var report = BuildPipeline.BuildPlayer(options);
            var sb = new StringBuilder();
            sb.AppendLine(report.summary.result == BuildResult.Succeeded ? "PASS" : "FAIL " + report.summary.result);
            sb.AppendLine($"tamano={report.summary.totalSize / (1024 * 1024)} MB tiempo={report.summary.totalTime.TotalSeconds:0}s errores={report.summary.totalErrors}");
            sb.AppendLine("salida=" + ExePath);
            File.WriteAllText(ResultPath, sb.ToString());
            Debug.Log("[BotBuild] " + sb);
        }
    }
}
