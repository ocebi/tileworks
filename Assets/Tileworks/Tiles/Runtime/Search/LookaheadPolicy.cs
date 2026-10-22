using System;
using Tileworks.Kit.Core;

namespace Tileworks.Tiles.Search
{
    // A skilled player's judgement: how good a move looks before it is played, and how good a settled position is.
    // Both are called many times per decision, so implementations must not allocate.
    public interface IPositionEvaluator<TState, TMove> where TMove : struct
    {
        int Score(TState s, in TMove move);
        int Value(TState s);
    }

    // Takes the best few moves by the evaluator's score, plays each on a scratch state and keeps the one with the highest
    // score + position value + a share of the best follow-up move's score. Ties break at random, so a seed decides every
    // choice. Not thread-safe: one policy per thread.
    public sealed class LookaheadPolicy<TState, TMove> : IPlayoutPolicy<TState, TMove> where TMove : struct
    {
        private const int Win = int.MaxValue / 4;

        private readonly IPositionEvaluator<TState, TMove> _evaluator;
        private readonly int _epsilonPercent;
        private readonly int _breadth;
        private readonly int _followUpPercent;
        private readonly int[] _top;
        private readonly int[] _topScores;
        private ISearchDomain<TState, TMove> _domain;
        private TState _scratch;
        private TMove[] _followUps;

        public LookaheadPolicy(IPositionEvaluator<TState, TMove> evaluator, int epsilonPercent, int breadth, int followUpPercent)
        {
            _evaluator = Guard.NotNull(evaluator, nameof(evaluator));
            _epsilonPercent = epsilonPercent;
            _breadth = Math.Clamp(breadth, 1, BotProfile.MaxBreadth);
            _followUpPercent = followUpPercent;
            _top = new int[_breadth];
            _topScores = new int[_breadth];
        }

        public int Choose(TState s, ReadOnlySpan<TMove> moves, ISearchDomain<TState, TMove> domain, ref Pcg32 rng)
        {
            if (rng.Chance(_epsilonPercent, 100)) return rng.NextInt(moves.Length);
            var count = SelectTop(s, moves);
            if (count == 1) return _top[0];

            Bind(domain);
            var best = long.MinValue;
            var choice = _top[0];
            var ties = 0;
            for (var i = 0; i < count; i++)
            {
                domain.CopyTo(s, _scratch);
                domain.Apply(_scratch, moves[_top[i]]);
                var value = _topScores[i] + Outcome(domain);
                if (value > best)
                {
                    best = value;
                    choice = _top[i];
                    ties = 1;
                }
                else if (value == best && rng.NextInt(++ties) == 0)
                {
                    choice = _top[i];
                }
            }

            return choice;
        }

        private long Outcome(ISearchDomain<TState, TMove> domain)
        {
            var status = domain.Status(_scratch);
            if (status == SearchStatus.Won) return Win;
            if (status == SearchStatus.Lost) return -Win;

            var followUp = 0;
            var count = domain.GetMoves(_scratch, _followUps);
            for (var i = 0; i < count; i++) followUp = Math.Max(followUp, _evaluator.Score(_scratch, _followUps[i]));
            return _evaluator.Value(_scratch) + (long)followUp * _followUpPercent / 100;
        }

        // The highest-scoring moves, best first; equal scores keep move order.
        private int SelectTop(TState s, ReadOnlySpan<TMove> moves)
        {
            var count = 0;
            for (var i = 0; i < moves.Length; i++)
            {
                var score = _evaluator.Score(s, moves[i]);
                if (count == _breadth && score <= _topScores[count - 1]) continue;
                var j = count < _breadth ? count++ : count - 1;
                while (j > 0 && _topScores[j - 1] < score)
                {
                    _top[j] = _top[j - 1];
                    _topScores[j] = _topScores[j - 1];
                    j--;
                }

                _top[j] = i;
                _topScores[j] = score;
            }

            return count;
        }

        // Probes make a new domain per board, so the scratch follows the domain it is applied with.
        private void Bind(ISearchDomain<TState, TMove> domain)
        {
            if (ReferenceEquals(domain, _domain)) return;
            _domain = domain;
            _scratch = domain.CreateScratch();
            _followUps = new TMove[domain.MaxMoves];
        }
    }
}
