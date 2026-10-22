using System;
using Tileworks.Tiles.Model;

namespace TileMatch.Model
{
    public sealed class SelectTileHandler : IActionHandler<TileMatchState, SelectTile>
    {
        public const int MaxGroup = 4;

        private readonly ModifierSet _modifiers;
        private readonly TileMatchEventIds _ids;

        public SelectTileHandler(ModifierSet modifiers, TileMatchEventIds ids)
        {
            _modifiers = modifiers;
            _ids = ids;
        }

        public RejectReason Validate(TileMatchState s, in SelectTile action)
        {
            if (s.Outcome != Outcome.Ongoing) return RejectReason.GameOver;
            if ((uint)action.Tile >= (uint)s.TileCount) return RejectReason.InvalidTarget;
            Span<int> group = stackalloc int[MaxGroup];
            return ValidateTile(s, _modifiers, action.Tile, group);
        }

        public void Apply(TileMatchState s, in SelectTile action, RuleContext ctx)
        {
            Span<int> group = stackalloc int[MaxGroup];
            var count = CollectGroup(s, _modifiers, action.Tile, group);
            var groupId = s.NextGroup++;
            for (var i = 0; i < count; i++) BoardOps.RemoveFromBoard(s, group[i], groupId, ctx, _ids);
            s.ActionsTaken++;
        }

        // Shared with the no-moves check and the solver, so "selectable" means the same thing everywhere.
        public static RejectReason ValidateTile(TileMatchState s, ModifierSet modifiers, int tile, Span<int> group)
        {
            if (!s.IsOnBoard(tile)) return RejectReason.InvalidTarget;
            if (!s.IsFree(tile)) return RejectReason.NotFree;
            var veto = modifiers.CanSelect(s, tile);
            if (!veto.IsNone) return veto;

            var count = CollectGroup(s, modifiers, tile, group);
            for (var i = 1; i < count; i++)
            {
                if (!s.IsFree(group[i]) || !modifiers.CanSelect(s, group[i]).IsNone) return TileMatchRejects.PartnerBlocked;
            }

            return s.FreeTraySlots >= count ? RejectReason.None : RejectReason.NoRoom;
        }

        public static int CollectGroup(TileMatchState s, ModifierSet modifiers, int tile, Span<int> group)
        {
            group[0] = tile;
            return 1 + modifiers.CollectCompanions(s, tile, group.Slice(1));
        }
    }
}
