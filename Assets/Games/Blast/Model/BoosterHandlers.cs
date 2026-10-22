using System;
using Tileworks.Tiles.Model;

namespace Blast.Model
{
    // The hammer breaks one cell's top layer: a cube or balloon goes, a box loses a layer, a special goes off.
    public sealed class HammerHandler : IActionHandler<BlastState, Hammer>
    {
        private readonly BlastBoard _board;

        public HammerHandler(BlastBoard board) => _board = board;

        public static bool CanHit(BlastState s, int cell) =>
            cell >= 0 && cell < s.CellCount && s.IsPlayable(cell) && s.Pieces[cell] != Piece.Empty && s.Pieces[cell] != Piece.Duck && !s.Flight[cell];

        public RejectReason Validate(BlastState s, in Hammer a)
        {
            if (s.Outcome != Outcome.Ongoing) return RejectReason.GameOver;
            if (s.MovesLeft <= 0) return BlastRejects.NoMovesLeft;
            if (a.Cell < 0 || a.Cell >= s.CellCount || !s.IsPlayable(a.Cell)) return RejectReason.InvalidTarget;
            return CanHit(s, a.Cell) ? RejectReason.None : BlastRejects.NotHammerable;
        }

        public void Apply(BlastState s, in Hammer a, RuleContext ctx)
        {
            HammerStruck.Emit(ctx.Events, _board.Ids, s.Ids[a.Cell], a.Cell);
            s.NextCount = 0;
            _board.Hit(s, ctx, a.Cell, 0, Effect.None);
            s.SwapPending();
        }
    }

    // Cubes and specials move, each to a different cell when it can; blockers and ducks stay.
    public sealed class ShuffleBoardHandler : IActionHandler<BlastState, ShuffleBoard>
    {
        private const int Attempts = 20;

        private readonly BlastEventIds _ids;

        public ShuffleBoardHandler(BlastEventIds ids) => _ids = ids;

        public RejectReason Validate(BlastState s, in ShuffleBoard a)
        {
            if (s.Outcome != Outcome.Ongoing) return RejectReason.GameOver;
            if (s.MovesLeft <= 0) return BlastRejects.NoMovesLeft;
            if (s.Flight.Count > 0) return BlastRejects.Falling;
            return Movable(s) >= 2 ? RejectReason.None : BlastRejects.NothingToShuffle;
        }

        public void Apply(BlastState s, in ShuffleBoard a, RuleContext ctx)
        {
            var count = Movable(s);
            Span<Piece> pieces = stackalloc Piece[count];
            Span<byte> colours = stackalloc byte[count];
            Span<int> ids = stackalloc int[count];
            Span<int> order = stackalloc int[count];
            for (var i = 0; i < count; i++)
            {
                var cell = s.Group[i];
                (pieces[i], colours[i], ids[i]) = (s.Pieces[cell], s.Colours[cell], s.Ids[cell]);
            }

            // The first half of the attempts use Sattolo's shuffle (one cycle, so no piece stays put), the rest any order.
            ref var rng = ref ctx.Rng.Get(BlastRules.ShuffleStream);
            for (var attempt = 0; attempt < Attempts; attempt++)
            {
                var derange = attempt < Attempts / 2;
                for (var i = 0; i < count; i++) order[i] = i;
                for (var i = count - 1; i > 0; i--)
                {
                    var j = rng.NextInt(derange ? i : i + 1);
                    (order[i], order[j]) = (order[j], order[i]);
                }

                for (var i = 0; i < count; i++)
                {
                    var cell = s.Group[i];
                    (s.Pieces[cell], s.Colours[cell], s.Ids[cell]) = (pieces[order[i]], colours[order[i]], ids[order[i]]);
                }

                if (BlastBoard.HasMove(s)) break;
            }

            ctx.Events.Emit(_ids.BoardShuffled, 0);
            for (var i = 0; i < count; i++)
                if (order[i] != i) PieceShuffled.Emit(ctx.Events, _ids, ids[order[i]], s.Group[order[i]], s.Group[i]);
        }

        private static int Movable(BlastState s)
        {
            var count = 0;
            for (var cell = 0; cell < s.CellCount; cell++)
                if (s.Pieces[cell] == Piece.Cube || Pieces.IsSpecial(s.Pieces[cell])) s.Group[count++] = cell;
            return count;
        }
    }
}
