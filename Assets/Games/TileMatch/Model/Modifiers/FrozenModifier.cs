using System;
using Tileworks.Tiles.Model;

namespace TileMatch.Model
{
    // data[tile] = ice layers; data[TileCount] = triples already counted. Each triple anywhere cracks one layer everywhere.
    public sealed class FrozenModifier : ITileModifier
    {
        public ushort TypeId => ModifierTypes.Frozen;

        public int[] CreateState(TileMatchLevel level)
        {
            var data = new int[level.TileCount + 1];
            foreach (var spec in level.Modifiers)
            {
                if (spec.Type == TypeId) data[spec.Tile] = Math.Max(1, spec.Value);
            }

            return data;
        }

        public RejectReason CanSelect(TileMatchState s, int[] data, int tile) =>
            data[tile] > 0 ? TileMatchRejects.Frozen : RejectReason.None;

        public TileTraits Traits(TileMatchState s, int[] data, int tile) => data[tile] > 0 ? TileTraits.Locked : TileTraits.None;

        public bool OnReact(TileMatchState s, int[] data, RuleContext ctx, in TileMatchEventIds ids)
        {
            var counted = s.TileCount;
            var cracks = s.TriplesCleared - data[counted];
            if (cracks <= 0) return false;
            data[counted] = s.TriplesCleared;

            for (var tile = 0; tile < s.TileCount; tile++)
            {
                if (data[tile] == 0 || s.Locations[tile] == TileLocation.Cleared) continue;
                data[tile] = Math.Max(0, data[tile] - cracks);
                ModifierChanged.Emit(ctx.Events, ids, tile, TypeId, data[tile]);
            }

            return true;
        }
    }
}
