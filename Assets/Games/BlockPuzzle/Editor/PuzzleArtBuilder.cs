using Tileworlds.Games.Editor;
using UnityEditor;
using UnityEngine;

namespace BlockPuzzle.Editor
{
    // Import settings for the block, cell, gem and frame (drawn by Tools/art/blockpuzzle) and the board atlas. Blocks, cells
    // and the frame are greyscale so the skin tints them.
    public static class PuzzleArtBuilder
    {
        public const string Folder = "Assets/Games/BlockPuzzle/Art";
        public const string BoardAtlas = Folder + "/PuzzleBoard.spriteatlasv2";
        private const int Size = 128;
        private const int FrameBorder = 48;

        public static readonly Color[] Colours =
        {
            new Color32(235, 77, 75, 255), new Color32(245, 155, 45, 255), new Color32(245, 205, 60, 255), new Color32(95, 200, 90, 255),
            new Color32(70, 200, 225, 255), new Color32(75, 120, 235, 255), new Color32(165, 95, 225, 255),
        };

        [MenuItem("Tileworks/Block Puzzle/Import Art")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            ArtImport.Atlas(BoardAtlas, Import("block"), Import("cell"), Import("gem"), ArtImport.Sprite($"{Folder}/frame.png", Size, 0.5f, Vector4.one * FrameBorder));
        }

        private static Sprite Import(string name) => ArtImport.Sprite($"{Folder}/{name}.png", Size);
    }
}
