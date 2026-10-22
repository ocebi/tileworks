using Tileworks.Tiles.Model;

namespace TileMatch.Model
{
    // Revives a lost attempt: extra tray slots, and the most recent tray tiles go back to their board slots.
    public sealed class ContinueHandler : IActionHandler<TileMatchState, ContinueAfterFail>
    {
        private readonly TileMatchEventIds _ids;

        public ContinueHandler(TileMatchEventIds ids) => _ids = ids;

        public RejectReason Validate(TileMatchState s, in ContinueAfterFail action)
        {
            if (s.Outcome != Outcome.Lost || s.LoseReason == LoseReason.TimeUp) return TileMatchRejects.NotLost;
            return s.TrayCapacity + action.ExtraSlots > s.Tray.Length ? TileMatchRejects.ExtraSlotLimit : RejectReason.None;
        }

        public void Apply(TileMatchState s, in ContinueAfterFail action, RuleContext ctx)
        {
            s.Outcome = Outcome.Ongoing;
            s.LoseReason = LoseReason.None;
            s.ContinuesUsed++;
            if (action.ExtraSlots > 0) BoardOps.SetCapacity(s, s.TrayCapacity + action.ExtraSlots, ctx, _ids);

            var returned = 0;
            for (var i = s.InsertCount - 1; i >= 0 && returned < action.ReturnCount; i--)
            {
                var tile = s.InsertTile[i];
                if (s.Locations[tile] != TileLocation.Tray || s.Occupant[s.SlotOf(tile)] >= 0) continue;
                BoardOps.RemoveFromTray(s, BoardOps.TrayIndexOf(s, tile), ctx, _ids);
                BoardOps.ReturnToBoard(s, tile, ctx, _ids);
                returned++;
            }
        }
    }

    public sealed class TimeUpHandler : IActionHandler<TileMatchState, TimeUp>
    {
        private readonly TileMatchEventIds _ids;

        public TimeUpHandler(TileMatchEventIds ids) => _ids = ids;

        public RejectReason Validate(TileMatchState s, in TimeUp action) =>
            s.Outcome == Outcome.Ongoing ? RejectReason.None : RejectReason.GameOver;

        public void Apply(TileMatchState s, in TimeUp action, RuleContext ctx)
        {
            s.Outcome = Outcome.Lost;
            s.LoseReason = LoseReason.TimeUp;
            LevelLost.Emit(ctx.Events, _ids, LoseReason.TimeUp);
        }
    }
}
