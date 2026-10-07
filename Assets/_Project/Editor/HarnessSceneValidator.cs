using System.Text;
using UnityEditor;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Casting;
using SpellSimulator.Recognition;
using SpellSimulator.Output;
using SpellSimulator.Player;
using SpellSimulator.World;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Chequea que la escena abierta tenga las piezas del pipeline cableadas.
    /// Revisa referencias serializadas nulas en los componentes del proyecto.
    /// </summary>
    public static class HarnessSceneValidator
    {
        [MenuItem("SpellSimulator/Harness/Validate Open Scene")]
        public static string Validate()
        {
            var report = new StringBuilder();
            int problems = 0;

            problems += RequireSingle<SentisGlyphClassifier>(report);
            problems += RequireSingle<SpellCaster>(report);
            problems += RequireSingle<SpellOutputController>(report);
            problems += RequireSingle<PlatformDetector>(report);
            problems += RequireAtLeastOne<DrawingCanvas>(report);
            problems += RequireAtLeastOne<PhysicalTool>(report);

            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb == null || mb.GetType().Namespace == null || !mb.GetType().Namespace.StartsWith("SpellSimulator"))
                    continue;

                var so = new SerializedObject(mb);
                var prop = so.GetIterator();
                while (prop.NextVisible(true))
                {
                    if (prop.propertyType == SerializedPropertyType.ObjectReference && prop.objectReferenceValue == null)
                    {
                        report.AppendLine($"WARN null ref: {mb.gameObject.name}/{mb.GetType().Name}.{prop.propertyPath}");
                    }
                }
            }

            string result = $"{(problems == 0 ? "PASS" : "FAIL")} problemas={problems}\n{report}";
            Debug.Log($"[Harness] Scene: {result}");
            return result;
        }

        private static int RequireSingle<T>(StringBuilder report) where T : Object
        {
            int count = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            if (count == 1) return 0;
            report.AppendLine($"ERROR se esperaba 1 {typeof(T).Name}, hay {count}");
            return 1;
        }

        private static int RequireAtLeastOne<T>(StringBuilder report) where T : Object
        {
            if (Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0) return 0;
            report.AppendLine($"ERROR falta al menos un {typeof(T).Name}");
            return 1;
        }
    }
}
