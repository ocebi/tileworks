using Tileworks.Tiles.Model;

namespace Match3.Model
{
    // The hammer takes one cell's top layer, as a special's hit does: a chain, else a box layer, else the piece and one
    // floor layer. A special it hits goes off; a disco clears the most common colour.
    public sealed class HammerHandler : IActionHandler<Match3State, Hammer>
    {
        private readonly Match3Board _board;

        public HammerHandler(Match3Board board) => _board = board;

        public static bool CanHit(Match3State s, int cell) =>
            cell >= 0 && cell < s.CellCount && s.IsPlayable(cell) && (s.Chained[cell] || s.Pieces[cell] != Piece.Empty && s.Pieces[cell] != Piece.Duck);

        public RejectReason Validate(Match3State s, in Hammer a)
        {
            if (s.Outcome != Outcome.Ongoing) return RejectReason.GameOver;
            if (s.MovesLeft <= 0) return Match3Rejects.NoMovesLeft;
            if (a.Cell < 0 || a.Cell >= s.CellCount || !s.IsPlayable(a.Cell)) return RejectReason.InvalidTarget;
            return CanHit(s, a.Cell) ? RejectReason.None : Match3Rejects.NotHammerable;
        }

        public void Apply(Match3State s, in Hammer a, RuleContext ctx)
        {
            s.BeginMove();
            HammerStruck.Emit(ctx.Events, _board.Ids, s.Ids[a.Cell], a.Cell, s.Floors[a.Cell] > 0 ? s.FloorIds[a.Cell] : -1);
            s.NextCount = 0;
            _board.Hit(s, ctx, a.Cell, 0, Effect.None);
            s.SwapPending();
        }
    }

    // Gems and specials move, each to a different cell when it can, leaving no match and at least one move. Blockers,
    // chained gems and ducks stay.
    public sealed class ShuffleBoardHandler : IActionHandler<Match3State, ShuffleBoard>
    {
        private readonly Match3EventIds _ids;

        public ShuffleBoardHandler(Match3EventIds ids) => _ids = ids;

        public RejectReason Validate(Match3State s, in ShuffleBoard a)
        {
            if (s.Outcome != Outcome.Ongoing) return RejectReason.GameOver;
            if (s.Flight.Count > 0) return Match3Rejects.Falling;
            return ShuffleSystem.Movable(s) >= 3 ? RejectReason.None : Match3Rejects.NothingToShuffle;
        }

        public void Apply(Match3State s, in ShuffleBoard a, RuleContext ctx)
        {
            s.BeginMove();
            var count = ShuffleSystem.Movable(s);
            if (!ShuffleSystem.Shuffle(s, count, ref ctx.Rng.Get(Match3Rules.ShuffleStream), derange: true)) return;
            ShuffleSystem.Emit(s, ctx, _ids, count, true);
            s.MovedWave = 0;
        }
    }
}
