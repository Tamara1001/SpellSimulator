using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Build de WebGL para itch.io. Menu: SpellSimulator/Build/WebGL (itch.io). Por MCP: WebGLBuild.Run().
    /// Requiere que la ventana de Unity tenga el foco: en segundo plano el editor no ejecuta delayCall y el build no arranca.
    /// Salida: Builds/WebGL (index.html en la raiz, listo para comprimir en un .zip). Resultado en Logs/build-webgl.txt.
    /// </summary>
    public static class WebGLBuild
    {
        private static readonly string ResultPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "build-webgl.txt"));
        private static readonly string OutputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "WebGL"));

        private static bool development;

        /// <summary>Build de diagnostico: Development Build (errores de shaders y logs en la consola del navegador) y compilacion rapida.</summary>
        [MenuItem("SpellSimulator/Build/WebGL (diagnostico)")]
        public static void RunDevelopment()
        {
            development = true;
            Run();
        }

        [MenuItem("SpellSimulator/Build/WebGL (itch.io)")]
        public static void RunRelease()
        {
            development = false;
            Run();
        }

        public static void Run()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.WriteAllText(ResultPath, "RUNNING");
            // BuildPlayer no puede llamarse dentro del loop del player: se difiere un tick.
            EditorApplication.delayCall += Build;
        }

        private static void Build()
        {
            // Gzip: itch.io lo sirve con el Content-Encoding correcto sin configurar nada. Sin hilos (no hay SharedArrayBuffer en itch).
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.WebGL.exceptionSupport = development ? WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly : WebGLExceptionSupport.None;
            PlayerSettings.WebGL.initialMemorySize = 256;
            PlayerSettings.WebGL.maximumMemorySize = 1024;
            PlayerSettings.WebGL.dataCaching = true;

            Directory.CreateDirectory(OutputPath);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Project/Scenes/Pradera.unity" },
                locationPathName = OutputPath,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = development ? BuildOptions.Development : BuildOptions.None
            };
            UnityEditor.WebGL.UserBuildSettings.codeOptimization = development ? UnityEditor.WebGL.WasmCodeOptimization.BuildTimes : UnityEditor.WebGL.WasmCodeOptimization.RuntimeSpeed;
            var report = BuildPipeline.BuildPlayer(options);
            var sb = new StringBuilder();
            sb.AppendLine(report.summary.result == BuildResult.Succeeded ? "PASS" : "FAIL " + report.summary.result);
            sb.AppendLine($"tamano={report.summary.totalSize / (1024 * 1024)} MB tiempo={report.summary.totalTime.TotalSeconds:0}s errores={report.summary.totalErrors} avisos={report.summary.totalWarnings}");
            sb.AppendLine("salida=" + OutputPath);
            File.WriteAllText(ResultPath, sb.ToString());
            Debug.Log("[WebGLBuild] " + sb);
        }
    }
}
