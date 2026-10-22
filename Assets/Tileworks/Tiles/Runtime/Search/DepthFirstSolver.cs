using System;
using System.Collections.Generic;

namespace Tileworks.Tiles.Search
{
    // Iterative DFS with an explicit stack, move ordering and a table of hashes already proven to fail.
    public sealed class DepthFirstSolver<TState, TMove> where TMove : struct
    {
        private readonly ISearchDomain<TState, TMove> _domain;
        private readonly List<TState> _states = new List<TState>();
        private readonly List<TMove[]> _moves = new List<TMove[]>();
        private readonly List<int> _moveCounts = new List<int>();
        private readonly List<int> _next = new List<int>();
        private readonly HashSet<ulong> _failed = new HashSet<ulong>();
        private int[] _scores;

        public DepthFirstSolver(ISearchDomain<TState, TMove> domain) => _domain = domain ?? throw new ArgumentNullException(nameof(domain));

        public SolveResult<TMove> Solve(TState start, SearchBudget budget)
        {
            var result = new SolveResult<TMove>();
            _failed.Clear();
            EnsureDepth(0);
            _domain.CopyTo(start, _states[0]);
            Expand(0);
            var depth = 0;

            while (depth >= 0)
            {
                if (_next[depth] >= _moveCounts[depth])
                {
                    _failed.Add(_domain.Hash(_states[depth]));
                    depth--;
                    continue;
                }

                if (result.Nodes >= budget.MaxNodes) return result;

                var move = _moves[depth][_next[depth]++];
                EnsureDepth(depth + 1);
                var child = _states[depth + 1];
                _domain.CopyTo(_states[depth], child);
                _domain.Apply(child, move);
                result.Nodes++;
                result.PeakPressure = Math.Max(result.PeakPressure, _domain.Pressure(child));

                var status = _domain.Status(child);
                if (status == SearchStatus.Won)
                {
                    result.Solved = true;
                    result.Path = BuildPath(depth + 1);
                    return result;
                }

                if (status == SearchStatus.Lost || _domain.Pressure(child) > budget.MaxPressure || _failed.Contains(_domain.Hash(child)))
                {
                    result.DeadEnds++;
                    continue;
                }

                depth++;
                Expand(depth);
            }

            result.ProvenUnsolvable = true;
            return result;
        }

        // Smallest pressure cap that still has a solution, or -1 if none is found within budget.
        public int MinimumPeak(TState start, int maxPressure, int nodesPerAttempt)
        {
            for (var cap = 1; cap <= maxPressure; cap++)
            {
                var result = Solve(start, new SearchBudget(nodesPerAttempt, cap));
                if (result.Solved) return cap;
            }

            return -1;
        }

        private void Expand(int depth)
        {
            var state = _states[depth];
            var moves = _moves[depth];
            var count = _domain.GetMoves(state, moves);
            if (_scores == null || _scores.Length < moves.Length) _scores = new int[moves.Length];
            for (var i = 0; i < count; i++) _scores[i] = _domain.Score(state, moves[i]);

            for (var i = 1; i < count; i++)
            {
                var move = moves[i];
                var score = _scores[i];
                var j = i - 1;
                while (j >= 0 && _scores[j] < score)
                {
                    moves[j + 1] = moves[j];
                    _scores[j + 1] = _scores[j];
                    j--;
                }

                moves[j + 1] = move;
                _scores[j + 1] = score;
            }

            _moveCounts[depth] = count;
            _next[depth] = 0;
        }

        private TMove[] BuildPath(int length)
        {
            var path = new TMove[length];
            for (var d = 0; d < length; d++) path[d] = _moves[d][_next[d] - 1];
            return path;
        }

        private void EnsureDepth(int depth)
        {
            while (_states.Count <= depth)
            {
                _states.Add(_domain.CreateScratch());
                _moves.Add(new TMove[_domain.MaxMoves]);
                _moveCounts.Add(0);
                _next.Add(0);
            }
        }
    }
}
