using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace Tileworlds.Games.Editor
{
    // Import settings for the generated art (drawn by Tools/art) and the sprite atlases that batch it.
    public static class ArtImport
    {
        // Sets sprite import settings when they differ, so repeated builds don't reimport.
        public static Sprite Sprite(string path, float pixelsPerUnit, float pivotY = 0.5f, Vector4 border = default)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                Debug.LogWarning($"[Tileworks] Missing art {path}");
                return null;
            }

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            var pivot = new Vector2(0.5f, pivotY);
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit) || settings.spritePivot != pivot || settings.spriteBorder != border ||
                importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = pixelsPerUnit;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteBorder = border;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // One atlas per screen layer, so its sprites draw in a single batch. High-quality ASTC on phones keeps soft gradients clean.
        public static void Atlas(string path, params Sprite[] sprites)
        {
            if (EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2) EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
            var atlas = new SpriteAtlasAsset();
            atlas.Add(sprites.Where(s => s != null).Cast<Object>().ToArray());
            SpriteAtlasAsset.Save(atlas, path);
            AssetDatabase.ImportAsset(path);
            var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(path);
            importer.includeInBuild = true;
            importer.packingSettings = new SpriteAtlasPackingSettings { enableRotation = false, enableTightPacking = false, padding = 4 };
            importer.textureSettings = new SpriteAtlasTextureSettings { filterMode = FilterMode.Bilinear, generateMipMaps = false, sRGB = true };
            foreach (var platform in new[] { "Android", "iPhone" })
                importer.SetPlatformSettings(new TextureImporterPlatformSettings
                    { name = platform, overridden = true, format = TextureImporterFormat.ASTC_4x4, maxTextureSize = 2048 });
            importer.SaveAndReimport();
        }
    }
}
