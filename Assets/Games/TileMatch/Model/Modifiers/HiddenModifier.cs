using Tileworks.Tiles.Model;

namespace TileMatch.Model
{
    // data[tile] = 1 while the kind is face-down; revealed as soon as the tile is free.
    public sealed class HiddenModifier : ITileModifier
    {
        public ushort TypeId => ModifierTypes.Hidden;

        public int[] CreateState(TileMatchLevel level)
        {
            var data = new int[level.TileCount];
            foreach (var spec in level.Modifiers)
            {
                if (spec.Type != TypeId) continue;
                var slot = level.SlotOf(spec.Tile);
                if (level.Cover.InitialCoveringCount(slot) > 0) data[spec.Tile] = 1;
            }

            return data;
        }

        public TileTraits Traits(TileMatchState s, int[] data, int tile) => data[tile] > 0 ? TileTraits.KindHidden : TileTraits.None;

        public bool OnReact(TileMatchState s, int[] data, RuleContext ctx, in TileMatchEventIds ids)
        {
            var changed = false;
            for (var tile = 0; tile < s.TileCount; tile++)
            {
                if (data[tile] == 0 || !s.IsFree(tile)) continue;
                data[tile] = 0;
                TileRevealed.Emit(ctx.Events, ids, tile, s.Kinds[tile]);
                changed = true;
            }

            return changed;
        }
    }
}
