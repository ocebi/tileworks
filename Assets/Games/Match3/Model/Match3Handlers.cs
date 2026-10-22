using System;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Model;

namespace Match3.Model
{
    public sealed class SwapHandler : IActionHandler<Match3State, Swap>
    {
        private readonly Match3Board _board;
        private readonly MatchClearer _matches;

        public SwapHandler(Match3Board board, MatchClearer matches)
        {
            _board = board;
            _matches = matches;
        }

        public RejectReason Validate(Match3State s, in Swap a)
        {
            if (s.Outcome != Outcome.Ongoing) return RejectReason.GameOver;
            if (s.MovesLeft <= 0) return Match3Rejects.NoMovesLeft;
            if (!IsCell(s, a.From) || !IsCell(s, a.To)) return RejectReason.InvalidTarget;
            if (!Match3Board.AreNeighbours(s, a.From, a.To)) return Match3Rejects.NotNeighbours;
            if (s.Chained[a.From] || s.Chained[a.To]) return Match3Rejects.Chained;
            if (s.Flight[a.From] || s.Flight[a.To]) return Match3Rejects.Falling;
            if (!Matches.CanSwap(s, a.From) || !Matches.CanSwap(s, a.To)) return Match3Rejects.Fixed;
            if (Pieces.IsSpecial(s.Pieces[a.From]) || Pieces.IsSpecial(s.Pieces[a.To])) return RejectReason.None;
            return Matches.MakesMatch(s, a.From, a.To) ? RejectReason.None : Match3Rejects.NoMatch;
        }

        public void Apply(Match3State s, in Swap a, RuleContext ctx)
        {
            s.BeginMove();
            s.MovesLeft--;
            s.MovesUsed++;
            MovesChanged.Emit(ctx.Events, _board.Ids, s.MovesLeft);
            PiecesSwapped.Emit(ctx.Events, _board.Ids, s.Ids[a.From], a.From, a.To, s.Ids[a.To]);
            Match3Board.Exchange(s, a.From, a.To);
            s.Landed[a.From] = ++s.LandClock;
            s.Landed[a.To] = ++s.LandClock;

            var moved = s.Pieces[a.To];
            var other = s.Pieces[a.From];
            s.NextCount = 0;
            if (Pieces.IsSpecial(moved) && Pieces.IsSpecial(other)) Combine(s, a.To, a.From, ctx);
            else if (Pieces.IsSpecial(moved)) _board.Queue(s, a.To, Match3Board.EffectOf(moved), GemColour(s, a.From));
            else if (Pieces.IsSpecial(other)) _board.Queue(s, a.From, Match3Board.EffectOf(other), GemColour(s, a.To));
            else if (_matches.ClearAll(s, ctx)) s.ClearedWave = 0;
            s.SwapPending();
        }

        // The other special moves onto `at`, and the pair fires there as one combo.
        private void Combine(Match3State s, int at, int other, RuleContext ctx)
        {
            var effect = Match3Board.Combine(s.Pieces[at], s.Pieces[other]);
            SpecialMerged.Emit(ctx.Events, _board.Ids, s.Ids[other], other, at);
            s.Pieces[other] = Piece.Empty;
            s.Colours[other] = 0;
            SpecialsCombined.Emit(ctx.Events, _board.Ids, s.Ids[at], at, effect);
            _board.Queue(s, at, effect, 0);
        }

        private static byte GemColour(Match3State s, int cell) => s.Pieces[cell] == Piece.Gem ? s.Colours[cell] : (byte)0;

        private static bool IsCell(Match3State s, int cell) => cell >= 0 && cell < s.CellCount && s.IsPlayable(cell);
    }

    public sealed class ActivateHandler : IActionHandler<Match3State, Activate>
    {
        private readonly Match3Board _board;

        public ActivateHandler(Match3Board board) => _board = board;

