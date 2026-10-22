using System;
using System.Collections.Generic;
using Tileworks.Tiles.Search;
using TileMatch.Model;

namespace TileMatch.Generation
{
    public enum DifficultyBand : byte
    {
        Easy,
        Normal,
        Hard,
        SuperHard,
    }

    public sealed class DifficultyMetrics
    {
        public bool Solvable;
        public bool SolverExhausted;
        public int SolverNodes;
        public float PRandom;
        public float PGreedy;
        public float PCasual;
        public int MinPeak;
        public float DeadEndRatio;
        public int VisibleKindsAtStart;

        public override string ToString() =>
            $"solvable={Solvable} pRandom={PRandom:0.00} pGreedy={PGreedy:0.00} pCasual={PCasual:0.00} minPeak={MinPeak} deadEnds={DeadEndRatio:0.00} visibleKinds={VisibleKindsAtStart} nodes={SolverNodes}";
    }

    public sealed class AnalyzerSettings
    {
        public int Playouts = 300;
        public int CasualEpsilonPercent = 25;
        public int SolverNodes = 200_000;
        public int PeakNodes = 20_000;
        public ulong Seed = 1;
    }

    public static class DifficultyAnalyzer
    {
        public static DifficultyMetrics Measure(TileMatchRules rules, TileMatchLevel level, AnalyzerSettings settings)
        {
            var domain = new TileMatchSearchDomain(rules, level);
            var start = domain.CreateStart();
            var solver = new DepthFirstSolver<TileMatchState, SelectTile>(domain);
            var solve = solver.Solve(start, new SearchBudget(settings.SolverNodes));
            var runner = new PlayoutRunner<TileMatchState, SelectTile>(domain);

            return new DifficultyMetrics
            {
                Solvable = solve.Solved,
                SolverExhausted = solve.BudgetExhausted,
                SolverNodes = solve.Nodes,
                DeadEndRatio = solve.DeadEndRatio,
                PRandom = runner.Run(start, new RandomPolicy<TileMatchState, SelectTile>(), settings.Playouts, settings.Seed).WinRate,
                PGreedy = runner.Run(start, new GreedyPolicy<TileMatchState, SelectTile>(), settings.Playouts, settings.Seed + 1).WinRate,
                PCasual = runner.Run(start, new EpsilonGreedyPolicy<TileMatchState, SelectTile>(settings.CasualEpsilonPercent), settings.Playouts, settings.Seed + 2).WinRate,
                MinPeak = solve.Solved ? solver.MinimumPeak(start, level.TrayCapacity, settings.PeakNodes) : -1,
                VisibleKindsAtStart = VisibleKinds(start),
            };
        }

        private static int VisibleKinds(TileMatchState s)
        {
            var kinds = new HashSet<ushort>();
            for (var tile = 0; tile < s.TileCount; tile++)
            {
                if (s.IsFree(tile)) kinds.Add(s.Kinds[tile]);
            }

            return kinds.Count;
        }
    }

    // Lower bounds on casual-player (ε-greedy) success per band; recalibrated from attempt data after playtests.
    public sealed class DifficultyBands
    {
        public float EasyMinCasual = 0.7f;
        public float NormalMinCasual = 0.35f;
        public float HardMinCasual = 0.05f;

        public DifficultyBand Classify(DifficultyMetrics m) =>
            m.PCasual >= EasyMinCasual ? DifficultyBand.Easy
            : m.PCasual >= NormalMinCasual ? DifficultyBand.Normal
            : m.PCasual >= HardMinCasual ? DifficultyBand.Hard
            : DifficultyBand.SuperHard;

        public static string Label(DifficultyBand band) => band switch
        {
            DifficultyBand.Easy => "easy",
            DifficultyBand.Normal => "normal",
            DifficultyBand.Hard => "hard",
            _ => "superHard",
        };

        public static DifficultyBand Parse(string label) => label switch
        {
            "easy" => DifficultyBand.Easy,
            "hard" => DifficultyBand.Hard,
            "superHard" => DifficultyBand.SuperHard,
            _ => DifficultyBand.Normal,
        };
    }
}
