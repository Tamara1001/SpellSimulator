using System;
using System.IO;
using UnityEngine;

namespace SpellSimulator.Learning
{
    /// <summary>
    /// Archivo de los hechizos propios (hechizos.json en persistentDataPath, junto a descubrimientos.json). Escritura atomica (.tmp y reemplazo); si el archivo
    /// esta corrupto se renombra a .bak, se avisa y se arranca vacio (ADR-034).
    /// </summary>
    public static class SpellLibraryStore
    {
        public const string FileName = "hechizos.json";

        public static string PathOnDisk => Path.Combine(Application.persistentDataPath, FileName);

        public static SpellLibrary Load(Action<string> onError = null)
        {
            string path = PathOnDisk;
            try
            {
                if (!File.Exists(path)) return new SpellLibrary();
                return SpellLibrary.FromJson(File.ReadAllText(path), onError);
            }
            catch (Exception e)
            {
                onError?.Invoke("hechizos.json corrupto, se usa una biblioteca vacia: " + e.Message);
                try { File.Move(path, path + ".bak"); } catch { /* ya existia un .bak: se deja */ }
                return new SpellLibrary();
            }
        }

        public static bool Save(SpellLibrary library, Action<string> onError = null)
        {
            string path = PathOnDisk, tmp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(tmp, library.ToJson());
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                return true;
            }
            catch (Exception e)
            {
                onError?.Invoke("No se pudo guardar hechizos.json: " + e.Message);
                return false;
            }
        }
    }
}
