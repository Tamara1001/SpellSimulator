using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using SpellSimulator.Player;

namespace SpellSimulator.Lab
{
    /// <summary>
    /// Escritor del registro CSV del laboratorio: un archivo por sesion, en modo append con Flush por fila (un crash a mitad de
    /// sesion no pierde lo anterior). FileShare.ReadWrite para que Excel pueda tenerlo abierto; UTF-8 con BOM al crearlo (acentos
    /// bien en Excel) y encabezado <see cref="LabCsv.Header"/> si el archivo es nuevo. Si escribir falla, sigue guardando las filas
    /// en memoria (<see cref="MemoryLines"/>) y avisa una sola vez. Crea strings: usar solo en eventos, nunca por frame.
    /// </summary>
    public sealed class LabCsvWriter : IDisposable
    {
        /// <summary>Prefijo del archivo de sesion.</summary>
        public const string FilePrefix = "registro-sesion-";
        /// <summary>Formato de fecha del nombre del archivo (AAAAMMDD-HHMM).</summary>
        public const string FileDateFormat = "yyyyMMdd-HHmm";
        /// <summary>Id del aviso de la HUD cuando no se puede escribir.</summary>
        public const string NoticeId = "lab-csv";
        /// <summary>Salto de linea fijo (CRLF) para que Excel lo lea igual en PC y en Quest.</summary>
        public const string LineBreak = "\r\n";

        private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };
        // El BOM se escribe a mano solo si el archivo es nuevo; el StreamWriter no debe agregar otro.
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        private readonly List<string> memory = new List<string>(64);
        private StreamWriter writer;
        private bool memoryOnly;
        private bool warned;

        /// <summary>Ruta completa del archivo (aunque se este guardando solo en memoria).</summary>
        public string Path { get; }

        /// <summary>True si fallo la escritura al disco: desde ahi las filas quedan en <see cref="MemoryLines"/>.</summary>
        public bool Failed { get; private set; }

        /// <summary>True si no se escribe archivo (WebGL o despues de un fallo).</summary>
        public bool MemoryOnly => memoryOnly || Failed;

        /// <summary>Filas guardadas en memoria (con el encabezado primero) cuando no se puede escribir al disco.</summary>
        public IReadOnlyList<string> MemoryLines => memory;

        /// <summary>Abre (o crea) el archivo en <paramref name="path"/>. Se puede redirigir, p. ej. a Logs/ para el smoke test.</summary>
        public LabCsvWriter(string path)
        {
            Path = path;
#if UNITY_WEBGL && !UNITY_EDITOR
            // En WebGL persistentDataPath es IndexedDB: el archivo no es accesible para la usuaria y puede no persistir. Se guarda
            // solo en memoria; exportar (descarga por .jslib) queda como extra (Docs/LABORATORIO_PLAN.md, riesgo 9).
            memoryOnly = true;
            memory.Add(LabCsv.Header);
#else
            Open();
#endif
        }

        /// <summary>Ruta del archivo de esta sesion: persistentDataPath/registro-sesion-AAAAMMDD-HHMM.csv.</summary>
        public static string BuildSessionPath()
        {
            string name = FilePrefix + DateTime.Now.ToString(FileDateFormat, CultureInfo.InvariantCulture) + ".csv";
            return System.IO.Path.Combine(Application.persistentDataPath, name);
        }

        /// <summary>Agrega una fila (sin salto de linea) y la baja al disco enseguida.</summary>
        public void Append(string line)
        {
            if (line == null) return;
            if (MemoryOnly || writer == null)
            {
                memory.Add(line);
                return;
            }
            try
            {
                writer.Write(line);
                writer.Write(LineBreak);
                writer.Flush();
            }
            catch (Exception e)
            {
                Fail(e);
                memory.Add(line);
            }
        }

        public void Dispose()
        {
            CloseQuietly();
        }

        private void Open()
        {
            FileStream stream = null;
            try
            {
                string dir = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                stream = new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                bool isNew = stream.Length == 0;
                if (isNew) stream.Write(Utf8Bom, 0, Utf8Bom.Length);
                writer = new StreamWriter(stream, Utf8NoBom);
                stream = null; // ahora lo cierra el writer
                if (isNew)
                {
                    writer.Write(LabCsv.Header);
                    writer.Write(LineBreak);
                    writer.Flush();
                }
            }
            catch (Exception e)
            {
                if (stream != null)
                {
                    try { stream.Dispose(); } catch { /* ya fallo: se sigue en memoria */ }
                }
                Fail(e);
            }
        }

        private void Fail(Exception e)
        {
            Failed = true;
            CloseQuietly();
            if (memory.Count == 0) memory.Add(LabCsv.Header);
            if (warned) return;
            warned = true;
            Debug.LogWarning("[Laboratorio] No se pudo escribir el registro en " + Path + ": " + e.Message
                + ". Se sigue guardando en memoria.");
            Hud.Notify(NoticeId, "No se pudo guardar el registro del laboratorio (¿está abierto en otro programa?). Se guarda en memoria.", 6f);
        }

        private void CloseQuietly()
        {
            if (writer == null) return;
            try { writer.Dispose(); } catch { /* el archivo ya no responde: nada mas que hacer */ }
            writer = null;
        }
    }
}
