using UnityEngine;
using System.Collections.Generic;

namespace SpellSimulator.Compiler
{
    /// <summary>
    /// Módulo 4: Intérprete Lógico (Compilador).
    /// Convierte las predicciones brutas del motor de inferencia en directivas
    /// estructuradas (SpellDescriptor) apoyándose en el diccionario JSON del modelo.
    /// </summary>
    public class SpellCompiler : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField, Tooltip("Archivo JSON con el diccionario (ej. glyph-class-to-idx.json)")]
        private TextAsset dictionaryJson;

        // Mapa de memoria (Indice -> Nombre del Glifo)
        private Dictionary<int, string> indexToClassName = new Dictionary<int, string>();

        private void Awake()
        {
            LoadDictionary();
        }

        /// <summary>
        /// Lee el archivo JSON plano y lo carga en el diccionario en memoria.
        /// </summary>
        private void LoadDictionary()
        {
            if (dictionaryJson == null)
            {
                Debug.LogWarning("SpellCompiler: No se asignó el diccionario JSON en el inspector.");
                return;
            }

            // Parseador liviano para JSON plano tipo "key": index
            string json = dictionaryJson.text;
            json = json.Replace("{", "").Replace("}", "").Replace("\"", "").Replace(" ", "").Replace("\r", "").Replace("\n", "");
            
            string[] pairs = json.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
            
            foreach (string p in pairs)
            {
                string[] kv = p.Split(':');
                if (kv.Length == 2)
                {
                    string className = kv[0];
                    if (int.TryParse(kv[1], out int index))
                    {
                        indexToClassName[index] = className;
                    }
                }
            }
            Debug.Log($"[Compilador] Diccionario cargado con {indexToClassName.Count} glifos mapeados.");
        }

        /// <summary>
        /// Compila las reglas lógicas basándose en el output de la IA y el diccionario.
        /// </summary>
        public SpellDescriptor Compile(int recognizedClassIndex, bool isRingClosed)
        {
            ElementType element = ElementType.None;
            float scale = 1.0f;
            string glyphName = "Desconocido";

            // Buscar en el diccionario
            if (indexToClassName.TryGetValue(recognizedClassIndex, out glyphName))
            {
                // Traducir el glifo a un elemento o modificador mecánico
                switch (glyphName.ToLower())
                {
                    case "fire":
                        element = ElementType.Fire;
                        break;
                    case "aeroform":
                        element = ElementType.Air;
                        break;
                    case "water":
                        element = ElementType.Water;
                        break;
                    case "earth":
                        element = ElementType.Earth;
                        break;
                    case "empower":
                        // Si el glifo es empower, aplicamos un multiplicador de escala.
                        // Como aún no tenemos sistema de apilamiento en el MVP, 
                        // se pasa como modificador puro.
                        scale = 2.0f; 
                        break;
                    default:
                        // Si es cualquier otro glifo (crush, crystal, focus, etc.)
                        // no le asignamos elemento primario todavía, pero el sistema lo reconoce.
                        break;
                }
            }
            else
            {
                Debug.LogWarning($"[Compilador] El índice de clase {recognizedClassIndex} no existe en el diccionario JSON.");
            }

            // Empaquetar en el struct (0 GC)
            SpellDescriptor descriptor = new SpellDescriptor(element, scale, isRingClosed);

            // Resumen en consola mostrando el Glifo detectado
            Debug.Log($"[Compilador] Hechizo Formado -> Glifo: '{glyphName}' | Elemento: {descriptor.primaryElement} | Modificador Escala: {descriptor.scaleMultiplier}x | Activo (Sellado): {descriptor.isSignedAndActive}");

            return descriptor;
        }
    }
}
