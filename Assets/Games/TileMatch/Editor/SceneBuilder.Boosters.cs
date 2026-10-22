using Tileworlds.Games.Editor;
using Tileworks.Tiles.View;
using TileMatch.Model;
using TileMatch.View;
using UnityEngine;

namespace TileMatch.Editor
{
    // Tile Match's four boosters on the shared booster bar. They act at once, so the bar's aim card is never shown.
    public static partial class SceneBuilder
    {
        private static readonly string[] BoosterIcons = { "icon_undo", "icon_magnet", "icon_shuffle", "icon_slot" };
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        private const float BoosterGap = 240f;
        private const float BoosterHeight = 180f;

        private static BoosterBarView BuildBoosters(Canvas canvas, RectTransform safe)
        {
            var slots = new BoosterBarBuilder.Slot[BoosterIcons.Length];
            for (var i = 0; i < slots.Length; i++)
            {
                var kind = (BoosterKind)(i + 1);
                slots[i] = new BoosterBarBuilder.Slot(BoosterText.Name(kind), BoosterIcons[i], BoosterText.What(kind), label: BoosterText.Label(kind),
                    id: kind.ToString());
            }

            return BoosterBarBuilder.Build(canvas, safe, slots, BoosterGap, BoosterHeight);
        }
    }
}
