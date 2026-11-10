using System.IO;
using UnityEditor;
using UnityEngine;

namespace Tileworlds.Games.Editor
{
    // Import settings for the big pictures outside the sprite atlases, so they stay small in the build: backdrops crunched at
    // full size, characters and hub cards compressed, the card glow tiny and scaled up.
    public sealed class TextureImportRules : AssetPostprocessor
    {
        private const string Games = "Assets/Games/";
        private static readonly string[] Phones = { "Android", "iPhone" };

        public override uint GetVersion() => 2;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Games)) return;
            var importer = (TextureImporter)assetImporter;
            var name = Path.GetFileName(assetPath);
            if (assetPath.Contains("/Backgrounds/")) Backdrop(importer);
            else if (name.StartsWith("hero_")) Compressed(importer, 512);
            else if (name == "hub_card_glow.png") Compressed(importer, 128);
            else if (name.StartsWith("hub_")) Compressed(importer, 512);
        }

        // Opaque and soft, so coarse ASTC blocks keep it under 300 KB on the web (the WebGL build uses the ASTC subtarget; a DXT
        // override there imports uncompressed); high-quality ASTC on phones keeps the gradients clean.
        private static void Backdrop(TextureImporter importer)
        {
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = false;
            foreach (var platform in Phones)
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                    { name = platform, overridden = true, format = TextureImporterFormat.ASTC_4x4, maxTextureSize = 2048 });
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                { name = "WebGL", overridden = true, format = TextureImporterFormat.ASTC_10x10, maxTextureSize = 2048 });
        }

        private static void Compressed(TextureImporter importer, int maxSize)
        {
            importer.mipmapEnabled = false;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = false;
        }
    }
}
