using System.IO;
using System.Linq;
using Tileworlds.Games.Editor;
using UnityEditor;
using UnityEngine;

namespace Match3.Editor
{
    // Import settings for the design's match-3 art: one sprite per piece, one board cell per sprite. The balloon, the duck
    // and the effects are Blast's and stay in Blast's atlas.
    public static class Match3ArtBuilder
    {
        public const string Folder = "Assets/Games/Match3/Art/Design";
        public const string UiFolder = Folder + "/UI";
        public const string BoardAtlas = "Assets/Games/Match3/Art/Match3Board.spriteatlasv2";
        private const string Shared = "Assets/Games/Blast/Art/Design";
        private const int PixelsPerUnit = 128;
        private const int FrameBorder = 32;

        public static readonly string[] Gems = { "gem_1_red", "gem_2_blue", "gem_3_green", "gem_4_yellow", "gem_5_purple", "gem_6_teal" };

        // Effect tints matching the gem colours.
        public static readonly Color[] Colours =
        {
            new Color32(235, 72, 70, 255), new Color32(60, 125, 235, 255), new Color32(60, 185, 90, 255), new Color32(250, 195, 50, 255),
            new Color32(160, 90, 225, 255), new Color32(30, 170, 185, 255),
        };

        [MenuItem("Tileworks/Match3/Import Art")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            var board = Directory.GetFiles(Folder, "*.png").OrderBy(p => p).Select(path =>
            {
                var border = Path.GetFileNameWithoutExtension(path) is "board_rim" or "board_fill" ? FrameBorder : 0;
                return ArtImport.Sprite(path, PixelsPerUnit, border: Vector4.one * border);
            });
            ArtImport.Atlas(BoardAtlas, board.ToArray());
            foreach (var path in Directory.GetFiles(UiFolder, "*.png")) ArtImport.Sprite(path, 100);
        }

        public static Sprite Load(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/{name}.png") ?? AssetDatabase.LoadAssetAtPath<Sprite>($"{Shared}/{name}.png");

        public static Sprite LoadUi(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{UiFolder}/{name}.png");

        public static Sprite[] ByColour() => Gems.Select(Load).ToArray();
    }
}
