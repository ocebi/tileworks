using System;
using Tileworks.Tiles.Model;

namespace TileMatch.Model
{
    // data[tile] = partner tile or -1. Selecting either moves both, so both must be free.
    public sealed class LinkedModifier : ITileModifier
    {
        public ushort TypeId => ModifierTypes.Linked;

        public int[] CreateState(TileMatchLevel level)
        {
            var data = new int[level.TileCount];
            Array.Fill(data, -1);
            foreach (var spec in level.Modifiers)
            {
                if (spec.Type != TypeId) continue;
                data[spec.Tile] = spec.Value;
                data[spec.Value] = spec.Tile;
            }

            return data;
        }

        public RejectReason CanSelect(TileMatchState s, int[] data, int tile)
        {
            var partner = data[tile];
            if (partner < 0 || !s.IsOnBoard(partner)) return RejectReason.None;
            return s.IsFree(partner) ? RejectReason.None : TileMatchRejects.PartnerBlocked;
        }

        public int CollectCompanions(TileMatchState s, int[] data, int tile, Span<int> into)
        {
            var partner = data[tile];
            if (partner < 0 || !s.IsOnBoard(partner) || into.Length == 0) return 0;
            into[0] = partner;
            return 1;
        }

        public TileTraits Traits(TileMatchState s, int[] data, int tile) =>
            data[tile] >= 0 && s.IsOnBoard(data[tile]) ? TileTraits.Linked : TileTraits.None;
    }
}
