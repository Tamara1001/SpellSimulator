using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Corre los tests EditMode y vuelca un resumen a Logs/harness-editmode.txt.
    /// Pensado para que un agente (via Unity MCP) o un humano verifique cambios sin abrir el Test Runner.
    /// </summary>
    public static class HarnessTestRunner
    {
        public const string ResultPath = "Logs/harness-editmode.txt";

        [MenuItem("SpellSimulator/Harness/Run EditMode Tests")]
        public static void RunEditMode()
        {
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();   // sin cartel de guardar escena en el Test Runner
            File.WriteAllText(ResultPath, "RUNNING\n");
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new ResultWriter(api));
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = new[] { "SpellSimulator.Tests.EditMode" }
            }));
        }

        private class ResultWriter : ICallbacks
        {
            private readonly TestRunnerApi api;
            private readonly StringBuilder failures = new StringBuilder();

            public ResultWriter(TestRunnerApi api) { this.api = api; }

            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.HasChildren && result.TestStatus == TestStatus.Failed)
                    failures.AppendLine($"FAIL {result.FullName}\n  {result.Message}");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                string summary =
                    $"{(result.FailCount == 0 ? "PASS" : "FAIL")} " +
                    $"passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount} " +
                    $"duration={result.Duration:F2}s\n{failures}";
                File.WriteAllText(ResultPath, summary);
                Debug.Log($"[Harness] EditMode: {summary}");
                api.UnregisterCallbacks(this);
            }
        }
    }
}
