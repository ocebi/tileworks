using System;
using Tileworks.Tiles.Model;
using Tileworks.Tiles.Search;

namespace Blast.Model
{
    // One tap per group: tapping any cube of a group clears the same cubes. Refills are random, so playouts measure
    // difficulty rather than prove it.
    public sealed class BlastSearchDomain : ISearchDomain<BlastState, Tap>
    {
        private readonly BlastLevel _level;
        private readonly RuleEngine<BlastState> _engine;

        public BlastSearchDomain(BlastRules rules, BlastLevel level, ulong seed = 0)
        {
            _level = level ?? throw new ArgumentNullException(nameof(level));
            _engine = (rules ?? throw new ArgumentNullException(nameof(rules))).CreateEngine(level, seed);
            _engine.RecordActions = false;
        }

        public int MaxMoves => _level.CellCount;
        public BlastState CreateScratch() => new BlastState(_level);
        public void CopyTo(BlastState from, BlastState to) => from.CopyTo(to);
        public ulong Hash(BlastState s) => s.ComputeHash();
        public int Pressure(BlastState s) => -s.MovesLeft;

        public SearchStatus Status(BlastState s) =>
            s.Outcome == Outcome.Won ? SearchStatus.Won : s.Outcome == Outcome.Lost ? SearchStatus.Lost : SearchStatus.Ongoing;

        public int GetMoves(BlastState s, Span<Tap> into)
        {
            var count = 0;
            Span<bool> seen = stackalloc bool[s.CellCount];
            for (var cell = 0; cell < s.CellCount; cell++)
            {
                if (seen[cell]) continue;
                var size = BlastBoard.FindGroup(s, cell);
                for (var i = 0; i < size; i++) seen[s.Group[i]] = true;
                if (size >= 2 || (size == 1 && Pieces.IsSpecial(s.Pieces[cell]))) into[count++] = new Tap(cell);
            }

            return count;
        }

        public void Apply(BlastState s, in Tap move)
        {
            _engine.Attach(s);
            var result = _engine.Apply(move);
            if (!result.Accepted) throw new InvalidOperationException($"Search applied an illegal move {move}: {result.Rejected}");

            // A bot sees every piece land before its next move.
            if (s.Outcome == Outcome.Ongoing && s.Flight.Count > 0) _engine.Apply(new LandAll());
        }

        // What a casual player goes for: specials first, then big groups, groups that work on a goal, groups next to
        // blockers, and cubes under a duck.
        public int Score(BlastState s, in Tap move)
        {
            if (Pieces.IsSpecial(s.Pieces[move.Cell])) return 5000 + BlastBoard.FindGroup(s, move.Cell) * 500;
            var size = BlastBoard.FindGroup(s, move.Cell);
            var colour = s.Colours[move.Cell];
            var score = size * 10 + (size >= TapHandler.RocketAt ? 300 : 0);
            var goals = s.Level.Goals;
            for (var i = 0; i < goals.Length; i++)
                if (goals[i].Kind == GoalKind.Colour && goals[i].Colour == colour && s.GoalsLeft[i] > 0) score += size * 25;

            for (var i = 0; i < size; i++)
            {
                var cell = s.Group[i];
                if (HasDuckAbove(s, cell)) score += 30;
                for (var d = 0; d < 4; d++)
                {
                    var n = BlastBoard.Neighbour(s, cell, d);
                    if (n >= 0 && (s.Pieces[n] == Piece.Balloon || s.Pieces[n] == Piece.Box)) score += 40;
                }
            }

            return score;
        }

        private static bool HasDuckAbove(BlastState s, int cell)
        {
            for (var above = cell - s.Width; above >= 0; above -= s.Width)
                if (s.Pieces[above] == Piece.Duck) return true;
            return false;
        }
    }
}
