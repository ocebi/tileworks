using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Tileworlds.Games.Editor
{
    // The shared glossy UI kit (buttons, panels, pills, badges, icons), drawn for the 1080-wide canvas: one pixel is one canvas unit.
    public static class KitArt
    {
        public const string Folder = "Assets/Games/Shared/Art/Kit";
        public const string Atlas = "Assets/Games/Shared/Art/Kit.spriteatlasv2";

        // 9-slice borders as left, bottom, right, top; must match Tools/art/blast/make_ui.py.
        private static readonly (string Prefix, Vector4 Border)[] Borders =
        {
            ("ui_panel", new Vector4(103, 128, 103, 103)), ("ui_button_", new Vector4(76, 92, 76, 76)), ("ui_card", new Vector4(67, 78, 67, 67)),
            ("ui_pill", new Vector4(72, 72, 72, 72)), ("ui_tile", new Vector4(67, 81, 67, 67)), ("ui_chip", new Vector4(48, 48, 48, 48)),
            ("ui_ribbon", new Vector4(59, 59, 59, 59)),
        };

        // Height below which a sliced sprite's borders would overlap; smaller rects scale their borders down.
        public static float MinHeight(string name)
        {
            var border = Borders.FirstOrDefault(b => name.StartsWith(b.Prefix)).Border;
            return border.y + border.w;
        }

        [MenuItem("Tileworks/Shared/Import UI Kit")]
        public static void Import()
        {
            AssetDatabase.Refresh();
            var sprites = Directory.GetFiles(Folder, "*.png").OrderBy(p => p).Select(path =>
            {
                var name = Path.GetFileNameWithoutExtension(path);
                return ArtImport.Sprite(path, 100, border: Borders.FirstOrDefault(b => name.StartsWith(b.Prefix)).Border);
            }).ToArray();
            ArtImport.Atlas(Atlas, sprites);
        }

        public static Sprite Load(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/{name}.png");
            if (sprite == null) Debug.LogWarning($"[Tileworks] Missing kit sprite {name}");
            return sprite;
        }
    }
}
