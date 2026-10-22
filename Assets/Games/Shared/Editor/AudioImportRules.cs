using UnityEditor;
using UnityEngine;

namespace Tileworlds.Games.Editor
{
    // Import settings for game audio, so a new clip needs no hand setup: short effects load decompressed in mono, music
    // streams compressed.
    public sealed class AudioImportRules : AssetPostprocessor
    {
        private const string Music = "Assets/Games/Shared/Audio/Music/";
        private static readonly string[] Effects = { "Assets/Games/Shared/Audio/", "Assets/ThirdParty/Kenney/Audio/" };

        public override uint GetVersion() => 1;

        private void OnPreprocessAudio()
        {
            var importer = (AudioImporter)assetImporter;
            if (assetPath.StartsWith(Music))
            {
                importer.forceToMono = false;
                importer.loadInBackground = true;
                importer.defaultSampleSettings = new AudioImporterSampleSettings
                {
                    loadType = AudioClipLoadType.Streaming,
                    compressionFormat = AudioCompressionFormat.Vorbis,
                    quality = 0.4f,
                    sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate,
                    preloadAudioData = false,
                };
                return;
            }

            foreach (var root in Effects)
            {
                if (!assetPath.StartsWith(root)) continue;
                importer.forceToMono = true;
                importer.defaultSampleSettings = new AudioImporterSampleSettings
                {
                    loadType = AudioClipLoadType.DecompressOnLoad,
                    compressionFormat = AudioCompressionFormat.Vorbis,
                    quality = 0.7f,
                    sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate,
                    preloadAudioData = true,
                };
                return;
            }
        }
    }
}
