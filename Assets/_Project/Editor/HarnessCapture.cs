using System.IO;
using UnityEngine;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Renderiza la escena desde un punto a un PNG (para verificar efectos sin depender de la Game View).
    /// Funciona en Play Mode y en edicion. Pensado para Unity MCP: Capture(ruta, posicion, objetivo).
    /// </summary>
    public static class HarnessCapture
    {
        public static string Capture(string path, Vector3 position, Vector3 lookAt, int width = 960, int height = 540, float fov = 60f)
        {
            var go = new GameObject("HarnessCaptureCam");
            var rt = new RenderTexture(width, height, 24);
            try
            {
                var cam = go.AddComponent<Camera>();
                go.transform.position = position;
                go.transform.LookAt(lookAt);
                cam.fieldOfView = fov;
                cam.targetTexture = rt;
                cam.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = previous;

                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                return path;
            }
            finally
            {
                var cam = go.GetComponent<Camera>();
                if (cam != null) cam.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(go);
            }
        }
    }
}
