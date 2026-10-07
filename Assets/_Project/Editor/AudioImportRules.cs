using UnityEditor;
using UnityEngine;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Importacion de audio del proyecto (Assets/_Project/Audio): Vorbis mono; la musica en streaming, los loops
    /// comprimidos en memoria y los efectos cortos descomprimidos al cargar (latencia minima). Pensado para Quest.
    /// </summary>
    public class AudioImportRules : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            if (!assetPath.Replace('\\', '/').Contains("/_Project/Audio/")) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = true;
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            var settings = importer.defaultSampleSettings;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            if (name.Contains("music"))
            {
                settings.loadType = AudioClipLoadType.Streaming;
                settings.quality = 0.5f;
            }
            else if (name.Contains("loop"))
            {
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.quality = 0.5f;
            }
            else
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.quality = 0.7f;
            }
            importer.defaultSampleSettings = settings;
        }
    }
}
