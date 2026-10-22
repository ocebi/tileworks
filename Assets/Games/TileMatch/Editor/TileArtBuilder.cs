using System.Linq;
using Tileworlds.Games.Editor;
using UnityEditor;
using UnityEngine;

namespace TileMatch.Editor
{
    // Import settings for the tile base, overlays and tray (drawn by Tools/art/tilematch), and the board atlas that batches
    // them with the animal icons.
    public static class TileArtBuilder
    {
        public const string Folder = "Assets/Games/TileMatch/Art/Tiles/Base";
        public const string BoardAtlas = "Assets/Games/TileMatch/Art/TileBoard.spriteatlasv2";
        public const int Width = 288;
        public const int Height = 320;
        public const int Face = 288;
        private const int TrayBorder = 40;

        [MenuItem("Tileworks/Tile Match/Import Tile Art")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            // The face sprite's pivot sits in the middle of its top face, above the 32 px side.
            var facePivot = (Height - Face * 0.5f) / Height;
            Import("tile_face", Width, facePivot);
            Import("tile_shadow", Width, facePivot);
            foreach (var name in new[] { "tile_hidden", "tile_ice_1", "tile_ice_2", "tile_link" }) Import(name, Face, 0.5f);
            ArtImport.Sprite($"{Folder}/tray.png", 128, 0.5f, Vector4.one * TrayBorder);
        }

        public static void BuildAtlas(Sprite[] icons)
        {
            var bases = new[] { "tile_face", "tile_shadow", "tile_hidden", "tile_ice_1", "tile_ice_2", "tile_link", "tray" }
                .Select(name => AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/{name}.png"));
            ArtImport.Atlas(BoardAtlas, bases.Concat(icons).ToArray());
        }

        private static void Import(string name, float pixelsPerUnit, float pivotY) => ArtImport.Sprite($"{Folder}/{name}.png", pixelsPerUnit, pivotY);
    }
}
