using Tileworks.Tiles.Model;

namespace TileMatch.Model
{
    // Returns the most recent selection still in the tray (a linked pair returns together).
    public sealed class UndoBooster
    {
        private readonly TileMatchEventIds _ids;

        public UndoBooster(TileMatchEventIds ids) => _ids = ids;

        public RejectReason Validate(TileMatchState s)
        {
            var last = LastUndoable(s);
            if (last < 0) return TileMatchRejects.NothingToUndo;
            var group = s.InsertGroup[last];
            for (var i = last; i >= 0; i--)
            {
                if (s.InsertGroup[i] != group) continue;
                var tile = s.InsertTile[i];
                if (s.Locations[tile] == TileLocation.Tray && s.Occupant[s.SlotOf(tile)] >= 0) return TileMatchRejects.SlotOccupied;
            }

            return RejectReason.None;
        }

        public void Apply(TileMatchState s, RuleContext ctx)
        {
            var last = LastUndoable(s);
            var group = s.InsertGroup[last];
            for (var i = last; i >= 0; i--)
            {
                if (s.InsertGroup[i] != group) continue;
                var tile = s.InsertTile[i];
                if (s.Locations[tile] != TileLocation.Tray) continue;
                BoardOps.RemoveFromTray(s, BoardOps.TrayIndexOf(s, tile), ctx, _ids);
                BoardOps.ReturnToBoard(s, tile, ctx, _ids);
            }

            s.InsertCount = last;
            while (s.InsertCount > 0 && s.Locations[s.InsertTile[s.InsertCount - 1]] != TileLocation.Tray) s.InsertCount--;
        }

        private static int LastUndoable(TileMatchState s)
        {
            for (var i = s.InsertCount - 1; i >= 0; i--)
            {
                if (s.Locations[s.InsertTile[i]] == TileLocation.Tray) return i;
            }

            return -1;
        }
    }
}
