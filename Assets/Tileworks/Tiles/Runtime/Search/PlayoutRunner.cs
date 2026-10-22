using System;
using Tileworks.Kit.Core;

namespace Tileworks.Tiles.Search
{
    public readonly struct PlayoutStats
    {
        public readonly int Runs;
        public readonly int Wins;
        public readonly float AverageMoves;

        public PlayoutStats(int runs, int wins, float averageMoves)
        {
            Runs = runs;
            Wins = wins;
            AverageMoves = averageMoves;
        }

        public float WinRate => Runs == 0 ? 0 : (float)Wins / Runs;
    }

    // Monte-Carlo playouts with a pluggable policy, for difficulty metrics and bot playtests.
    public sealed class PlayoutRunner<TState, TMove> where TMove : struct
    {
        private readonly ISearchDomain<TState, TMove> _domain;
        private readonly TState _scratch;
        private readonly TMove[] _moves;

        public PlayoutRunner(ISearchDomain<TState, TMove> domain)
        {
            _domain = domain ?? throw new ArgumentNullException(nameof(domain));
            _scratch = domain.CreateScratch();
            _moves = new TMove[domain.MaxMoves];
        }

        public PlayoutStats Run(TState start, IPlayoutPolicy<TState, TMove> policy, int runs, ulong seed)
        {
            var rng = new Pcg32(seed);
            var wins = 0;
            var totalMoves = 0;
            for (var run = 0; run < runs; run++)
            {
                _domain.CopyTo(start, _scratch);
                while (_domain.Status(_scratch) == SearchStatus.Ongoing)
                {
                    var count = _domain.GetMoves(_scratch, _moves);
                    if (count == 0) break;
                    var choice = policy.Choose(_scratch, new ReadOnlySpan<TMove>(_moves, 0, count), _domain, ref rng);
                    _domain.Apply(_scratch, _moves[choice]);
                    totalMoves++;
                }

                if (_domain.Status(_scratch) == SearchStatus.Won) wins++;
            }

            return new PlayoutStats(runs, wins, runs == 0 ? 0 : (float)totalMoves / runs);
        }
    }

    public sealed class RandomPolicy<TState, TMove> : IPlayoutPolicy<TState, TMove> where TMove : struct
    {
        public int Choose(TState s, ReadOnlySpan<TMove> moves, ISearchDomain<TState, TMove> domain, ref Pcg32 rng) => rng.NextInt(moves.Length);
    }

    // Picks among the best-scoring moves, breaking ties at random.
    public sealed class GreedyPolicy<TState, TMove> : IPlayoutPolicy<TState, TMove> where TMove : struct
    {
        public int Choose(TState s, ReadOnlySpan<TMove> moves, ISearchDomain<TState, TMove> domain, ref Pcg32 rng)
        {
            var best = int.MinValue;
            var ties = 0;
            var choice = 0;
            for (var i = 0; i < moves.Length; i++)
            {
                var score = domain.Score(s, moves[i]);
                if (score > best)
                {
                    best = score;
                    choice = i;
                    ties = 1;
                }
                else if (score == best && rng.NextInt(++ties) == 0)
                {
                    choice = i;
                }
            }

            return choice;
        }
    }

    // Greedy most of the time, a random legal move with probability Epsilon: a model of a casual player.
    public sealed class EpsilonGreedyPolicy<TState, TMove> : IPlayoutPolicy<TState, TMove> where TMove : struct
    {
        private readonly int _epsilonPercent;
        private readonly GreedyPolicy<TState, TMove> _greedy = new GreedyPolicy<TState, TMove>();

        public EpsilonGreedyPolicy(int epsilonPercent) => _epsilonPercent = epsilonPercent;

        public int Choose(TState s, ReadOnlySpan<TMove> moves, ISearchDomain<TState, TMove> domain, ref Pcg32 rng) =>
            rng.Chance(_epsilonPercent, 100) ? rng.NextInt(moves.Length) : _greedy.Choose(s, moves, domain, ref rng);
    }
}