        public RejectReason Validate(Match3State s, in Activate a)
        {
            if (s.Outcome != Outcome.Ongoing) return RejectReason.GameOver;
            if (s.MovesLeft <= 0) return Match3Rejects.NoMovesLeft;
            if (a.Cell < 0 || a.Cell >= s.CellCount || !s.IsPlayable(a.Cell)) return RejectReason.InvalidTarget;
            if (!Pieces.IsSpecial(s.Pieces[a.Cell])) return Match3Rejects.NotSpecial;
            return s.Flight[a.Cell] ? Match3Rejects.Falling : RejectReason.None;
        }

        public void Apply(Match3State s, in Activate a, RuleContext ctx)
        {
            s.BeginMove();
            s.MovesLeft--;
            s.MovesUsed++;
            MovesChanged.Emit(ctx.Events, _board.Ids, s.MovesLeft);
            var piece = s.Pieces[a.Cell];
            var colour = piece == Piece.Disco ? RandomColour(s, ref ctx.Rng.Get(Match3Rules.SpecialStream)) : (byte)0;
            s.NextCount = 0;
            _board.Queue(s, a.Cell, Match3Board.EffectOf(piece), colour);
            s.SwapPending();
        }

        // One of the colours still on the board, each equally likely.
        private static byte RandomColour(Match3State s, ref Pcg32 rng)
        {
            Span<bool> present = stackalloc bool[Match3Level.MaxColours + 1];
            var count = 0;
            for (var cell = 0; cell < s.CellCount; cell++)
            {
                if (s.Pieces[cell] != Piece.Gem || present[s.Colours[cell]]) continue;
                present[s.Colours[cell]] = true;
                count++;
            }

            if (count == 0) return 0;
            var pick = rng.NextInt(count);
            for (var c = 1; c < present.Length; c++)
                if (present[c] && pick-- == 0) return (byte)c;
            return 0;
        }
    }

    public sealed class AddMovesHandler : IActionHandler<Match3State, AddMoves>
    {
        private readonly Match3EventIds _ids;

        public AddMovesHandler(Match3EventIds ids) => _ids = ids;

        public RejectReason Validate(Match3State s, in AddMoves a)
        {
            if (s.Outcome != Outcome.Lost || s.MovesLeft > 0) return RejectReason.NotApplicable;
            return a.Moves > 0 ? RejectReason.None : RejectReason.InvalidTarget;
        }

        public void Apply(Match3State s, in AddMoves a, RuleContext ctx)
        {
            s.BeginMove();
            s.Outcome = Outcome.Ongoing;
            s.ContinuesUsed++;
            s.MovesLeft += a.Moves;
            MovesChanged.Emit(ctx.Events, _ids, s.MovesLeft);
        }
    }

    // The view reports a piece landed; the pass after it clears the lines through it. Costs no move.
    public sealed class LandHandler : IActionHandler<Match3State, Land>
    {
        public RejectReason Validate(Match3State s, in Land a) => FlightRules.Check(s, a);

        public void Apply(Match3State s, in Land a, RuleContext ctx)
        {
            s.BeginAction();
            Match3Board.Land(s, a.Cell);
        }
    }

    // Lands everything in flight through the LandingSystem, one piece per quiet pass.
    public sealed class LandAllHandler : IActionHandler<Match3State, LandAll>
    {
        public RejectReason Validate(Match3State s, in LandAll a) => FlightRules.Check(s);

        public void Apply(Match3State s, in LandAll a, RuleContext ctx)
        {
            s.BeginAction();
            s.Flight.LandingAll = true;
        }
    }

    // While pieces fly no loss is declared: the last landing may still win, and the one that settles the board decides.
    public sealed class Match3EndConditions : IEndCondition<Match3State>
    {
        private readonly Match3EventIds _ids;

        public Match3EndConditions(Match3EventIds ids) => _ids = ids;

        public Outcome Evaluate(Match3State s, RuleContext ctx)
        {
            if (s.GoalsMet)
            {
                ctx.Events.Emit(_ids.LevelWon, 0);
                return Outcome.Won;
            }

            if (s.Flight.Count > 0 || s.MovesLeft > 0 && Matches.HasMove(s)) return Outcome.Ongoing;
            ctx.Events.Emit(_ids.LevelLost, 0);
            return Outcome.Lost;
        }
    }
}
