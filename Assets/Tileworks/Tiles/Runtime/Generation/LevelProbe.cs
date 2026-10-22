using System;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Search;

namespace Tileworks.Tiles.Generation
{
    public readonly struct ProbeStats
    {
        public readonly int Runs;
        public readonly int Wins;

        // Mean share of the move limit left on a win, and of the goal left on a loss.
        public readonly float MovesLeftOnWin;
        public readonly float GoalLeftOnLoss;

        // Share of losses that ended with at most a tenth of the goal left.
        public readonly float NearMisses;

        // Mean dead-board shuffles per game, the one at the start aside.
        public readonly float ShufflesPerGame;

        public ProbeStats(int runs, int wins, float movesLeftOnWin, float goalLeftOnLoss, float nearMisses, float shufflesPerGame = 0f)
        {
            ShufflesPerGame = shufflesPerGame;
            Runs = runs;
            Wins = wins;
            MovesLeftOnWin = movesLeftOnWin;
            GoalLeftOnLoss = goalLeftOnLoss;
            NearMisses = nearMisses;
        }

        public float WinRate => Runs == 0 ? 0f : (float)Wins / Runs;

        public override string ToString() =>
            $"win {WinRate:0.00} ({Wins}/{Runs}), moves left on wins {MovesLeftOnWin:0.00}, goal left on losses {GoalLeftOnLoss:0.00}, near misses {NearMisses:0.00}, shuffles {ShufflesPerGame:0.00}";
    }

    // Casual-bot playouts over several seeds, each a different starting board and refill stream, as players' attempts are.
    // Also measures how close the games were: wins with many moves to spare and losses far from the goal both read badly.
    // The game's side of a probe: a board per seed, and how far a state is from its goal and from running out of moves (0..1).
    public interface IProbeDomain<TState, TMove> where TMove : struct
    {
        (ISearchDomain<TState, TMove> Domain, TState Start) Create(ulong seed);
        float GoalLeft(TState state);
        float MovesLeft(TState state);

        // Dead-board shuffles so far in the game.
        int Shuffles(TState state) => 0;
    }

    public sealed class LevelProbe<TState, TMove> where TMove : struct
    {
        public const float NearMiss = 0.1f;

        private readonly IProbeDomain<TState, TMove> _game;

        public LevelProbe(IProbeDomain<TState, TMove> game) => _game = Guard.NotNull(game, nameof(game));

        public ProbeStats Run(IPlayoutPolicy<TState, TMove> policy, ulong seed, int seeds, int runsPerSeed)
        {
            int runs = 0, wins = 0, nearMisses = 0, shuffles = 0;
            float movesLeft = 0f, goalLeft = 0f;
            for (var s = 0; s < seeds; s++)
            {
                var boardSeed = StableHash.Combine(seed, (ulong)s);
                var (domain, start) = _game.Create(boardSeed);
                var scratch = domain.CreateScratch();
                var moves = new TMove[domain.MaxMoves];
                var rng = new Pcg32(boardSeed);
                for (var run = 0; run < runsPerSeed; run++)
                {
                    domain.CopyTo(start, scratch);
                    while (domain.Status(scratch) == SearchStatus.Ongoing)
                    {
                        var count = domain.GetMoves(scratch, moves);
                        if (count == 0) break;
                        domain.Apply(scratch, moves[policy.Choose(scratch, new ReadOnlySpan<TMove>(moves, 0, count), domain, ref rng)]);
                    }

                    runs++;
                    shuffles += _game.Shuffles(scratch);
                    if (domain.Status(scratch) == SearchStatus.Won)
                    {
                        wins++;
                        movesLeft += _game.MovesLeft(scratch);
                        continue;
                    }

                    var left = _game.GoalLeft(scratch);
                    goalLeft += left;
                    if (left <= NearMiss) nearMisses++;
                }
            }

            var losses = runs - wins;
            return new ProbeStats(runs, wins, wins == 0 ? 0f : movesLeft / wins, losses == 0 ? 0f : goalLeft / losses, losses == 0 ? 0f : (float)nearMisses / losses,
                runs == 0 ? 0f : (float)shuffles / runs);
        }
    }
}
