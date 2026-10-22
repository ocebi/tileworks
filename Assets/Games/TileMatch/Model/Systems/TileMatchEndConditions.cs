using System;
using Tileworks.Tiles.Model;

namespace TileMatch.Model
{
    public sealed class TileMatchEndConditions : IEndCondition<TileMatchState>
    {
        private readonly ModifierSet _modifiers;
        private readonly TileMatchEventIds _ids;

        public TileMatchEndConditions(ModifierSet modifiers, TileMatchEventIds ids)
        {
            _modifiers = modifiers;
            _ids = ids;
        }

        public Outcome Evaluate(TileMatchState s, RuleContext ctx)
        {
            if (s.Outcome != Outcome.Ongoing) return Outcome.Ongoing;

            if (AllCleared(s))
            {
                ctx.Events.Emit(_ids.LevelWon, -1);
                return Outcome.Won;
            }

            var reason = s.TrayCount >= s.TrayCapacity ? LoseReason.TrayFull
                : !HasSelectableTile(s, _modifiers) ? LoseReason.NoMoves
                : LoseReason.None;
            if (reason == LoseReason.None) return Outcome.Ongoing;

            s.LoseReason = reason;
            LevelLost.Emit(ctx.Events, _ids, reason);
            return Outcome.Lost;
        }

        public static bool AllCleared(TileMatchState s)
        {
            for (var i = 0; i < s.TileCount; i++)
            {
                if (s.Locations[i] != TileLocation.Cleared) return false;
            }

            return true;
        }

        public static bool HasSelectableTile(TileMatchState s, ModifierSet modifiers)
        {
            Span<int> group = stackalloc int[SelectTileHandler.MaxGroup];
            for (var tile = 0; tile < s.TileCount; tile++)
            {
                if (SelectTileHandler.ValidateTile(s, modifiers, tile, group).IsNone) return true;
            }

            return false;
        }
    }
}
