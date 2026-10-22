using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace Blast.Editor
{
    // Import settings for the design's piece sheet: one sprite per piece, one board cell per sprite.
    public static class BlastArtBuilder
    {
        public const string Folder = "Assets/Games/Blast/Art/Design";
        public const string UiFolder = Folder + "/UI";
        public const string BoardShapePath = "Assets/Games/Blast/Art/Board/board_shape.png";
        private const int UiPixelsPerUnit = 100;
        private const int PixelsPerUnit = 128;
        private const int FrameBorder = 32;

        // The hammer turns about its handle end.
        private static readonly Vector2 HammerPivot = new Vector2(0.203f, 0.134f);

        public static readonly string[] ColourNames = { "1_red", "2_blue", "3_green", "4_yellow", "5_purple", "6_teal" };

        // Effect tints matching the cube colours.
        public static readonly Color[] Colours =
        {
            new Color32(235, 72, 70, 255), new Color32(60, 125, 235, 255), new Color32(60, 185, 90, 255), new Color32(250, 195, 50, 255),
            new Color32(160, 90, 225, 255), new Color32(30, 170, 185, 255),
        };

        [MenuItem("Tileworks/Blast/Import Art")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            foreach (var path in Directory.GetFiles(Folder, "*.png").Append(BoardShapePath))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var border = path == BoardShapePath || Path.GetFileNameWithoutExtension(path) == "board_frame" ? FrameBorder : 0;
                var pivot = Path.GetFileNameWithoutExtension(path) == "fx_hammer" ? HammerPivot : new Vector2(0.5f, 0.5f);
                if (importer.textureType == TextureImporterType.Sprite && importer.spritePixelsPerUnit == PixelsPerUnit && importer.spriteBorder.x == border &&
                    importer.spritePivot == pivot) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = PixelsPerUnit;
                importer.spriteBorder = Vector4.one * border;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)(pivot == new Vector2(0.5f, 0.5f) ? SpriteAlignment.Center : SpriteAlignment.Custom);
                settings.spritePivot = pivot;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }

        // Every board sprite in one texture, so pieces, cells and effects batch together.
        public const string BoardAtlas = "Assets/Games/Blast/Art/BlastBoard.spriteatlasv2";

        public static void BuildAtlas() => Pack(BoardAtlas, Folder, BoardShapePath);

        public static Sprite BoardShape() => AssetDatabase.LoadAssetAtPath<Sprite>(BoardShapePath);

        private static void Pack(string atlasPath, string folder, params string[] extra)
        {
            if (EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2) EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
            var paths = Directory.GetFiles(folder, "*.png").OrderBy(p => p).Concat(extra);
            var sprites = paths.Select(p => (Object)AssetDatabase.LoadAssetAtPath<Sprite>(p)).Where(s => s != null).ToArray();
            var atlas = new SpriteAtlasAsset();
            atlas.Add(sprites);
            SpriteAtlasAsset.Save(atlas, atlasPath);
            AssetDatabase.ImportAsset(atlasPath);
            var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(atlasPath);
            importer.includeInBuild = true;
            importer.packingSettings = new SpriteAtlasPackingSettings { enableRotation = false, enableTightPacking = false, padding = 4 };
            importer.textureSettings = new SpriteAtlasTextureSettings { filterMode = FilterMode.Bilinear, generateMipMaps = false, sRGB = true };
            // The default mobile compression bands the gradients and streaks stretched panels.
            foreach (var platform in new[] { "Android", "iPhone" })
                importer.SetPlatformSettings(new TextureImporterPlatformSettings
                    { name = platform, overridden = true, format = TextureImporterFormat.ASTC_4x4, maxTextureSize = 2048 });
            importer.SaveAndReimport();
        }

        // The UI kit is drawn for the 1080-wide canvas: one pixel is one canvas unit. Borders are left, bottom, right, top.
        private static readonly (string Prefix, Vector4 Border)[] UiBorders =
        {
            ("ui_panel", new Vector4(103, 128, 103, 103)), ("ui_button_", new Vector4(76, 92, 76, 76)), ("ui_card", new Vector4(67, 78, 67, 67)),
            ("ui_pill", new Vector4(72, 72, 72, 72)), ("ui_tile", new Vector4(67, 81, 67, 67)), ("ui_chip", new Vector4(48, 48, 48, 48)),
            ("ui_ribbon", new Vector4(59, 59, 59, 59)),
        };

        public const string UiAtlas = "Assets/Games/Blast/Art/BlastUI.spriteatlasv2";

        public static void ImportUi()
        {
            AssetDatabase.Refresh();
            foreach (var path in Directory.GetFiles(UiFolder, "*.png"))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                var border = UiBorders.FirstOrDefault(b => name.StartsWith(b.Prefix)).Border;
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer.textureType == TextureImporterType.Sprite && importer.spritePixelsPerUnit == UiPixelsPerUnit && importer.spriteBorder == border) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = UiPixelsPerUnit;
                importer.spriteBorder = border;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            Pack(UiAtlas, UiFolder);
        }

        public static Sprite LoadUi(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{UiFolder}/{name}.png");

        public static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/{name}.png");

        public static Sprite[] ByColour(string prefix)
        {
            var sprites = new Sprite[ColourNames.Length];
            for (var i = 0; i < sprites.Length; i++) sprites[i] = Load($"{prefix}_{ColourNames[i]}");
            return sprites;
        }
    }
}
