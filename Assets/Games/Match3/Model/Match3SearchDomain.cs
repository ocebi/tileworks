using System;
using Tileworks.Tiles.Model;
using Tileworks.Tiles.Search;

namespace Match3.Model
{
    // One swap per pair of neighbours. Refills are random, so playouts measure difficulty rather than prove it.
    public sealed class Match3SearchDomain : ISearchDomain<Match3State, Swap>
    {
        private readonly Match3Level _level;
        private readonly RuleEngine<Match3State> _engine;

        public Match3SearchDomain(Match3Rules rules, Match3Level level, ulong seed = 0)
        {
            _level = level ?? throw new ArgumentNullException(nameof(level));
            _engine = (rules ?? throw new ArgumentNullException(nameof(rules))).CreateEngine(level, seed);
            _engine.RecordActions = false;
        }

        public int MaxMoves => _level.CellCount * 2;
        public Match3State CreateScratch() => new Match3State(_level);
        public void CopyTo(Match3State from, Match3State to) => from.CopyTo(to);
        public ulong Hash(Match3State s) => s.ComputeHash();
        public int Pressure(Match3State s) => -s.MovesLeft;

        public SearchStatus Status(Match3State s) =>
            s.Outcome == Outcome.Won ? SearchStatus.Won : s.Outcome == Outcome.Lost ? SearchStatus.Lost : SearchStatus.Ongoing;

        public int GetMoves(Match3State s, Span<Swap> into)
        {
            var count = 0;
            for (var cell = 0; cell < s.CellCount; cell++)
            {
                if (!Matches.CanSwap(s, cell)) continue;
                for (var d = 0; d < 2; d++)
                {
                    var n = Match3Board.Neighbour(s, cell, d);
                    if (Matches.CanSwap(s, n) && Match3Hints.Value(s, cell, n) > 0) into[count++] = new Swap(cell, n);
                }
            }

            return count;
        }

        public void Apply(Match3State s, in Swap move)
        {
            _engine.Attach(s);
            var result = _engine.Apply(move);
            if (!result.Accepted) throw new InvalidOperationException($"Search applied an illegal move {move}: {result.Rejected}");
            if (s.Outcome == Outcome.Ongoing && s.Flight.Count > 0) _engine.Apply(new LandAll());
        }

        // What a casual player goes for: specials and combos first, then big matches, matches that work on a goal, and
        // matches next to blockers.
        public int Score(Match3State s, in Swap move)
        {
            var value = Match3Hints.Value(s, move.From, move.To);
            if (Pieces.IsSpecial(s.Pieces[move.From]) || Pieces.IsSpecial(s.Pieces[move.To])) return 4000 + value * 2;
            var score = value * 10;
            Match3Board.Exchange(s, move.From, move.To);
            score += Goals(s, move.To) + Goals(s, move.From);
            Match3Board.Exchange(s, move.From, move.To);
            return score;
        }

        private static int Goals(Match3State s, int cell)
        {
            Matches.SpecialAt(s, cell, out var size);
            var score = 0;
            var goals = s.Level.Goals;
            for (var i = 0; i < size; i++)
            {
                var matched = s.Group[i];
                if (s.Floors[matched] > 0) score += 30;
                if (s.Chained[matched]) score += 30;
                for (var g = 0; g < goals.Length; g++)
                    if (goals[g].Kind == GoalKind.Colour && goals[g].Colour == s.Colours[matched] && s.GoalsLeft[g] > 0) score += 25;
                for (var d = 0; d < 4; d++)
                {
                    var n = Match3Board.Neighbour(s, matched, d);
                    if (n >= 0 && (s.Pieces[n] == Piece.Balloon || s.Pieces[n] == Piece.Box)) score += 40;
                }
            }

            return score;
        }
    }
}
