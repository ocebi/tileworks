using Tileworks.Kit.Core;
using Tileworks.Tiles.Model;

namespace Blast.Model
{
    public sealed class TapHandler : IActionHandler<BlastState, Tap>
    {
        public const int RocketAt = 5;
        public const int BombAt = 7;
        public const int DiscoAt = 9;

        private readonly BlastBoard _board;

        public TapHandler(BlastBoard board) => _board = board;

        public RejectReason Validate(BlastState s, in Tap a)
        {
            if (s.Outcome != Outcome.Ongoing) return RejectReason.GameOver;
            if (s.MovesLeft <= 0) return BlastRejects.NoMovesLeft;
            if (a.Cell < 0 || a.Cell >= s.CellCount || !s.IsPlayable(a.Cell)) return RejectReason.InvalidTarget;
            if (s.Flight[a.Cell]) return BlastRejects.Falling;
            var piece = s.Pieces[a.Cell];
            if (Pieces.IsSpecial(piece)) return RejectReason.None;
            if (piece != Piece.Cube) return BlastRejects.NotTappable;
            return BlastBoard.FindGroup(s, a.Cell) >= 2 ? RejectReason.None : BlastRejects.GroupTooSmall;
        }

        public void Apply(BlastState s, in Tap a, RuleContext ctx)
        {
            s.MovesLeft--;
            s.MovesUsed++;
            MovesChanged.Emit(ctx.Events, _board.Ids, s.MovesLeft);
            if (s.Pieces[a.Cell] == Piece.Cube) BlastGroup(s, a.Cell, ctx);
            else FireSpecials(s, a.Cell, ctx);
        }

        public static Piece SpecialFor(int groupSize, ref Pcg32 rng)
        {
            if (groupSize >= DiscoAt) return Piece.Disco;
            if (groupSize >= BombAt) return Piece.Bomb;
            if (groupSize >= RocketAt) return rng.NextInt(2) == 0 ? Piece.RocketRow : Piece.RocketColumn;
            return Piece.Empty;
        }

        private void BlastGroup(BlastState s, int tapped, RuleContext ctx)
        {
            var count = BlastBoard.FindGroup(s, tapped);
            var colour = s.Colours[tapped];
            var inGroup = s.MarkStamp;
            var special = SpecialFor(count, ref ctx.Rng.Get(BlastRules.SpecialStream));
            var into = special == Piece.Empty ? -1 : tapped;
            for (var i = 0; i < count; i++) _board.Remove(s, ctx, s.Group[i], BlastBoard.Distance(s, tapped, s.Group[i]), Effect.None, into);

            // Each landed blocker next to the group is damaged once, however many group cells touch it.
            var damaged = s.NewMark();
            for (var i = 0; i < count; i++)
            {
                var cell = s.Group[i];
                for (var d = 0; d < 4; d++)
                {
                    var n = BlastBoard.Neighbour(s, cell, d);
                    if (n < 0 || s.Marks[n] == inGroup || s.Marks[n] == damaged || s.Flight[n]) continue;
                    s.Marks[n] = damaged;
                    _board.DamageNextTo(s, ctx, n, BlastBoard.Distance(s, tapped, n));
                }
            }

            if (special == Piece.Empty) return;
            s.Pieces[tapped] = special;
            s.Colours[tapped] = colour;
            s.Ids[tapped] = BlastBoard.NewId(s);
            SpecialCreated.Emit(ctx.Events, _board.Ids, s.Ids[tapped], tapped, special, colour);
        }

        // Touching specials fire together as a combo decided by the two strongest; a lone special fires by itself.
        private void FireSpecials(BlastState s, int tapped, RuleContext ctx)
        {
            var count = BlastBoard.FindGroup(s, tapped);
            if (count == 1)
            {
                s.NextCount = 0;
                _board.Queue(s, tapped, BlastBoard.EffectOf(s.Pieces[tapped]));
                s.SwapPending();
                return;
            }

            int first = -1, second = -1;
            for (var i = 0; i < count; i++)
            {
                var cell = s.Group[i];
                if (first < 0 || Strength(s.Pieces[cell]) > Strength(s.Pieces[first])) (first, second) = (cell, first);
                else if (second < 0 || Strength(s.Pieces[cell]) > Strength(s.Pieces[second])) second = cell;
            }

            var effect = BlastBoard.Combine(s.Pieces[first], s.Pieces[second]);
            var colour = s.Pieces[first] == Piece.Disco ? s.Colours[first] : s.Colours[second];
            for (var i = 0; i < count; i++)
            {
                var cell = s.Group[i];
                if (cell == tapped) continue;
                SpecialMerged.Emit(ctx.Events, _board.Ids, s.Ids[cell], cell, tapped);
                s.Clear(cell);
            }

            SpecialsCombined.Emit(ctx.Events, _board.Ids, s.Ids[tapped], tapped, effect, count, s.NextChain(ctx.ActionIndex));
            s.NextCount = 0;
            _board.Queue(s, tapped, effect);
            s.Next[0] = new PendingSpecial(tapped, s.Next[0].Id, effect, colour);
            s.SwapPending();
        }

        private static int Strength(Piece p) => p == Piece.Disco ? 3 : p == Piece.Bomb ? 2 : 1;
    }

    public sealed class AddMovesHandler : IActionHandler<BlastState, AddMoves>
    {
        private readonly BlastEventIds _ids;

        public AddMovesHandler(BlastEventIds ids) => _ids = ids;

        public RejectReason Validate(BlastState s, in AddMoves a)
        {
            if (s.Outcome != Outcome.Lost || s.MovesLeft > 0) return RejectReason.NotApplicable;
            return a.Moves > 0 ? RejectReason.None : RejectReason.InvalidTarget;
        }

        public void Apply(BlastState s, in AddMoves a, RuleContext ctx)
        {
            s.Outcome = Outcome.Ongoing;
            s.ContinuesUsed++;
            s.MovesLeft += a.Moves;
            MovesChanged.Emit(ctx.Events, _ids, s.MovesLeft);
        }
    }

    public sealed class BlastEndConditions : IEndCondition<BlastState>
    {
        private readonly BlastEventIds _ids;

        public BlastEndConditions(BlastEventIds ids) => _ids = ids;

        public Outcome Evaluate(BlastState s, RuleContext ctx)
        {
            if (s.GoalsMet)
            {
                ctx.Events.Emit(_ids.LevelWon, 0);
                return Outcome.Won;
            }

            // The last landing may still win, so nothing is lost while pieces fly.
            if (s.Flight.Count > 0) return Outcome.Ongoing;
            if (s.MovesLeft > 0 && BlastBoard.HasMove(s)) return Outcome.Ongoing;
            ctx.Events.Emit(_ids.LevelLost, 0);
            return Outcome.Lost;
        }
    }
}
